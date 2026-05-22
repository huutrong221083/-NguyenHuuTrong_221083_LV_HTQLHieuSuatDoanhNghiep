using LuanVan.Data;
using Microsoft.EntityFrameworkCore;

namespace LuanVan.Services;

public interface ITaskDelayLinearRegressionService
{
    Task<TaskDelayPrediction> PredictAsync(AiTaskDelayModelInput input, string? correlationId = null, CancellationToken cancellationToken = default);
}

public sealed class TaskDelayPrediction
{
    public double EstimatedDaysLate { get; set; }
    public string RiskLevel { get; set; } = "LOW";
    public string ModelName { get; set; } = "task_delay_linear_regression";
    public string ModelVersion { get; set; } = "v1";
    public string PredictionSource { get; set; } = "python_ai_service";
    public int InferenceTimeMs { get; set; }
    public string? CorrelationId { get; set; }
}

public sealed class TaskDelayLinearRegressionService : ITaskDelayLinearRegressionService
{
    private readonly AppDbContext _dbContext;
    private readonly IAiPythonClient _aiPythonClient;

    public TaskDelayLinearRegressionService(AppDbContext dbContext, IAiPythonClient aiPythonClient)
    {
        _dbContext = dbContext;
        _aiPythonClient = aiPythonClient;
    }

    public async Task<TaskDelayPrediction> PredictAsync(AiTaskDelayModelInput input, string? correlationId = null, CancellationToken cancellationToken = default)
    {
        var trainingRows = await BuildTrainingRowsAsync(cancellationToken);
        if (trainingRows.Count < 2)
        {
            throw new AiPythonBusinessException("INSUFFICIENT_TRAINING_DATA", "Khong du du lieu de huan luyen Task Delay model.");
        }

        var response = await _aiPythonClient.PredictTaskDelayAsync(new PyTaskDelayPredictRequest
        {
            CorrelationId = correlationId,
            TrainingRows = trainingRows,
            InputFeatures = new PyTaskDelayInputFeatures
            {
                EstimatedHours = input.EstimatedHours,
                SpentHours = input.SpentHours,
                ProgressPercent = input.TienDoHienTai,
                PriorityScore = input.DoUuTien,
                DifficultyScore = input.DoKho,
                DaysUntilDeadline = input.SoNgayConLai
            }
        }, cancellationToken);

        return new TaskDelayPrediction
        {
            EstimatedDaysLate = response.EstimatedDaysLate,
            RiskLevel = response.RiskLevel,
            ModelName = response.ModelName,
            ModelVersion = response.ModelVersion,
            PredictionSource = response.PredictionSource,
            InferenceTimeMs = response.InferenceTimeMs,
            CorrelationId = response.CorrelationId
        };
    }

    private async Task<List<PyTaskDelayTrainingRow>> BuildTrainingRowsAsync(CancellationToken cancellationToken)
    {
        var rows = await (
            from pc in _dbContext.PhanCongNhanViens.AsNoTracking()
            join cv in _dbContext.CongViecs.AsNoTracking() on pc.MaCongViec equals cv.MaCongViec
            where cv.HanHoanThanh.HasValue && pc.NgayBatDauThucTe.HasValue
            select new
            {
                cv.MaCongViec,
                cv.HanHoanThanh,
                pc.NgayBatDauThucTe,
                pc.NgayKetThucThucTe,
                cv.MaDoKho,
                HeSoDoKho = cv.DoKho != null ? cv.DoKho.HeSo : (decimal?)null,
                cv.MaDoUuTien,
                HeSoDoUuTien = cv.DoUuTien != null ? cv.DoUuTien.HeSo : (decimal?)null
            })
            .ToListAsync(cancellationToken);

        var latestProgressMap = await _dbContext.TienDoCongViecs
            .AsNoTracking()
            .GroupBy(x => x.MaCongViec)
            .Select(g => new
            {
                MaCongViec = g.Key,
                Progress = g.OrderByDescending(x => x.NgayCapNhat).Select(x => (double?)x.PhanTramHoanThanh).FirstOrDefault()
            })
            .ToDictionaryAsync(x => x.MaCongViec, x => x.Progress ?? 0, cancellationToken);

        var now = DateTime.UtcNow.Date;
        var result = new List<PyTaskDelayTrainingRow>();

        foreach (var row in rows)
        {
            var startDate = row.NgayBatDauThucTe!.Value.Date;
            var dueDate = row.HanHoanThanh!.Value.Date;
            var finishedDate = row.NgayKetThucThucTe?.Date ?? now;

            var estimatedHours = Math.Max(1d, (dueDate - startDate).TotalHours);
            var spentHours = Math.Max(0d, (finishedDate - startDate).TotalHours);
            var lateDays = Math.Max(0d, (finishedDate - dueDate).TotalDays);
            var progress = latestProgressMap.GetValueOrDefault(row.MaCongViec, 0d);

            result.Add(new PyTaskDelayTrainingRow
            {
                EstimatedHours = Math.Round(estimatedHours, 2),
                SpentHours = Math.Round(spentHours, 2),
                ProgressPercent = progress,
                PriorityScore = (double)(row.HeSoDoUuTien ?? row.MaDoUuTien ?? 1),
                DifficultyScore = (double)(row.HeSoDoKho ?? row.MaDoKho ?? 1),
                DaysUntilDeadline = Math.Max(-30d, (dueDate - now).TotalDays),
                LateDays = Math.Round(lateDays, 2)
            });
        }

        return result;
    }
}
