using System.Text.Json;
using LuanVan.Contracts;
using LuanVan.Data;
using LuanVan.Models;
using Microsoft.EntityFrameworkCore;

namespace LuanVan.Services;

public sealed class AiPredictionService : IAiPredictionService
{
    private const string LinearRegressionModelName = "task_delay_linear_regression";
    private const string RandomForestModelName = "employee_performance_random_forest";

    private readonly AppDbContext _dbContext;
    private readonly IAiFeatureBuilderService _featureBuilder;
    private readonly IAiDataValidationService _validator;
    private readonly ITaskDelayLinearRegressionService _linearRegressionService;
    private readonly IEmployeePerformanceRandomForestService _randomForestService;
    private readonly IAiRuntimeSettingsProvider _runtimeSettingsProvider;

    public AiPredictionService(
        AppDbContext dbContext,
        IAiFeatureBuilderService featureBuilder,
        IAiDataValidationService validator,
        ITaskDelayLinearRegressionService linearRegressionService,
        IEmployeePerformanceRandomForestService randomForestService,
        IAiRuntimeSettingsProvider runtimeSettingsProvider)
    {
        _dbContext = dbContext;
        _featureBuilder = featureBuilder;
        _validator = validator;
        _linearRegressionService = linearRegressionService;
        _randomForestService = randomForestService;
        _runtimeSettingsProvider = runtimeSettingsProvider;
    }

    public async Task<ApiResponse<PredictDelayResultDto>> PredictDelayAsync(PredictDelayCommand command, string? actorUserId, CancellationToken cancellationToken = default)
    {
        try
        {
            var runtime = await _runtimeSettingsProvider.GetAsync(cancellationToken);
            if (!runtime.General.Enabled)
            {
                return ApiResponse<PredictDelayResultDto>.Fail("AI đang tắt trong Cài đặt hệ thống.");
            }

            var input = await _featureBuilder.BuildTaskDelayInputAsync(command, cancellationToken);
            if (input == null)
            {
                return ApiResponse<PredictDelayResultDto>.Fail("Dữ liệu công việc chưa đủ để dự báo trễ hạn. Hãy bổ sung ngày bắt đầu, deadline hoặc tiến độ.");
            }

            var validationErrors = _validator.ValidateTaskDelayInput(input);
            if (validationErrors.Count > 0)
            {
                return ApiResponse<PredictDelayResultDto>.Fail(string.Join(" | ", validationErrors));
            }

            TaskDelayPrediction prediction;
            try
            {
                prediction = await _linearRegressionService.PredictAsync(input, command.CorrelationId, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                prediction = BuildFallbackDelayPrediction(input, runtime, command.CorrelationId);
            }

            try
            {
                var predictionRecord = await SavePredictionAsync(
                    modelName: prediction.ModelName,
                    maNhanVien: input.MaNhanVien ?? 0,
                    thang: DateTime.UtcNow.Month,
                    nam: DateTime.UtcNow.Year,
                    inputData: input,
                    outputData: prediction,
                    actorUserId: actorUserId,
                    diemDuDoan: null,
                    xacSuatTreHan: RiskToProbability(prediction.RiskLevel),
                    runtime: runtime,
                    cancellationToken: cancellationToken);

                if (predictionRecord != null)
                {
                    try
                    {
                        await SaveTaskFeatureSnapshotsAsync(predictionRecord.MaModel, predictionRecord.MaDuDoan, input, prediction, cancellationToken);
                    }
                    catch (Exception snapshotEx)
                    {
                        Console.Error.WriteLine(snapshotEx);
                    }
                }
            }
            catch (Exception saveEx)
            {
                Console.Error.WriteLine(saveEx);
            }

            return ApiResponse<PredictDelayResultDto>.Ok(new PredictDelayResultDto
            {
                CorrelationId = prediction.CorrelationId ?? command.CorrelationId,
                EstimatedDaysLate = prediction.EstimatedDaysLate,
                RiskLevel = prediction.RiskLevel,
                Percent = (double)RiskToProbability(prediction.RiskLevel) * 100d,
                Reason = prediction.RiskLevel switch
                {
                    "HIGH" => "Nguy co tre han cao do do kho va thoi gian con lai khong an toan.",
                    "MEDIUM" => "Co dau hieu cham tien do, can theo doi sat.",
                    _ => "Muc do an toan, nhieu kha nang hoan thanh dung han."
                },
                ModelName = prediction.ModelName,
                ModelVersion = prediction.ModelVersion,
                PredictionSource = prediction.PredictionSource,
                InferenceTimeMs = prediction.InferenceTimeMs
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return ApiResponse<PredictDelayResultDto>.Fail($"Không thể tạo dự báo AI: {ex.Message}");
        }
    }

    private static TaskDelayPrediction BuildFallbackDelayPrediction(AiTaskDelayModelInput input, AiRuntimeSettings runtime, string? correlationId)
    {
        var w = runtime.General.Weights ?? new AiRuntimeWeightSettings();
        var completionW = Math.Max(1, w.Completion) / 100d;
        var overdueW = Math.Max(1, w.Overdue) / 100d;
        var difficultyW = Math.Max(1, w.Difficulty) / 100d;

        var difficultyPressure = (input.DoKho * difficultyW) + (input.DoUuTien * (difficultyW * 0.6d)) + (Math.Max(1, input.SoNguoiThamGia) * 0.2);
        var progressRelief = input.TienDoHienTai * completionW / 20d;
        var deadlinePressure = input.SoNgayConLai < 0
            ? Math.Min(5d, Math.Abs(input.SoNgayConLai) * (0.55d + overdueW))
            : Math.Max(0d, 1.5d - (input.SoNgayConLai * 0.15d));

        var estimatedDaysLate = Math.Max(0d, difficultyPressure + deadlinePressure - progressRelief);
        var riskLevel = estimatedDaysLate <= 0d ? "LOW" : estimatedDaysLate <= 3d ? "MEDIUM" : "HIGH";

        return new TaskDelayPrediction
        {
            CorrelationId = correlationId,
            EstimatedDaysLate = Math.Round(estimatedDaysLate, 2),
            RiskLevel = riskLevel,
            ModelName = "task_delay_fallback_rule",
            ModelVersion = "fallback_v1",
            PredictionSource = "csharp_fallback_rule",
            InferenceTimeMs = 0
        };
    }

    public async Task<ApiResponse<ClassifyPerformanceResultDto>> ClassifyPerformanceAsync(ClassifyPerformanceCommand command, string? actorUserId, CancellationToken cancellationToken = default)
    {
        var runtime = await _runtimeSettingsProvider.GetAsync(cancellationToken);
        if (!runtime.General.Enabled)
        {
            return ApiResponse<ClassifyPerformanceResultDto>.Fail("AI đang tắt trong Cài đặt hệ thống.");
        }

        var input = await _featureBuilder.BuildPerformanceInputAsync(command, cancellationToken);
        if (input == null)
        {
            return ApiResponse<ClassifyPerformanceResultDto>.Fail("Khong du du lieu de tao model input cho phan loai hieu suat.");
        }

        var validationErrors = _validator.ValidatePerformanceInput(input);
        if (validationErrors.Count > 0)
        {
            return ApiResponse<ClassifyPerformanceResultDto>.Fail(string.Join(" | ", validationErrors));
        }

        PerformanceClassificationPrediction prediction;
        try
        {
            prediction = await _randomForestService.PredictAsync(input, command.CorrelationId, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            prediction = BuildFallbackPerformancePrediction(input, command.CorrelationId);
        }

        var predictionRecord = await SavePredictionAsync(
            modelName: prediction.ModelName,
            maNhanVien: input.MaNhanVien,
            thang: DateTime.UtcNow.Month,
            nam: DateTime.UtcNow.Year,
            inputData: input,
            outputData: prediction,
            actorUserId: actorUserId,
            diemDuDoan: LabelToScore(prediction.Label),
            xacSuatTreHan: null,
            runtime: runtime,
            cancellationToken: cancellationToken);

        if (predictionRecord != null)
        {
            await SavePerformanceFeatureSnapshotsAsync(predictionRecord.MaModel, predictionRecord.MaDuDoan, input, prediction, cancellationToken);
        }

        return ApiResponse<ClassifyPerformanceResultDto>.Ok(new ClassifyPerformanceResultDto
        {
            CorrelationId = prediction.CorrelationId ?? command.CorrelationId,
            Label = prediction.Label,
            LabelDisplay = ToVietnameseLabel(prediction.Label),
            Confidence = prediction.Confidence,
            ModelName = prediction.ModelName,
            ModelVersion = prediction.ModelVersion,
            PredictionSource = prediction.PredictionSource,
            InferenceTimeMs = prediction.InferenceTimeMs
        });
    }

    private static PerformanceClassificationPrediction BuildFallbackPerformancePrediction(AiPerformanceModelInput input, string? correlationId)
    {
        var denominator = Math.Max(1, input.SoCongViecHoanThanh + input.SoCongViecTreHan);
        var lateRate = input.SoCongViecTreHan / (double)denominator;
        var score = input.KpiTrungBinh - (lateRate * 30d) + (input.SoCongViecHoanThanh * 2d) - (input.ThoiGianTrungBinh * 0.3d);

        var label = score >= 85 ? "EXCELLENT"
            : score >= 70 ? "GOOD"
            : score >= 50 ? "NORMAL"
            : "LOW";

        var confidence = score >= 85 || score < 50 ? 0.82 : score >= 70 ? 0.72 : 0.64;

        return new PerformanceClassificationPrediction
        {
            CorrelationId = correlationId,
            Label = label,
            Confidence = confidence,
            ModelName = "employee_performance_fallback_rule",
            ModelVersion = "fallback_v1",
            PredictionSource = "csharp_fallback_rule",
            InferenceTimeMs = 0
        };
    }

    private async Task<DuDoanAi?> SavePredictionAsync(
        string modelName,
        int maNhanVien,
        int thang,
        int nam,
        object inputData,
        object outputData,
        string? actorUserId,
        decimal? diemDuDoan,
        decimal? xacSuatTreHan,
        AiRuntimeSettings runtime,
        CancellationToken cancellationToken)
    {
        if (maNhanVien <= 0)
        {
            return null;
        }

        var modelId = await GetOrCreateModelIdAsync(modelName, cancellationToken);

        var serializedInput = JsonSerializer.Serialize(inputData);
        var serializedOutput = JsonSerializer.Serialize(outputData);
        var now = DateTime.UtcNow;
        var employeeDisplayName = maNhanVien > 0 ? $"NV {maNhanVien}" : "Nhân viên";
        var isDelayModel = modelName.Contains("delay", StringComparison.OrdinalIgnoreCase);
        var riskLevel = outputData is TaskDelayPrediction delay ? delay.RiskLevel : "LOW";
        var label = outputData is PerformanceClassificationPrediction perf ? perf.Label : "NORMAL";
        var reason = isDelayModel
            ? (riskLevel == "HIGH"
                ? "khối lượng và hạn hoàn thành chưa cân bằng"
                : riskLevel == "MEDIUM"
                    ? "tiến độ có dấu hiệu chậm"
                    : "rủi ro thấp")
            : $"mức hiệu suất hiện tại: {ToVietnameseLabel(label)}";
        var solution = isDelayModel
            ? (runtime.Automation.AutoSuggestDeadline
                ? "xem xét điều chỉnh deadline, ưu tiên lại task quan trọng và tăng hỗ trợ nguồn lực"
                : "ưu tiên lại task quan trọng và theo dõi tiến độ sát hơn")
            : "tăng theo dõi KPI, phân bổ lại nhiệm vụ và hỗ trợ kỹ năng đúng điểm yếu";
        var suggestionText = BuildSuggestionText(runtime, employeeDisplayName, reason, solution);
        var resourceHint = runtime.Automation.SuggestPeople
            ? "Gợi ý người/nguồn lực dựa trên kỹ năng và lịch sử hiệu suất đang bật."
            : "Gợi ý nguồn lực tự động đang tắt.";

        var existing = await _dbContext.DuDoanAis
            .FirstOrDefaultAsync(x => x.MaNhanVien == maNhanVien && x.ModelName == modelName && x.thang == thang && x.nam == nam, cancellationToken);

        DuDoanAi entity;
        if (existing == null)
        {
            entity = new DuDoanAi
            {
                MaNhanVien = maNhanVien,
                MaModel = modelId,
                ModelName = modelName,
                thang = thang,
                nam = nam,
                DiemDuDoan = diemDuDoan,
                XacSuatTreHan = xacSuatTreHan,
                InputData = serializedInput,
                OutputData = serializedOutput,
                Actor = actorUserId,
                ThoiGianDuDoan = now,
                DeXuatCaiThien = suggestionText,
                GoiYNguonLuc = resourceHint
            };

            _dbContext.DuDoanAis.Add(entity);
        }
        else
        {
            entity = existing;
            entity.MaModel = modelId;
            entity.ModelName = modelName;
            entity.DiemDuDoan = diemDuDoan;
            entity.XacSuatTreHan = xacSuatTreHan;
            entity.InputData = serializedInput;
            entity.OutputData = serializedOutput;
            entity.Actor = actorUserId;
            entity.ThoiGianDuDoan = now;
            entity.DeXuatCaiThien = suggestionText;
            entity.GoiYNguonLuc = resourceHint;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private async Task SaveTaskFeatureSnapshotsAsync(int modelId, int predictionId, AiTaskDelayModelInput input, TaskDelayPrediction prediction, CancellationToken cancellationToken)
    {
        if (modelId <= 0 || predictionId <= 0)
        {
            return;
        }

        var sourceKey = $"DUDOANAI:{predictionId}";
        var existingSnapshots = await _dbContext.AiFeatureStores
            .Where(x => x.SourceTable == "DUDOANAI" && x.SourceKey == sourceKey)
            .ToListAsync(cancellationToken);

        if (existingSnapshots.Count > 0)
        {
            _dbContext.AiFeatureStores.RemoveRange(existingSnapshots);
        }

        var now = DateTime.UtcNow;
        var versionTag = $"{prediction.ModelName}:{prediction.ModelVersion}";

        _dbContext.AiFeatureStores.AddRange(new[]
        {
            CreateFeatureSnapshot(modelId, input.MaNhanVien, input.MaCongViec, "EstimatedHours", input.EstimatedHours, "number", sourceKey, versionTag, now),
            CreateFeatureSnapshot(modelId, input.MaNhanVien, input.MaCongViec, "SpentHours", input.SpentHours, "number", sourceKey, versionTag, now),
            CreateFeatureSnapshot(modelId, input.MaNhanVien, input.MaCongViec, "DoKho", input.DoKho, "number", sourceKey, versionTag, now),
            CreateFeatureSnapshot(modelId, input.MaNhanVien, input.MaCongViec, "DoUuTien", input.DoUuTien, "number", sourceKey, versionTag, now),
            CreateFeatureSnapshot(modelId, input.MaNhanVien, input.MaCongViec, "SoNguoiThamGia", input.SoNguoiThamGia, "number", sourceKey, versionTag, now),
            CreateFeatureSnapshot(modelId, input.MaNhanVien, input.MaCongViec, "TienDoHienTai", input.TienDoHienTai, "number", sourceKey, versionTag, now),
            CreateFeatureSnapshot(modelId, input.MaNhanVien, input.MaCongViec, "SoNgayConLai", input.SoNgayConLai, "number", sourceKey, versionTag, now),
            CreateFeatureSnapshot(modelId, input.MaNhanVien, input.MaCongViec, "EstimatedDaysLate", prediction.EstimatedDaysLate, "number", sourceKey, versionTag, now),
            CreateFeatureSnapshot(modelId, input.MaNhanVien, input.MaCongViec, "RiskLevel", prediction.RiskLevel, "string", sourceKey, versionTag, now),
            CreateFeatureSnapshot(modelId, input.MaNhanVien, input.MaCongViec, "PredictionSource", prediction.PredictionSource, "string", sourceKey, versionTag, now)
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SavePerformanceFeatureSnapshotsAsync(int modelId, int predictionId, AiPerformanceModelInput input, PerformanceClassificationPrediction prediction, CancellationToken cancellationToken)
    {
        if (modelId <= 0 || predictionId <= 0)
        {
            return;
        }

        var sourceKey = $"DUDOANAI:{predictionId}";
        var existingSnapshots = await _dbContext.AiFeatureStores
            .Where(x => x.SourceTable == "DUDOANAI" && x.SourceKey == sourceKey)
            .ToListAsync(cancellationToken);

        if (existingSnapshots.Count > 0)
        {
            _dbContext.AiFeatureStores.RemoveRange(existingSnapshots);
        }

        var now = DateTime.UtcNow;
        var versionTag = $"{prediction.ModelName}:{prediction.ModelVersion}";

        _dbContext.AiFeatureStores.AddRange(new[]
        {
            CreateFeatureSnapshot(modelId, input.MaNhanVien, null, "SoCongViecHoanThanh", input.SoCongViecHoanThanh, "number", sourceKey, versionTag, now),
            CreateFeatureSnapshot(modelId, input.MaNhanVien, null, "SoCongViecTreHan", input.SoCongViecTreHan, "number", sourceKey, versionTag, now),
            CreateFeatureSnapshot(modelId, input.MaNhanVien, null, "ThoiGianTrungBinh", input.ThoiGianTrungBinh, "number", sourceKey, versionTag, now),
            CreateFeatureSnapshot(modelId, input.MaNhanVien, null, "KpiTrungBinh", input.KpiTrungBinh, "number", sourceKey, versionTag, now),
            CreateFeatureSnapshot(modelId, input.MaNhanVien, null, "Label", prediction.Label, "string", sourceKey, versionTag, now),
            CreateFeatureSnapshot(modelId, input.MaNhanVien, null, "Confidence", prediction.Confidence, "number", sourceKey, versionTag, now),
            CreateFeatureSnapshot(modelId, input.MaNhanVien, null, "PredictionSource", prediction.PredictionSource, "string", sourceKey, versionTag, now)
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AiFeatureStore CreateFeatureSnapshot(
        int modelId,
        int? maNhanVien,
        int? maCongViec,
        string featureName,
        object? featureValue,
        string featureType,
        string sourceKey,
        string versionTag,
        DateTime now)
    {
        return new AiFeatureStore
        {
            MaModel = modelId,
            MaNhanVien = maNhanVien,
            MaCongViec = maCongViec,
            FeatureName = featureName,
            FeatureValue = featureValue?.ToString(),
            FeatureType = featureType,
            SourceTable = "DUDOANAI",
            SourceKey = sourceKey,
            VersionTag = versionTag,
            DongChot = now
        };
    }

    private async Task<int> GetOrCreateModelIdAsync(string modelName, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.MoHinhAis.FirstOrDefaultAsync(x => x.TenModel == modelName, cancellationToken);
        if (existing != null)
        {
            return existing.MaModel;
        }

        var model = new MoHinhAi
        {
            TenModel = modelName,
            Version = "v1",
            NgayTrain = DateTime.UtcNow
        };

        _dbContext.MoHinhAis.Add(model);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return model.MaModel;
    }

    private static decimal RiskToProbability(string riskLevel)
    {
        return riskLevel switch
        {
            "HIGH" => 0.8m,
            "MEDIUM" => 0.5m,
            _ => 0.2m
        };
    }

    private static decimal LabelToScore(string label)
    {
        return label switch
        {
            "EXCELLENT" => 95m,
            "GOOD" => 80m,
            "NORMAL" => 60m,
            _ => 35m
        };
    }

    private static string ToVietnameseLabel(string label)
    {
        return label switch
        {
            "EXCELLENT" => "Xuat sac",
            "GOOD" => "Tot",
            "NORMAL" => "Trung binh",
            "LOW" => "Yeu",
            _ => "Trung binh"
        };
    }

    private static string BuildSuggestionText(AiRuntimeSettings runtime, string employeeDisplayName, string reason, string solution)
    {
        var template = runtime.Automation?.SuggestionTemplate;
        if (string.IsNullOrWhiteSpace(template))
        {
            template = "Nhân viên {Ten} có nguy cơ trễ hạn do {LyDo}. Đề xuất: {GiaiPhap}";
        }

        return template
            .Replace("{Ten}", employeeDisplayName, StringComparison.OrdinalIgnoreCase)
            .Replace("{LyDo}", reason, StringComparison.OrdinalIgnoreCase)
            .Replace("{GiaiPhap}", solution, StringComparison.OrdinalIgnoreCase);
    }
}
