using LuanVan.Contracts;
using LuanVan.Data;
using LuanVan.Models;
using LuanVan.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LuanVan.Controllers.Api;

[ApiController]
[Route("ai")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IAiPredictionService _aiPredictionService;

    public AiController(AppDbContext dbContext, IAiPredictionService aiPredictionService)
    {
        _dbContext = dbContext;
        _aiPredictionService = aiPredictionService;
    }

    [HttpGet("models")]
    public async Task<ActionResult<ApiResponse<List<AiModelItemDto>>>> GetModels(CancellationToken cancellationToken)
    {
        try
        {
            var models = await _dbContext.MoHinhAis
                .AsNoTracking()
                .OrderByDescending(m => m.MaModel)
                .ToListAsync(cancellationToken);

            var latestEvalByModel = await _dbContext.AiDanhGiaRuns
                .AsNoTracking()
                .GroupBy(x => x.MaModel)
                .Select(g => new { MaModel = g.Key, Latest = g.OrderByDescending(x => x.NgayDanhGia).FirstOrDefault() })
                .ToDictionaryAsync(x => x.MaModel, x => x.Latest, cancellationToken);

            var items = models.Select(m => new AiModelItemDto
            {
                MaModel = m.MaModel,
                TenModel = m.TenModel,
                Version = m.Version,
                NgayTrain = m.NgayTrain,
                LatestAccuracy = latestEvalByModel.TryGetValue(m.MaModel, out var run) && run != null && run.Accuracy.HasValue ? (double?)run.Accuracy.Value : null
            }).ToList();

            return Ok(ApiResponse<List<AiModelItemDto>>.Ok(items));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<List<AiModelItemDto>>.Ok(new List<AiModelItemDto>(), $"Lỗi khi lấy danh sách model: {ex.Message}"));
        }
    }

    [HttpPost("models/train")]
    [Authorize(Policy = Permissions.AiSuggestResources)]
    public async Task<ActionResult<ApiResponse<object>>> TrainModel([FromBody] TrainModelRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var model = request.MaModel.HasValue
                ? await _dbContext.MoHinhAis.FirstOrDefaultAsync(x => x.MaModel == request.MaModel.Value, cancellationToken)
                : await _dbContext.MoHinhAis.OrderByDescending(x => x.MaModel).FirstOrDefaultAsync(cancellationToken);

            if (model == null)
            {
                // create a default model record
                model = new MoHinhAi { TenModel = request.TenModel ?? "Default Model", Version = "v1", NgayTrain = DateTime.UtcNow };
                _dbContext.MoHinhAis.Add(model);
            }
            else
            {
                model.NgayTrain = DateTime.UtcNow;
                // bump version using timestamp
                model.Version = $"v{DateTime.UtcNow:yyyyMMddHHmmss}";
                _dbContext.MoHinhAis.Update(model);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // write a log entry to LOG_AI if table exists
            try
            {
                await using var connection = _dbContext.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(cancellationToken);
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = @"INSERT INTO dbo.LOG_AI(MAMODEL, LOAI_SUKIEN, KET_QUA, THOI_GIAN, NOI_DUNG)
VALUES (@modelId, N'TRAIN', N'SUCCESS', SYSUTCDATETIME(), @content);";
                var modelIdParam = cmd.CreateParameter();
                modelIdParam.ParameterName = "@modelId";
                modelIdParam.Value = model.MaModel;
                cmd.Parameters.Add(modelIdParam);

                var contentParam = cmd.CreateParameter();
                contentParam.ParameterName = "@content";
                contentParam.Value = $"Đã train model từ giao diện bởi {User?.Identity?.Name ?? "anonymous"}";
                cmd.Parameters.Add(contentParam);

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch { /* ignore logging errors */ }

            return Ok(ApiResponse<object>.Ok(null, "Đã thực hiện train model (mô phỏng)."));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<object>.Ok(null, $"Lỗi khi train model: {ex.Message}"));
        }
    }

    [HttpGet("models/logs")]
    public async Task<ActionResult<ApiResponse<List<AiLogItemDto>>>> GetModelLogs([FromQuery] int top = 20, CancellationToken cancellationToken = default)
    {
        try
        {
            var safeTop = Math.Clamp(top, 1, 200);
            var rows = new List<AiLogItemDto>();
            await using var connection = _dbContext.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(cancellationToken);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = @"SELECT TOP (@top) MALOG, MAMODEL, LOAI_SUKIEN, KET_QUA, THOI_GIAN, NOI_DUNG FROM dbo.LOG_AI ORDER BY MALOG DESC;";
            var topParam = cmd.CreateParameter(); topParam.ParameterName = "@top"; topParam.Value = safeTop; cmd.Parameters.Add(topParam);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new AiLogItemDto
                {
                    MaLog = reader.IsDBNull(0) ? 0 : reader.GetInt64(0),
                    MaModel = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                    LoaiSuKien = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    KetQua = reader.IsDBNull(3) ? null : reader.GetString(3),
                    ThoiGian = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                    NoiDung = reader.IsDBNull(5) ? null : reader.GetString(5)
                });
            }

            return Ok(ApiResponse<List<AiLogItemDto>>.Ok(rows));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<List<AiLogItemDto>>.Ok(new List<AiLogItemDto>(), $"Lỗi khi lấy logs: {ex.Message}"));
        }
    }

    [HttpGet("models/perf")]
    public async Task<ActionResult<ApiResponse<List<AiModelPerfPointDto>>>> GetModelPerf([FromQuery] int maModel = 0, [FromQuery] int top = 12, CancellationToken cancellationToken = default)
    {
        try
        {
            var q = _dbContext.AiDanhGiaRuns.AsNoTracking().OrderByDescending(x => x.NgayDanhGia);
            if (maModel > 0) q = q.Where(x => x.MaModel == maModel).OrderByDescending(x => x.NgayDanhGia);

            var rows = await q.Take(Math.Clamp(top,1,60)).ToListAsync(cancellationToken);
            var points = rows.OrderBy(x => x.NgayDanhGia).Select(x => new AiModelPerfPointDto
            {
                Label = x.NgayDanhGia?.ToString("yyyy-MM-dd") ?? "",
                Accuracy = x.Accuracy is null ? null : (double?)x.Accuracy.Value
            }).ToList();

            return Ok(ApiResponse<List<AiModelPerfPointDto>>.Ok(points));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<List<AiModelPerfPointDto>>.Ok(new List<AiModelPerfPointDto>(), $"Lỗi khi lấy hiệu suất: {ex.Message}"));
        }
    }

    [HttpGet("feature-store")]
    [Authorize(Policy = Permissions.AiViewPerformance)]
    public async Task<ActionResult<ApiResponse<List<AiFeatureStoreItemDto>>>> GetFeatureStore(
        [FromQuery] int? maDuAn = null,
        [FromQuery] int? maNhanVien = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int top = 200,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var safeTop = Math.Clamp(top, 1, 1000);
            var query =
                from feature in _dbContext.AiFeatureStores.AsNoTracking()
                join model in _dbContext.MoHinhAis.AsNoTracking() on feature.MaModel equals model.MaModel into modelGroup
                from model in modelGroup.DefaultIfEmpty()
                join employee in _dbContext.NhanViens.AsNoTracking() on feature.MaNhanVien equals employee.MaNhanVien into employeeGroup
                from employee in employeeGroup.DefaultIfEmpty()
                join project in _dbContext.DuAns.AsNoTracking() on feature.MaDuAn equals project.MaDuAn into projectGroup
                from project in projectGroup.DefaultIfEmpty()
                join task in _dbContext.CongViecs.AsNoTracking() on feature.MaCongViec equals task.MaCongViec into taskGroup
                from task in taskGroup.DefaultIfEmpty()
                select new { feature, model, employee, project, task };

            if (maDuAn.HasValue)
            {
                query = query.Where(x => x.feature.MaDuAn == maDuAn.Value);
            }

            if (maNhanVien.HasValue)
            {
                query = query.Where(x => x.feature.MaNhanVien == maNhanVien.Value);
            }

            if (from.HasValue)
            {
                var fromDate = from.Value.Date;
                query = query.Where(x => x.feature.DongChot.HasValue && x.feature.DongChot.Value >= fromDate);
            }

            if (to.HasValue)
            {
                var toExclusive = to.Value.Date.AddDays(1);
                query = query.Where(x => x.feature.DongChot.HasValue && x.feature.DongChot.Value < toExclusive);
            }

            var items = await query
                .OrderByDescending(x => x.feature.DongChot)
                .ThenByDescending(x => x.feature.MaFeature)
                .Take(safeTop)
                .Select(x => new AiFeatureStoreItemDto
                {
                    MaFeature = x.feature.MaFeature,
                    MaModel = x.feature.MaModel,
                    TenModel = x.model != null ? x.model.TenModel : null,
                    MaNhanVien = x.feature.MaNhanVien,
                    HoTenNhanVien = x.employee != null ? x.employee.HoTen : null,
                    MaDuAn = x.feature.MaDuAn,
                    TenDuAn = x.project != null ? x.project.TenDuAn : null,
                    MaCongViec = x.feature.MaCongViec,
                    TenCongViec = x.task != null ? x.task.TenCongViec : null,
                    FeatureName = x.feature.FeatureName,
                    FeatureValue = x.feature.FeatureValue,
                    FeatureType = x.feature.FeatureType,
                    SourceTable = x.feature.SourceTable,
                    SourceKey = x.feature.SourceKey,
                    VersionTag = x.feature.VersionTag,
                    DongChot = x.feature.DongChot
                })
                .ToListAsync(cancellationToken);

            return Ok(ApiResponse<List<AiFeatureStoreItemDto>>.Ok(items));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<List<AiFeatureStoreItemDto>>.Ok(new List<AiFeatureStoreItemDto>(), $"Lỗi khi lấy dữ liệu feature store: {ex.Message}"));
        }
    }

    [HttpPost("predict-delay")]
    public async Task<ActionResult<ApiResponse<PredictDelayResultDto>>> PredictDelay([FromBody] PredictDelayRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<PredictDelayResultDto>.Fail("Dữ liệu dự báo không hợp lệ."));
            }

            var command = new PredictDelayCommand
            {
                MaCongViec = request.MaCongViec,
                MaNhanVien = request.MaNhanVien,
                DoKho = request.DoKho,
                DoUuTien = request.DoUuTien,
                SoNguoiThamGia = request.SoNguoiThamGia,
                TienDoHienTai = request.TienDoHienTai,
                SoNgayConLai = request.SoNgayConLai,
                TienDo = request.TienDo,
                EstimatedHours = request.EstimatedHours,
                SpentHours = request.SpentHours,
                CorrelationId = request.CorrelationId
            };

            var result = await _aiPredictionService.PredictDelayAsync(command, User?.Identity?.Name, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<PredictDelayResultDto>.Fail($"Không thể tạo dự báo AI: {ex.Message}"));
        }
    }

    [HttpPost("classify-performance")]
    public async Task<ActionResult<ApiResponse<ClassifyPerformanceResultDto>>> ClassifyPerformance([FromBody] ClassifyPerformanceRequest request, CancellationToken cancellationToken)
    {
        var command = new ClassifyPerformanceCommand
        {
            MaNhanVien = request.MaNhanVien,
            SoCongViecHoanThanh = request.SoCongViecHoanThanh,
            SoCongViecTreHan = request.SoCongViecTreHan,
            ThoiGianTrungBinh = request.ThoiGianTrungBinh,
            KpiTrungBinh = request.KpiTrungBinh,
            CorrelationId = request.CorrelationId
        };

        var result = await _aiPredictionService.ClassifyPerformanceAsync(command, User?.Identity?.Name, cancellationToken);
        if (!result.Success)
        {
            if (result.Message.Contains("Model", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, result);
            }

            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("suggest-employee")]
    public async Task<ActionResult<ApiResponse<List<SuggestEmployeeItem>>>> SuggestEmployee([FromBody] SuggestEmployeeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Skill))
        {
            return BadRequest(ApiResponse<List<SuggestEmployeeItem>>.Fail("Skill là bắt buộc."));
        }

        var top = Math.Clamp(request.SoLuongDeXuat ?? 5, 1, 20);
        var normalizedSkill = request.Skill.Trim();

        var latestKpiByEmployee = await _dbContext.KetQuaKpis
            .AsNoTracking()
            .Where(x => x.MaKpi == 1)
            .GroupBy(x => x.MaNhanVien)
            .Select(g => new
            {
                MaNhanVien = g.Key,
                Latest = g.OrderByDescending(x => x.nam).ThenByDescending(x => x.thang).Select(x => (double)(x.DiemSo ?? 0)).FirstOrDefault()
            })
            .ToDictionaryAsync(x => x.MaNhanVien, x => x.Latest);

        var activeTaskCountByEmployee = await (
            from pc in _dbContext.PhanCongNhanViens.AsNoTracking()
            join cv in _dbContext.CongViecs.AsNoTracking() on pc.MaCongViec equals cv.MaCongViec
            where cv.MaTrangThai != 3
            group pc by pc.MaNhanVien into g
            select new { MaNhanVien = g.Key, SoTaskDangLam = g.Select(x => x.MaCongViec).Distinct().Count() }
        ).ToDictionaryAsync(x => x.MaNhanVien, x => x.SoTaskDangLam);

        var candidates = await (
            from nv in _dbContext.NhanViens.AsNoTracking()
            where nv.TrangThai == 1
            join knv in _dbContext.KyNangNhanViens.AsNoTracking() on nv.MaNhanVien equals knv.MaNhanVien
            join kn in _dbContext.KyNangs.AsNoTracking() on knv.MaKyNang equals kn.MaKyNang
            where kn.TenKyNang != null && EF.Functions.Like(EF.Functions.Collate(kn.TenKyNang, "Latin1_General_CI_AI"), $"%{normalizedSkill}%")
            select new
            {
                nv.MaNhanVien,
                nv.HoTen,
                KyNang = kn.TenKyNang,
                CapDoSkill = knv.CapDo ?? 1
            })
            .ToListAsync();

        var result = candidates
            .GroupBy(x => new { x.MaNhanVien, x.HoTen })
            .Select(g =>
            {
                var bestSkill = g.OrderByDescending(x => x.CapDoSkill).FirstOrDefault();
                if (bestSkill == null)
                {
                    return null;
                }

                var kpi = latestKpiByEmployee.GetValueOrDefault(g.Key.MaNhanVien, 0d);
                var soTaskDangLam = activeTaskCountByEmployee.GetValueOrDefault(g.Key.MaNhanVien, 0);
                var score = (kpi * 0.5) + (bestSkill.CapDoSkill * 10) - (soTaskDangLam * 5);

                // Slightly reward matching high-difficulty task with high skill level.
                if (request.DoKho.HasValue && bestSkill.CapDoSkill >= request.DoKho.Value)
                {
                    score += 5;
                }

                return new SuggestEmployeeItem
                {
                    MaNhanVien = g.Key.MaNhanVien,
                    HoTen = g.Key.HoTen,
                    Skill = bestSkill.KyNang,
                    CapDoSkill = bestSkill.CapDoSkill,
                    Kpi = kpi,
                    SoTaskDangLam = soTaskDangLam,
                    DiemDeXuat = Math.Round(score, 2)
                };
            })
            .Where(x => x != null)
            .Select(x => x!)
            .OrderByDescending(x => x.DiemDeXuat)
            .ThenByDescending(x => x.Kpi)
            .Take(top)
            .ToList();

        return Ok(ApiResponse<List<SuggestEmployeeItem>>.Ok(result));
    }

    [HttpGet("history")]
    public async Task<ActionResult<ApiResponse<AiMonitoringSnapshotDto>>> GetHistory([FromQuery] int top = 8, CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(top, 1, 30);

        var predictions = new List<DuDoanAi>();
        try
        {
            predictions = await _dbContext.DuDoanAis
                .AsNoTracking()
                .Include(x => x.NhanVien)
                .Include(x => x.MoHinhAi)
                .OrderByDescending(x => x.ThoiGianDuDoan ?? DateTime.MinValue)
                .Take(take)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }

        var predictionIds = predictions.Select(x => x.MaDuDoan).ToList();
        var feedbackRows = new List<AiFeedback>();
        if (predictionIds.Count > 0)
        {
            try
            {
                feedbackRows = await _dbContext.AiFeedbacks
                    .AsNoTracking()
                    .Where(x => x.MaDuDoan.HasValue && predictionIds.Contains(x.MaDuDoan.Value))
                    .OrderByDescending(x => x.NgayPhanHoi)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
        }

        var feedbackByPrediction = feedbackRows
            .GroupBy(x => x.MaDuDoan!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var predictionDtos = predictions.Select(x =>
        {
            feedbackByPrediction.TryGetValue(x.MaDuDoan, out var feedbacks);
            var latestFeedback = feedbacks?.OrderByDescending(f => f.NgayPhanHoi).FirstOrDefault();

            return new AiPredictionHistoryItemDto
            {
                MaDuDoan = x.MaDuDoan,
                MaNhanVien = x.MaNhanVien,
                HoTenNhanVien = x.NhanVien?.HoTen,
                ModelName = x.ModelName ?? x.MoHinhAi?.TenModel,
                DiemDuDoan = x.DiemDuDoan is null ? null : (double?)x.DiemDuDoan,
                XacSuatTreHan = x.XacSuatTreHan is null ? null : (double?)x.XacSuatTreHan,
                RiskLevel = x.XacSuatTreHan.HasValue
                    ? (x.XacSuatTreHan.Value >= 0.75m ? "HIGH" : x.XacSuatTreHan.Value >= 0.45m ? "MEDIUM" : "LOW")
                    : null,
                DeXuatCaiThien = x.DeXuatCaiThien,
                GoiYNguonLuc = x.GoiYNguonLuc,
                ThoiGianDuDoan = x.ThoiGianDuDoan,
                FeedbackCount = feedbacks?.Count ?? 0,
                AvgDoChinhXac = feedbacks?.Where(f => f.DoChinhXac.HasValue).Select(f => (double?)f.DoChinhXac!.Value).Average(),
                AvgMucHuuIch = feedbacks?.Where(f => f.MucHuuIch.HasValue).Select(f => (double?)f.MucHuuIch!.Value).Average(),
                LatestFeedback = latestFeedback?.NoiDung,
                LatestFeedbackAt = latestFeedback?.NgayPhanHoi
            };
        }).ToList();

        var evaluationRows = new List<AiDanhGiaRun>();
        try
        {
            evaluationRows = await _dbContext.AiDanhGiaRuns
                .AsNoTracking()
                .Include(x => x.MoHinhAi)
                .OrderByDescending(x => x.NgayDanhGia ?? DateTime.MinValue)
                .Take(take)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
        }

        var evaluations = evaluationRows.Select(x => new AiEvaluationRunDto
        {
            MaDanhGia = x.MaDanhGia,
            MaModel = x.MaModel,
            TenModel = x.MoHinhAi?.TenModel,
            LoaiMoHinh = x.LoaiMoHinh,
            Version = x.MoHinhAi?.Version,
            NgayDanhGia = x.NgayDanhGia,
            TongBanGhi = x.TongBanGhi,
            TongDung = x.TongDung,
            TongSai = x.TongSai,
            Mae = x.Mae is null ? null : (double?)x.Mae,
            Rmse = x.Rmse is null ? null : (double?)x.Rmse,
            Accuracy = x.Accuracy is null ? null : (double?)x.Accuracy,
            PrecisionScore = x.PrecisionScore is null ? null : (double?)x.PrecisionScore,
            RecallScore = x.RecallScore is null ? null : (double?)x.RecallScore,
            F1Score = x.F1Score is null ? null : (double?)x.F1Score,
            GhiChu = x.GhiChu
        }).ToList();

        return Ok(ApiResponse<AiMonitoringSnapshotDto>.Ok(new AiMonitoringSnapshotDto
        {
            Predictions = predictionDtos,
            Evaluations = evaluations
        }));
    }

    [HttpPost("feedback")]
    public async Task<ActionResult<ApiResponse<AiFeedbackDto>>> SubmitFeedback([FromBody] AiFeedbackRequestDto request, CancellationToken cancellationToken)
    {
        if (!request.MaDuDoan.HasValue && !request.MaDanhGia.HasValue && !request.TaskId.HasValue && request.IsCorrect is null && request.DungSai is null)
        {
            return BadRequest(ApiResponse<AiFeedbackDto>.Fail("Cần cung cấp dữ liệu phản hồi AI hợp lệ."));
        }

        if (request.DoChinhXac.HasValue && (request.DoChinhXac < 1 || request.DoChinhXac > 5))
        {
            return BadRequest(ApiResponse<AiFeedbackDto>.Fail("DoChinhXac phai trong khoang 1 den 5."));
        }

        if (request.MucHuuIch.HasValue && (request.MucHuuIch < 1 || request.MucHuuIch > 5))
        {
            return BadRequest(ApiResponse<AiFeedbackDto>.Fail("MucHuuIch phai trong khoang 1 den 5."));
        }

        var prediction = request.MaDuDoan.HasValue
            ? await _dbContext.DuDoanAis.AsNoTracking().FirstOrDefaultAsync(x => x.MaDuDoan == request.MaDuDoan.Value, cancellationToken)
            : null;

        var derivedDungSai = request.DungSai
            ?? (request.IsCorrect.HasValue ? !request.IsCorrect.Value : null);

        var derivedNoiDung = !string.IsNullOrWhiteSpace(request.Note)
            ? request.Note.Trim()
            : (!string.IsNullOrWhiteSpace(request.WrongReason) ? request.WrongReason.Trim() : null);

        var derivedHanhDongDeXuat = !string.IsNullOrWhiteSpace(request.HanhDongDeXuat)
            ? request.HanhDongDeXuat.Trim()
            : (request.TaskId.HasValue
                ? $"forecast-task:{request.TaskId.Value}"
                : (string.Equals(request.Context, "forecast", StringComparison.OrdinalIgnoreCase) ? "forecast" : null));

        var feedback = new AiFeedback
        {
            MaDanhGia = request.MaDanhGia,
            MaDuDoan = request.MaDuDoan,
            MaNhanVien = request.MaNhanVien ?? prediction?.MaNhanVien,
            DoChinhXac = request.DoChinhXac,
            MucHuuIch = request.MucHuuIch,
            DungSai = derivedDungSai,
            NoiDung = derivedNoiDung ?? (request.TaskId.HasValue ? $"TaskId={request.TaskId.Value}" : null),
            HanhDongDeXuat = derivedHanhDongDeXuat,
            NgayPhanHoi = DateTime.UtcNow
        };

        _dbContext.AiFeedbacks.Add(feedback);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<AiFeedbackDto>.Ok(new AiFeedbackDto
        {
            MaFeedback = feedback.MaFeedback,
            MaDanhGia = feedback.MaDanhGia,
            MaDuDoan = feedback.MaDuDoan,
            MaNhanVien = feedback.MaNhanVien,
            DoChinhXac = feedback.DoChinhXac,
            MucHuuIch = feedback.MucHuuIch,
            DungSai = feedback.DungSai,
            NoiDung = feedback.NoiDung,
            HanhDongDeXuat = feedback.HanhDongDeXuat,
            NgayPhanHoi = feedback.NgayPhanHoi
        }, "Da ghi nhan phan hoi AI."));
    }

    [HttpGet("feedback/forecast")]
    [Authorize(Policy = Permissions.AiViewForecast)]
    public async Task<ActionResult<ApiResponse<List<AiForecastFeedbackItemDto>>>> GetForecastFeedback([FromQuery] string? taskIds = null, CancellationToken cancellationToken = default)
    {
        var parsedTaskIds = (taskIds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.TryParse(value, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (parsedTaskIds.Count == 0)
        {
            return Ok(ApiResponse<List<AiForecastFeedbackItemDto>>.Ok(new List<AiForecastFeedbackItemDto>()));
        }

        const string prefix = "forecast-task:";
        var feedbackRows = await _dbContext.AiFeedbacks
            .AsNoTracking()
            .Where(x => x.HanhDongDeXuat != null && x.HanhDongDeXuat.StartsWith(prefix))
            .OrderByDescending(x => x.NgayPhanHoi)
            .ThenByDescending(x => x.MaFeedback)
            .ToListAsync(cancellationToken);

        var feedbackDtos = feedbackRows
            .Select(x => new
            {
                Feedback = x,
                TaskId = TryParseForecastTaskId(x.HanhDongDeXuat)
            })
            .Where(x => x.TaskId.HasValue && parsedTaskIds.Contains(x.TaskId.Value))
            .GroupBy(x => x.TaskId!.Value)
            .Select(g => g.First())
            .Select(x => new AiForecastFeedbackItemDto
            {
                TaskId = x.TaskId!.Value,
                MaFeedback = x.Feedback.MaFeedback,
                IsCorrect = x.Feedback.DungSai.HasValue ? !x.Feedback.DungSai.Value : null,
                WrongReason = x.Feedback.DungSai == true ? x.Feedback.NoiDung : null,
                Note = x.Feedback.NoiDung,
                NgayPhanHoi = x.Feedback.NgayPhanHoi
            })
            .ToList();

        return Ok(ApiResponse<List<AiForecastFeedbackItemDto>>.Ok(feedbackDtos));
    }

    private static int? TryParseForecastTaskId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        const string prefix = "forecast-task:";
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rawId = value.Substring(prefix.Length).Trim();
        return int.TryParse(rawId, out var id) ? id : null;
    }

    [HttpGet("intervention-log")]
    [Authorize(Policy = Permissions.AiViewForecast)]
    public async Task<ActionResult<ApiResponse<List<AiInterventionLogDto>>>> GetInterventionLog(
        [FromQuery] int top = 100,
        [FromQuery] int? maDuDoan = null,
        [FromQuery] int? maDanhGia = null,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(top, 1, 500);
        var query = _dbContext.AiNhatKyCanThieps.AsNoTracking().AsQueryable();
        if (maDuDoan.HasValue) query = query.Where(x => x.MaDuDoan == maDuDoan.Value);
        if (maDanhGia.HasValue) query = query.Where(x => x.MaDanhGia == maDanhGia.Value);

        var rows = await query
            .OrderByDescending(x => x.NgayCanThiep)
            .ThenByDescending(x => x.MaCanThiep)
            .Take(take)
            .Select(x => new AiInterventionLogDto
            {
                MaCanThiep = x.MaCanThiep,
                MaDanhGia = x.MaDanhGia,
                MaDuDoan = x.MaDuDoan,
                MaNhanVien = x.MaNhanVien,
                NguoiCanThiep = x.NguoiCanThiep,
                ActionType = x.ActionType,
                ActionSource = x.ActionSource,
                Reason = x.Reason,
                OldValue = x.OldValue,
                NewValue = x.NewValue,
                NguonCanThiep = x.NguonCanThiep,
                NgayCanThiep = x.NgayCanThiep,
                SoLanChinhSua = x.SoLanChinhSua
            })
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<List<AiInterventionLogDto>>.Ok(rows));
    }

    [HttpGet("nhatky-can-thiep")]
    [Authorize(Policy = Permissions.AiViewForecast)]
    public Task<ActionResult<ApiResponse<List<AiInterventionLogDto>>>> GetInterventionLogVnAlias(
        [FromQuery] int top = 100,
        [FromQuery] int? maDuDoan = null,
        [FromQuery] int? maDanhGia = null,
        CancellationToken cancellationToken = default)
    {
        Response.Headers.Append("X-API-Alias", "Alias route; ưu tiên /ai/intervention-log");
        return GetInterventionLog(top, maDuDoan, maDanhGia, cancellationToken);
    }

    [HttpGet("hitl-actions")]
    [Authorize(Policy = Permissions.AiViewForecast)]
    public Task<ActionResult<ApiResponse<List<AiInterventionLogDto>>>> GetInterventionLogLegacyAlias(
        [FromQuery] int top = 100,
        [FromQuery] int? maDuDoan = null,
        [FromQuery] int? maDanhGia = null,
        CancellationToken cancellationToken = default)
    {
        Response.Headers.Append("Deprecation", "true");
        Response.Headers.Append("Sunset", "2026-12-31");
        Response.Headers.Append("Link", "</ai/intervention-log>; rel=\"successor-version\"");
        return GetInterventionLog(top, maDuDoan, maDanhGia, cancellationToken);
    }

    [HttpPost("intervention-log")]
    [Authorize(Policy = Permissions.AiSuggestResources)]
    public async Task<ActionResult<ApiResponse<AiInterventionLogDto>>> CreateInterventionLog(
        [FromBody] AiInterventionLogRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!request.MaDuDoan.HasValue && !request.MaDanhGia.HasValue)
        {
            return BadRequest(ApiResponse<AiInterventionLogDto>.Fail("Cần chỉ định MaDuDoan hoặc MaDanhGia."));
        }

        var actorUserId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var actorEmployeeId = await _dbContext.NhanViens.AsNoTracking()
            .Where(x => x.AspNetUserId == actorUserId)
            .Select(x => (int?)x.MaNhanVien)
            .FirstOrDefaultAsync(cancellationToken);

        var entity = new AiNhatKyCanThiep
        {
            MaDanhGia = request.MaDanhGia,
            MaDuDoan = request.MaDuDoan,
            MaNhanVien = request.MaNhanVien,
            NguoiCanThiep = request.NguoiCanThiep ?? actorEmployeeId,
            ActionType = string.IsNullOrWhiteSpace(request.ActionType) ? "Review" : request.ActionType.Trim(),
            ActionSource = string.IsNullOrWhiteSpace(request.ActionSource) ? "Manual" : request.ActionSource.Trim(),
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            OldValue = string.IsNullOrWhiteSpace(request.OldValue) ? null : request.OldValue.Trim(),
            NewValue = string.IsNullOrWhiteSpace(request.NewValue) ? null : request.NewValue.Trim(),
            NguonCanThiep = string.IsNullOrWhiteSpace(request.NguonCanThiep) ? "manager" : request.NguonCanThiep.Trim(),
            NgayCanThiep = DateTime.UtcNow,
            SoLanChinhSua = 0
        };

        _dbContext.AiNhatKyCanThieps.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<AiInterventionLogDto>.Ok(new AiInterventionLogDto
        {
            MaCanThiep = entity.MaCanThiep,
            MaDanhGia = entity.MaDanhGia,
            MaDuDoan = entity.MaDuDoan,
            MaNhanVien = entity.MaNhanVien,
            NguoiCanThiep = entity.NguoiCanThiep,
            ActionType = entity.ActionType,
            ActionSource = entity.ActionSource,
            Reason = entity.Reason,
            OldValue = entity.OldValue,
            NewValue = entity.NewValue,
            NguonCanThiep = entity.NguonCanThiep,
            NgayCanThiep = entity.NgayCanThiep,
            SoLanChinhSua = entity.SoLanChinhSua
        }, "Đã ghi nhật ký can thiệp AI."));
    }

    [HttpPost("nhatky-can-thiep")]
    [Authorize(Policy = Permissions.AiSuggestResources)]
    public Task<ActionResult<ApiResponse<AiInterventionLogDto>>> CreateInterventionLogVnAlias(
        [FromBody] AiInterventionLogRequestDto request,
        CancellationToken cancellationToken = default)
    {
        Response.Headers.Append("X-API-Alias", "Alias route; ưu tiên /ai/intervention-log");
        return CreateInterventionLog(request, cancellationToken);
    }

    [HttpPost("hitl-actions")]
    [Authorize(Policy = Permissions.AiSuggestResources)]
    public Task<ActionResult<ApiResponse<AiInterventionLogDto>>> CreateInterventionLogLegacyAlias(
        [FromBody] AiInterventionLogRequestDto request,
        CancellationToken cancellationToken = default)
    {
        Response.Headers.Append("Deprecation", "true");
        Response.Headers.Append("Sunset", "2026-12-31");
        Response.Headers.Append("Link", "</ai/intervention-log>; rel=\"successor-version\"");
        return CreateInterventionLog(request, cancellationToken);
    }

    public class PredictDelayRequest
    {
        public int? MaCongViec { get; set; }
        public int? MaNhanVien { get; set; }
        public int? DoKho { get; set; }
        public int? DoUuTien { get; set; }
        public int? SoNguoiThamGia { get; set; }
        public double? TienDoHienTai { get; set; }
        public int? SoNgayConLai { get; set; }
        public double? EstimatedHours { get; set; }
        public double? SpentHours { get; set; }
        public string? CorrelationId { get; set; }

        // Backward-compatible payload field from previous API contract.
        public double? TienDo { get; set; }
    }

    public class ClassifyPerformanceRequest
    {
        public int? MaNhanVien { get; set; }
        public int? SoCongViecHoanThanh { get; set; }
        public int? SoCongViecTreHan { get; set; }
        public double? ThoiGianTrungBinh { get; set; }
        public double? KpiTrungBinh { get; set; }
        public string? CorrelationId { get; set; }
    }

    public class SuggestEmployeeRequest
    {
        public string Skill { get; set; } = string.Empty;
        public int? DoKho { get; set; }
        public int? SoLuongDeXuat { get; set; }
    }

    public class SuggestEmployeeItem
    {
        public int MaNhanVien { get; set; }
        public string? HoTen { get; set; }
        public string? Skill { get; set; }
        public int CapDoSkill { get; set; }
        public double Kpi { get; set; }
        public int SoTaskDangLam { get; set; }
        public double DiemDeXuat { get; set; }
    }

    public class AiModelItemDto
    {
        public int MaModel { get; set; }
        public string? TenModel { get; set; }
        public string? Version { get; set; }
        public DateTime? NgayTrain { get; set; }
        public double? LatestAccuracy { get; set; }
    }

    public class TrainModelRequest
    {
        public int? MaModel { get; set; }
        public string? TenModel { get; set; }
    }

    public class AiLogItemDto
    {
        public long MaLog { get; set; }
        public int MaModel { get; set; }
        public string LoaiSuKien { get; set; } = string.Empty;
        public string? KetQua { get; set; }
        public DateTime? ThoiGian { get; set; }
        public string? NoiDung { get; set; }
    }

    public class AiModelPerfPointDto
    {
        public string Label { get; set; } = string.Empty;
        public double? Accuracy { get; set; }
    }
}




