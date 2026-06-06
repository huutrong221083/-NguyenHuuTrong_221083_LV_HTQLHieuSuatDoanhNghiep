using LuanVan.Data;
using LuanVan.Contracts;
using LuanVan.Models;
using LuanVan.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;

namespace LuanVan.Controllers.Api;

[ApiController]
[Route("ai")]
[Authorize]
public class AiController : ControllerBase
{
    private const int TaskStatusCompleted = 3;
    private const string SourceAiHistory = "AI_HISTORY";
    private const string SourceRuleFast = "RULE_FAST";
    private const string SourceInsufficientData = "INSUFFICIENT_DATA";
    private static int _performanceSummaryCacheVersion = 1;
    private static readonly TimeSpan PerformanceSummaryCacheDuration = TimeSpan.FromMinutes(10);
    private readonly AppDbContext _dbContext;
    private readonly IAiPredictionService _aiPredictionService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AiController> _logger;

    public AiController(
        AppDbContext dbContext,
        IAiPredictionService aiPredictionService,
        IMemoryCache cache,
        ILogger<AiController> logger)
    {
        _dbContext = dbContext;
        _aiPredictionService = aiPredictionService;
        _cache = cache;
        _logger = logger;
    }

    private static void BumpPerformanceSummaryCacheVersion()
        => Interlocked.Increment(ref _performanceSummaryCacheVersion);

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
            BumpPerformanceSummaryCacheVersion();

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
                return Ok(ApiResponse<PredictDelayResultDto>.Fail("Dữ liệu dự báo không hợp lệ."));
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
                return Ok(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<PredictDelayResultDto>.Fail($"Không thể tạo dự báo AI: {ex.Message}"));
        }
    }

    [HttpGet("training-data")]
    [Authorize(Policy = Permissions.AiViewPerformance)]
    public async Task<ActionResult<ApiResponse<AiTrainingDataResponseDto>>> GetTrainingData(
        [FromQuery] string model = "all",
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int? employeeId = null,
        [FromQuery] int? projectId = null,
        [FromQuery] string? keyword = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var safePage = Math.Max(1, page);
            var safePageSize = Math.Clamp(pageSize, 1, 200);
            var normalizedModel = (model ?? "all").Trim().ToLowerInvariant();
            var includeTaskDelay = normalizedModel is "all" or "task-delay";
            var includePerformance = normalizedModel is "all" or "performance";

            if (!includeTaskDelay && !includePerformance)
            {
                return BadRequest(ApiResponse<AiTrainingDataResponseDto>.Fail("model phải là task-delay, performance hoặc all."));
            }

            var response = new AiTrainingDataResponseDto
            {
                GeneratedAt = DateTime.UtcNow
            };

            if (includeTaskDelay)
            {
            var taskDelayRowsQuery =
                from pc in _dbContext.PhanCongNhanViens.AsNoTracking()
                join cv in _dbContext.CongViecs.AsNoTracking() on pc.MaCongViec equals cv.MaCongViec
                join nv in _dbContext.NhanViens.AsNoTracking() on pc.MaNhanVien equals nv.MaNhanVien into nvGroup
                from nv in nvGroup.DefaultIfEmpty()
                where cv.HanHoanThanh.HasValue && pc.NgayBatDauThucTe.HasValue
                select new
                {
                    pc.MaPhaCong,
                    pc.MaNhanVien,
                    HoTenNhanVien = nv != null ? nv.HoTen : null,
                    pc.MaCongViec,
                    cv.TenCongViec,
                    cv.MaDuAn,
                    TenDuAn = cv.DuAn != null ? cv.DuAn.TenDuAn : null,
                    pc.NgayBatDauDuKien,
                    pc.NgayKetThucdukien,
                    pc.NgayBatDauThucTe,
                    pc.NgayKetThucThucTe,
                    cv.HanHoanThanh,
                    pc.PhanTramHoanThanh,
                    cv.MaDoUuTien,
                    PriorityWeight = cv.DoUuTien != null ? cv.DoUuTien.HeSo : (decimal?)null,
                    cv.MaDoKho,
                    DifficultyWeight = cv.DoKho != null ? cv.DoKho.HeSo : (decimal?)null
                };

            if (employeeId.HasValue)
            {
                taskDelayRowsQuery = taskDelayRowsQuery.Where(x => x.MaNhanVien == employeeId.Value);
            }

            if (projectId.HasValue)
            {
                taskDelayRowsQuery = taskDelayRowsQuery.Where(x => x.MaDuAn == projectId.Value);
            }

            if (fromDate.HasValue)
            {
                var from = fromDate.Value.Date;
                taskDelayRowsQuery = taskDelayRowsQuery.Where(x => x.NgayBatDauThucTe.HasValue && x.NgayBatDauThucTe.Value.Date >= from);
            }

            if (toDate.HasValue)
            {
                var to = toDate.Value.Date.AddDays(1);
                taskDelayRowsQuery = taskDelayRowsQuery.Where(x => x.NgayBatDauThucTe.HasValue && x.NgayBatDauThucTe.Value < to);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim();
                taskDelayRowsQuery = taskDelayRowsQuery.Where(x =>
                    (x.HoTenNhanVien != null && EF.Functions.Like(x.HoTenNhanVien, $"%{kw}%")) ||
                    (x.TenCongViec != null && EF.Functions.Like(x.TenCongViec, $"%{kw}%")) ||
                    (x.TenDuAn != null && EF.Functions.Like(x.TenDuAn, $"%{kw}%")));
            }

            var latestProgressMap = await _dbContext.TienDoCongViecs
                .AsNoTracking()
                .GroupBy(x => x.MaCongViec)
                .Select(g => new
                {
                    MaCongViec = g.Key,
                    Progress = g.OrderByDescending(x => x.NgayCapNhat).Select(x => (double?)x.PhanTramHoanThanh).FirstOrDefault()
                })
                .ToDictionaryAsync(x => x.MaCongViec, x => x.Progress ?? 0d, cancellationToken);

            var now = DateTime.UtcNow.Date;
            var taskDelayRowsRaw = await taskDelayRowsQuery
                .OrderByDescending(x => x.MaPhaCong)
                .ToListAsync(cancellationToken);

            var taskDelayRows = taskDelayRowsRaw.Select(x =>
            {
                var startDate = x.NgayBatDauThucTe!.Value.Date;
                var dueDate = x.HanHoanThanh!.Value.Date;
                var finishedDate = x.NgayKetThucThucTe?.Date ?? now;
                var estimatedHours = Math.Max(1d, (dueDate - startDate).TotalHours);
                var spentHours = Math.Max(0d, (finishedDate - startDate).TotalHours);
                var lateDays = Math.Max(0d, (finishedDate - dueDate).TotalDays);
                var progressPercent = latestProgressMap.GetValueOrDefault(x.MaCongViec, (double)(x.PhanTramHoanThanh ?? 0m));
                var priorityWeight = (double)(x.PriorityWeight ?? x.MaDoUuTien ?? 1);
                var difficultyWeight = (double)(x.DifficultyWeight ?? x.MaDoKho ?? 1);
                var daysUntilDeadline = Math.Max(-30d, (dueDate - now).TotalDays);
                var isLate = lateDays > 0d;

                return new AiTrainingRowDto
                {
                    RowType = "task-delay",
                    Values = new Dictionary<string, object?>
                    {
                        ["assignmentId"] = x.MaPhaCong,
                        ["employeeId"] = x.MaNhanVien,
                        ["employeeName"] = x.HoTenNhanVien,
                        ["taskId"] = x.MaCongViec,
                        ["taskName"] = x.TenCongViec,
                        ["projectId"] = x.MaDuAn,
                        ["projectName"] = x.TenDuAn,
                        ["plannedStartDate"] = x.NgayBatDauDuKien,
                        ["plannedEndDate"] = x.NgayKetThucdukien,
                        ["actualStartDate"] = x.NgayBatDauThucTe,
                        ["actualEndDate"] = x.NgayKetThucThucTe,
                        ["deadline"] = x.HanHoanThanh,
                        ["estimatedHours"] = Math.Round(estimatedHours, 2),
                        ["spentHours"] = Math.Round(spentHours, 2),
                        ["progressPercent"] = Math.Round(progressPercent, 2),
                        ["priorityWeight"] = priorityWeight,
                        ["difficultyWeight"] = difficultyWeight,
                        ["daysUntilDeadline"] = Math.Round(daysUntilDeadline, 2),
                        ["lateDays"] = Math.Round(lateDays, 2),
                        ["lateLabel"] = isLate ? "LATE" : "ON_TIME"
                    }
                };
            }).ToList();

            var taskDelayPaged = taskDelayRows
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize)
                .ToList();

            response.TaskDelay = new AiTrainingDatasetDto
            {
                DatasetKey = "task-delay",
                ModelName = "Task Delay Prediction",
                Algorithm = "Linear Regression",
                SourceTables = new List<string> { "PHANCONGNHANVIEN", "CONGVIEC", "TIENDOCONGVIEC" },
                Features = new List<string> { "estimated_hours", "spent_hours", "progress_percent", "priority_weight", "difficulty_weight", "days_until_deadline" },
                Target = "late_days",
                TotalRows = taskDelayRows.Count,
                DataQuality = BuildDataQuality(taskDelayRows.Count, 100),
                Rows = taskDelayPaged
            };
            }

            if (includePerformance)
            {
            var perfBaseQuery =
                from dl in _dbContext.DuLieuAis.AsNoTracking()
                join nv in _dbContext.NhanViens.AsNoTracking() on dl.MaNhanVien equals nv.MaNhanVien into nvGroup
                from nv in nvGroup.DefaultIfEmpty()
                where dl.SoCongViecHoanThanh.HasValue && dl.SoCongViecTreHan.HasValue && dl.ThoiGianTrungBinh.HasValue && dl.KpiTrungBinh.HasValue
                select new
                {
                    dl.MaDuLieu,
                    dl.MaNhanVien,
                    HoTenNhanVien = nv != null ? nv.HoTen : null,
                    Completed = dl.SoCongViecHoanThanh!.Value,
                    Late = dl.SoCongViecTreHan!.Value,
                    AvgTime = (double)dl.ThoiGianTrungBinh!.Value,
                    KpiAvg = (double)dl.KpiTrungBinh!.Value
                };

            if (employeeId.HasValue)
            {
                perfBaseQuery = perfBaseQuery.Where(x => x.MaNhanVien == employeeId.Value);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim();
                perfBaseQuery = perfBaseQuery.Where(x => x.HoTenNhanVien != null && EF.Functions.Like(x.HoTenNhanVien, $"%{kw}%"));
            }

            var perfRowsRaw = await perfBaseQuery
                .OrderByDescending(x => x.MaDuLieu)
                .ToListAsync(cancellationToken);

            var employeeIds = perfRowsRaw.Select(x => x.MaNhanVien).Distinct().ToList();
            var kpiTongRows = await _dbContext.KetQuaKpiTongs
                .AsNoTracking()
                .Where(x => employeeIds.Contains(x.MaNhanVien))
                .OrderByDescending(x => x.Nam)
                .ThenByDescending(x => x.Thang)
                .ToListAsync(cancellationToken);

            var kpiTongMap = kpiTongRows
                .GroupBy(x => x.MaNhanVien)
                .Select(g => g.First())
                .ToDictionary(
                    x => x.MaNhanVien,
                    x => new { x.Thang, x.Nam, x.XepLoai, DiemTong = (double)x.DiemTong, x.NgayTinh });

            var projectCountMap = await (
                from pc in _dbContext.PhanCongNhanViens.AsNoTracking()
                join cv in _dbContext.CongViecs.AsNoTracking() on pc.MaCongViec equals cv.MaCongViec
                where employeeIds.Contains(pc.MaNhanVien)
                group cv by pc.MaNhanVien into g
                select new
                {
                    MaNhanVien = g.Key,
                    ProjectCount = g.Select(x => x.MaDuAn).Distinct().Count()
                })
                .ToDictionaryAsync(x => x.MaNhanVien, x => x.ProjectCount, cancellationToken);

            var perfRows = perfRowsRaw.Select(x =>
            {
                var totalTasks = Math.Max(1, x.Completed + x.Late);
                var completionRate = x.Completed / (double)totalTasks;
                var lateRate = x.Late / (double)totalTasks;
                var projectCount = projectCountMap.GetValueOrDefault(x.MaNhanVien, 0);
                var score = x.KpiAvg - (lateRate * 30d);
                var fallbackLabel = score >= 85 ? "EXCELLENT"
                    : score >= 70 ? "GOOD"
                    : score >= 50 ? "NORMAL"
                    : "LOW";

                var hasKpiTong = kpiTongMap.TryGetValue(x.MaNhanVien, out var kpiTong) && !string.IsNullOrWhiteSpace(kpiTong?.XepLoai);
                var label = hasKpiTong ? NormalizePerformanceLabel(kpiTong!.XepLoai!) : fallbackLabel;
                var labelSource = hasKpiTong ? "KETQUAKPI_TONG" : "RULE_FALLBACK";
                var month = hasKpiTong ? kpiTong!.Thang : DateTime.UtcNow.Month;
                var year = hasKpiTong ? kpiTong!.Nam : DateTime.UtcNow.Year;
                double? totalKpiScore = hasKpiTong ? kpiTong!.DiemTong : null;

                return new AiTrainingRowDto
                {
                    RowType = "performance",
                    Values = new Dictionary<string, object?>
                    {
                        ["employeeId"] = x.MaNhanVien,
                        ["employeeName"] = x.HoTenNhanVien,
                        ["month"] = month,
                        ["year"] = year,
                        ["taskCount"] = totalTasks,
                        ["completedTaskCount"] = x.Completed,
                        ["lateTaskCount"] = x.Late,
                        ["completionRate"] = Math.Round(completionRate, 4),
                        ["lateRate"] = Math.Round(lateRate, 4),
                        ["avgWorkHours"] = Math.Round(x.AvgTime, 2),
                        ["kpiAverage"] = Math.Round(x.KpiAvg, 2),
                        ["totalKpiScore"] = totalKpiScore is null ? null : Math.Round(totalKpiScore.Value, 2),
                        ["projectCount"] = projectCount,
                        ["avgProgress"] = Math.Max(0d, Math.Min(100d, x.KpiAvg)),
                        ["label"] = label,
                        ["labelSource"] = labelSource
                    }
                };
            }).ToList();

            if (fromDate.HasValue || toDate.HasValue)
            {
                var fromBoundary = fromDate?.Date;
                var toBoundary = toDate?.Date;
                perfRows = perfRows.Where(x =>
                {
                    var rowYear = Convert.ToInt32(x.Values.GetValueOrDefault("year") ?? DateTime.UtcNow.Year);
                    var rowMonth = Convert.ToInt32(x.Values.GetValueOrDefault("month") ?? DateTime.UtcNow.Month);
                    var rowDate = new DateTime(rowYear, Math.Clamp(rowMonth, 1, 12), 1);
                    if (fromBoundary.HasValue && rowDate < new DateTime(fromBoundary.Value.Year, fromBoundary.Value.Month, 1))
                    {
                        return false;
                    }

                    if (toBoundary.HasValue && rowDate > new DateTime(toBoundary.Value.Year, toBoundary.Value.Month, 1))
                    {
                        return false;
                    }

                    return true;
                }).ToList();
            }

            var perfPaged = perfRows
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize)
                .ToList();

            response.Performance = new AiTrainingDatasetDto
            {
                DatasetKey = "performance",
                ModelName = "Performance Classification",
                Algorithm = "Random Forest",
                SourceTables = new List<string> { "DULIEUAI", "KETQUAKPI_TONG", "PHANCONGNHANVIEN", "CONGVIEC" },
                Features = new List<string> { "kpi_average", "completion_rate", "late_task_count", "avg_progress", "task_count", "project_count" },
                Target = "performance_label",
                TotalRows = perfRows.Count(),
                DataQuality = BuildDataQuality(perfRows.Count(), 100),
                Rows = perfPaged
            };
            }

            return Ok(ApiResponse<AiTrainingDataResponseDto>.Ok(response));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<AiTrainingDataResponseDto>.Fail($"Không thể tải dữ liệu huấn luyện AI: {ex.Message}", "TRAINING_DATA_ERROR"));
        }
    }

    [HttpGet("performance-summary")]
    [Authorize(Policy = Permissions.AiViewPerformance)]
    public async Task<ActionResult<ApiResponse<AiPerformanceSummaryResponseDto>>> GetPerformanceSummary(
        [FromQuery] int? thang = null,
        [FromQuery] int? nam = null,
        [FromQuery] int? maPhongBan = null,
        [FromQuery] int? maNhanVien = null,
        [FromQuery] int? top = 5,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var roleKey = User.IsInRole(Roles.Admin) ? Roles.Admin : (User.IsInRole(Roles.Manager) ? Roles.Manager : Roles.Employee);
        var month = thang is >= 1 and <= 12 ? thang.Value : DateTime.Now.Month;
        var year = nam is > 0 ? nam.Value : DateTime.Now.Year;
        var safeTop = Math.Clamp(top ?? 5, 1, 20);
        var selectedDepartmentId = maPhongBan is > 0 ? maPhongBan.Value : (int?)null;
        var selectedEmployeeId = maNhanVien is > 0 ? maNhanVien.Value : (int?)null;
        var cacheVersion = Volatile.Read(ref _performanceSummaryCacheVersion);
        var cacheKey = $"ai:performance-summary:v{cacheVersion}:user:{userId}:role:{roleKey}:m:{month}:y:{year}:pb:{selectedDepartmentId?.ToString() ?? "all"}:nv:{selectedEmployeeId?.ToString() ?? "all"}:top:{safeTop}";

        if (_cache.TryGetValue(cacheKey, out AiPerformanceSummaryResponseDto? cached) && cached != null)
        {
            cached.CacheHit = true;
            _logger.LogInformation(
                "AI performance-summary completed in {ElapsedMs}ms | employees={EmployeeCount} | cacheHit={CacheHit}",
                stopwatch.ElapsedMilliseconds,
                cached.Summary.TotalEmployees,
                true);
            return Ok(ApiResponse<AiPerformanceSummaryResponseDto>.Ok(cached));
        }

        var actor = string.IsNullOrWhiteSpace(userId)
            ? null
            : await _dbContext.NhanViens.AsNoTracking()
                .Where(x => x.AspNetUserId == userId)
                .Select(x => new { x.MaNhanVien, x.MaPhongBan })
                .FirstOrDefaultAsync(cancellationToken);

        if (actor == null)
        {
            return Forbid();
        }

        var employeeScopeIds = await GetAiPerformanceEmployeeScopeIdsAsync(actor.MaNhanVien, roleKey, cancellationToken);
        if (selectedEmployeeId.HasValue)
        {
            if (!employeeScopeIds.Contains(selectedEmployeeId.Value))
            {
                return Forbid();
            }

            employeeScopeIds = new List<int> { selectedEmployeeId.Value };
        }

        var employeesQuery = _dbContext.NhanViens.AsNoTracking()
            .Where(x => x.TrangThai == 1 && employeeScopeIds.Contains(x.MaNhanVien));

        if (selectedDepartmentId.HasValue)
        {
            employeesQuery = employeesQuery.Where(x => x.MaPhongBan == selectedDepartmentId.Value);
        }

        var employees = await employeesQuery
            .Select(x => new
            {
                x.MaNhanVien,
                x.HoTen,
                x.MaPhongBan,
                TenPhongBan = x.PhongBanQuanLy != null ? x.PhongBanQuanLy.TenPhongBan : null
            })
            .OrderBy(x => x.HoTen)
            .ToListAsync(cancellationToken);

        var employeeIds = employees.Select(x => x.MaNhanVien).ToList();

        var kpiTotalRows = employeeIds.Count == 0
            ? new List<KpiScoreRaw>()
            : await _dbContext.KetQuaKpiTongs.AsNoTracking()
                .Where(x => x.Thang == month && x.Nam == year && employeeIds.Contains(x.MaNhanVien))
                .Select(x => new KpiScoreRaw { MaNhanVien = x.MaNhanVien, Score = x.DiemTong })
                .ToListAsync(cancellationToken);

        var kpiByEmployee = kpiTotalRows
            .GroupBy(x => x.MaNhanVien)
            .ToDictionary(x => x.Key, x => (decimal?)x.Average(v => v.Score));

        var missingKpiEmployeeIds = employeeIds.Where(x => !kpiByEmployee.ContainsKey(x)).ToList();
        if (missingKpiEmployeeIds.Count > 0)
        {
            var fallbackRows = await _dbContext.KetQuaKpis.AsNoTracking()
                .Where(x => x.thang == month
                    && x.nam == year
                    && x.DiemSo.HasValue
                    && missingKpiEmployeeIds.Contains(x.MaNhanVien))
                .GroupBy(x => x.MaNhanVien)
                .Select(g => new { MaNhanVien = g.Key, Score = g.Average(x => x.DiemSo ?? 0m) })
                .ToListAsync(cancellationToken);

            foreach (var row in fallbackRows)
            {
                kpiByEmployee[row.MaNhanVien] = row.Score;
            }
        }

        var today = DateTime.Now.Date;
        var taskRows = employeeIds.Count == 0
            ? new List<EmployeeTaskStatsRaw>()
            : await (
                from pc in _dbContext.PhanCongNhanViens.AsNoTracking()
                join cv in _dbContext.CongViecs.AsNoTracking() on pc.MaCongViec equals cv.MaCongViec
                where employeeIds.Contains(pc.MaNhanVien)
                    && (pc.TrangThai ?? 1) == 1
                    && (cv.DaXoa ?? false) == false
                group cv by pc.MaNhanVien into g
                select new EmployeeTaskStatsRaw
                {
                    MaNhanVien = g.Key,
                    TotalTasks = g.Select(x => x.MaCongViec).Distinct().Count(),
                    CompletedTasks = g.Where(x => x.MaTrangThai == TaskStatusCompleted).Select(x => x.MaCongViec).Distinct().Count(),
                    LateTasks = g.Where(x => x.MaTrangThai != TaskStatusCompleted && x.HanHoanThanh.HasValue && x.HanHoanThanh.Value.Date < today).Select(x => x.MaCongViec).Distinct().Count()
                })
                .ToListAsync(cancellationToken);

        var taskStatsByEmployee = taskRows.ToDictionary(x => x.MaNhanVien);

        var historyRows = employeeIds.Count == 0
            ? new List<DuDoanAi>()
            : await _dbContext.DuDoanAis.AsNoTracking()
                .Where(x => x.thang == month
                    && x.nam == year
                    && employeeIds.Contains(x.MaNhanVien)
                    && x.ModelName != null
                    && x.ModelName.Contains("performance"))
                .OrderByDescending(x => x.ThoiGianDuDoan)
                .ToListAsync(cancellationToken);

        var latestHistoryByEmployee = historyRows
            .GroupBy(x => x.MaNhanVien)
            .ToDictionary(x => x.Key, x => x.First());

        var rows = employees.Select(employee =>
        {
            kpiByEmployee.TryGetValue(employee.MaNhanVien, out var kpi);
            taskStatsByEmployee.TryGetValue(employee.MaNhanVien, out var taskStats);
            latestHistoryByEmployee.TryGetValue(employee.MaNhanVien, out var history);

            var totalTasks = taskStats?.TotalTasks ?? 0;
            var completedTasks = taskStats?.CompletedTasks ?? 0;
            var lateTasks = taskStats?.LateTasks ?? 0;
            var lateRate = totalTasks == 0 ? 0m : Math.Round((decimal)lateTasks / totalTasks, 4);
            var classification = ResolveFastPerformanceClassification(kpi, totalTasks, lateRate, history);

            return new AiPerformanceEmployeeSummaryDto
            {
                MaNhanVien = employee.MaNhanVien,
                HoTen = employee.HoTen,
                MaPhongBan = employee.MaPhongBan,
                TenPhongBan = employee.TenPhongBan,
                Kpi = kpi.HasValue ? Math.Round(kpi.Value, 2) : null,
                TotalTasks = totalTasks,
                CompletedTasks = completedTasks,
                LateTasks = lateTasks,
                LateRate = lateRate,
                PerformanceLabel = classification.Label,
                Confidence = classification.Confidence,
                Source = classification.Source
            };
        }).ToList();

        var topPerformers = rows
            .Where(x => x.PerformanceLabel is "Xuất sắc" or "Tốt")
            .OrderByDescending(x => x.Kpi ?? 0m)
            .ThenBy(x => x.LateRate)
            .Take(safeTop)
            .ToList();

        var needSupport = rows
            .Where(x => x.PerformanceLabel is "Cần hỗ trợ" or "Cần cải thiện")
            .OrderByDescending(x => x.PerformanceLabel == "Cần hỗ trợ")
            .ThenByDescending(x => x.LateTasks)
            .ThenBy(x => x.Kpi ?? 999m)
            .Take(safeTop)
            .ToList();

        var labelDistribution = new[] { "Xuất sắc", "Tốt", "Cần cải thiện", "Cần hỗ trợ", "Chưa đủ dữ liệu" }
            .Select(label => new AiPerformanceLabelDistributionDto
            {
                Label = label,
                Count = rows.Count(x => x.PerformanceLabel == label)
            })
            .ToList();

        var averageKpi = rows.Where(x => x.Kpi.HasValue).Select(x => x.Kpi!.Value).DefaultIfEmpty(0m).Average();
        var totalTaskCount = rows.Sum(x => x.TotalTasks);
        var totalLateTasks = rows.Sum(x => x.LateTasks);
        var summary = new AiPerformanceSummaryDto
        {
            TotalEmployees = rows.Count,
            ExcellentCount = rows.Count(x => x.PerformanceLabel == "Xuất sắc"),
            GoodCount = rows.Count(x => x.PerformanceLabel == "Tốt"),
            NeedImproveCount = rows.Count(x => x.PerformanceLabel == "Cần cải thiện"),
            NeedSupportCount = rows.Count(x => x.PerformanceLabel == "Cần hỗ trợ"),
            InsufficientDataCount = rows.Count(x => x.PerformanceLabel == "Chưa đủ dữ liệu"),
            AverageKpi = Math.Round(averageKpi, 2),
            LateTaskRate = totalTaskCount == 0 ? 0m : Math.Round((decimal)totalLateTasks / totalTaskCount, 4)
        };

        var response = new AiPerformanceSummaryResponseDto
        {
            Period = new AiPerformancePeriodDto { Thang = month, Nam = year },
            GeneratedAt = DateTime.UtcNow,
            CacheHit = false,
            Summary = summary,
            LabelDistribution = labelDistribution,
            Employees = rows,
            TopPerformers = topPerformers,
            NeedSupport = needSupport,
            Insights = BuildPerformanceSummaryInsights(summary, rows)
        };

        _cache.Set(cacheKey, response, PerformanceSummaryCacheDuration);

        _logger.LogInformation(
            "AI performance-summary completed in {ElapsedMs}ms | employees={EmployeeCount} | cacheHit={CacheHit}",
            stopwatch.ElapsedMilliseconds,
            rows.Count,
            false);

        return Ok(ApiResponse<AiPerformanceSummaryResponseDto>.Ok(response));
    }

    [HttpPost("classify-performance")]
    public async Task<ActionResult<ApiResponse<ClassifyPerformanceResultDto>>> ClassifyPerformance([FromBody] ClassifyPerformanceRequest request, CancellationToken cancellationToken)
    {
        BumpPerformanceSummaryCacheVersion();
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

    private async Task<List<int>> GetAiPerformanceEmployeeScopeIdsAsync(int actorEmployeeId, string roleKey, CancellationToken cancellationToken)
    {
        if (roleKey == Roles.Admin)
        {
            return await _dbContext.NhanViens.AsNoTracking()
                .Where(x => x.TrangThai == 1)
                .Select(x => x.MaNhanVien)
                .ToListAsync(cancellationToken);
        }

        if (roleKey == Roles.Manager)
        {
            var managedDepartmentIds = await _dbContext.PhongBans.AsNoTracking()
                .Where(x => x.MaTruongPhong == actorEmployeeId)
                .Select(x => x.MaPhongBan)
                .ToListAsync(cancellationToken);

            var departmentEmployeeIds = managedDepartmentIds.Count == 0
                ? new List<int>()
                : await _dbContext.NhanViens.AsNoTracking()
                    .Where(x => x.TrangThai == 1 && x.MaPhongBan.HasValue && managedDepartmentIds.Contains(x.MaPhongBan.Value))
                    .Select(x => x.MaNhanVien)
                    .ToListAsync(cancellationToken);

            var teamIds = await _dbContext.ThanhVienNhoms.AsNoTracking()
                .Where(x => x.MaNhanVien == actorEmployeeId)
                .Select(x => x.MaNhom)
                .ToListAsync(cancellationToken);

            var ledTeamIds = await _dbContext.Nhoms.AsNoTracking()
                .Where(x => x.TruongNhom == actorEmployeeId)
                .Select(x => x.MaNhom)
                .ToListAsync(cancellationToken);

            var visibleTeamIds = teamIds.Concat(ledTeamIds).Distinct().ToList();
            var teamEmployeeIds = visibleTeamIds.Count == 0
                ? new List<int>()
                : await _dbContext.ThanhVienNhoms.AsNoTracking()
                    .Where(x => visibleTeamIds.Contains(x.MaNhom))
                    .Select(x => x.MaNhanVien)
                    .Distinct()
                    .ToListAsync(cancellationToken);

            return departmentEmployeeIds
                .Concat(teamEmployeeIds)
                .Append(actorEmployeeId)
                .Distinct()
                .ToList();
        }

        return new List<int> { actorEmployeeId };
    }

    private static (string Label, decimal Confidence, string Source) ResolveFastPerformanceClassification(
        decimal? kpi,
        int totalTasks,
        decimal lateRate,
        DuDoanAi? history)
    {
        if (history != null)
        {
            var parsed = TryParsePerformanceHistory(history);
            if (parsed.HasValue)
            {
                return (parsed.Value.Label, parsed.Value.Confidence, SourceAiHistory);
            }
        }

        if (!kpi.HasValue && totalTasks == 0)
        {
            return ("Chưa đủ dữ liệu", 0m, SourceInsufficientData);
        }

        var confidence = kpi.HasValue && totalTasks > 0 ? 0.70m : 0.55m;
        if ((kpi.HasValue && kpi.Value < 65m) || lateRate > 0.40m)
        {
            return ("Cần hỗ trợ", confidence, SourceRuleFast);
        }

        if ((kpi.HasValue && kpi.Value < 70m) || lateRate > 0.25m)
        {
            return ("Cần cải thiện", confidence, SourceRuleFast);
        }

        if (kpi.HasValue && kpi.Value >= 85m && lateRate <= 0.10m)
        {
            return ("Xuất sắc", confidence, SourceRuleFast);
        }

        if (kpi.HasValue && kpi.Value >= 70m && lateRate <= 0.25m)
        {
            return ("Tốt", confidence, SourceRuleFast);
        }

        return ("Cần cải thiện", confidence, SourceRuleFast);
    }

    private static (string Label, decimal Confidence)? TryParsePerformanceHistory(DuDoanAi history)
    {
        string? label = null;
        decimal? confidence = null;

        if (!string.IsNullOrWhiteSpace(history.OutputData))
        {
            try
            {
                using var document = JsonDocument.Parse(history.OutputData);
                var root = document.RootElement;
                if (root.TryGetProperty("labelDisplay", out var labelDisplayProperty))
                {
                    label = labelDisplayProperty.GetString();
                }
                else if (root.TryGetProperty("label", out var labelProperty))
                {
                    label = ToVietnamesePerformanceLabel(labelProperty.GetString());
                }

                if (root.TryGetProperty("confidence", out var confidenceProperty) && confidenceProperty.TryGetDecimal(out var parsedConfidence))
                {
                    confidence = parsedConfidence <= 1m ? parsedConfidence : parsedConfidence / 100m;
                }
            }
            catch
            {
                label = null;
            }
        }

        if (string.IsNullOrWhiteSpace(label) && history.DiemDuDoan.HasValue)
        {
            label = history.DiemDuDoan.Value >= 85m ? "Xuất sắc"
                : history.DiemDuDoan.Value >= 70m ? "Tốt"
                : history.DiemDuDoan.Value >= 50m ? "Cần cải thiện"
                : "Cần hỗ trợ";
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        return (label, Math.Clamp(confidence ?? 0.75m, 0m, 1m));
    }

    private static string ToVietnamesePerformanceLabel(string? label)
    {
        var normalized = NormalizePerformanceLabel(label ?? string.Empty);
        return normalized switch
        {
            "EXCELLENT" => "Xuất sắc",
            "GOOD" => "Tốt",
            "LOW" => "Cần hỗ trợ",
            _ => "Cần cải thiện"
        };
    }

    private static List<string> BuildPerformanceSummaryInsights(AiPerformanceSummaryDto summary, List<AiPerformanceEmployeeSummaryDto> employees)
    {
        var insights = new List<string>
        {
            $"Phân tích {summary.TotalEmployees} nhân sự trong kỳ {DateTime.Now:MM/yyyy}.",
            $"KPI trung bình {summary.AverageKpi:F1}%, tỷ lệ task trễ {summary.LateTaskRate:P0}."
        };

        if (summary.NeedSupportCount > 0)
        {
            insights.Add($"Có {summary.NeedSupportCount} nhân sự cần hỗ trợ, nên ưu tiên mentoring hoặc điều chỉnh phân công.");
        }
        else if (summary.ExcellentCount + summary.GoodCount > summary.TotalEmployees / 2)
        {
            insights.Add("Phần lớn nhân sự đang ở nhóm hiệu suất tốt, có thể duy trì nhịp theo dõi định kỳ.");
        }

        var topLateDepartment = employees
            .Where(x => x.LateTasks > 0 && !string.IsNullOrWhiteSpace(x.TenPhongBan))
            .GroupBy(x => x.TenPhongBan!)
            .Select(g => new { Department = g.Key, LateTasks = g.Sum(x => x.LateTasks) })
            .OrderByDescending(x => x.LateTasks)
            .FirstOrDefault();
        if (topLateDepartment != null)
        {
            insights.Add($"Task trễ tập trung nhiều nhất ở {topLateDepartment.Department} ({topLateDepartment.LateTasks} task).");
        }

        return insights;
    }

    private static AiTrainingDataQualityDto BuildDataQuality(int actualRows, int minRequiredRows)
    {
        var isLowData = actualRows < minRequiredRows;
        return new AiTrainingDataQualityDto
        {
            IsLowData = isLowData,
            MinRequiredRows = minRequiredRows,
            ActualRows = actualRows,
            WarningMessage = isLowData
                ? "Dữ liệu huấn luyện hiện còn ít, kết quả AI chủ yếu phục vụ minh họa quy trình phân tích và cần thêm dữ liệu thực tế để tăng độ tin cậy."
                : string.Empty
        };
    }

    private static string NormalizePerformanceLabel(string rawLabel)
    {
        var normalized = (rawLabel ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized.Contains("XUAT") || normalized.Contains("EXCELLENT"))
        {
            return "EXCELLENT";
        }

        if (normalized.Contains("TOT") || normalized.Contains("GOOD"))
        {
            return "GOOD";
        }

        if (normalized.Contains("TRUNG") || normalized.Contains("AVERAGE") || normalized.Contains("NORMAL"))
        {
            return "NORMAL";
        }

        if (normalized.Contains("KEM") || normalized.Contains("YEU") || normalized.Contains("POOR") || normalized.Contains("LOW"))
        {
            return "LOW";
        }

        return "NORMAL";
    }

    private sealed class EmployeeTaskStatsRaw
    {
        public int MaNhanVien { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int LateTasks { get; set; }
    }

    private sealed class KpiScoreRaw
    {
        public int MaNhanVien { get; set; }
        public decimal Score { get; set; }
    }

    public sealed class AiPerformanceSummaryResponseDto
    {
        public AiPerformancePeriodDto Period { get; set; } = new();
        public DateTime GeneratedAt { get; set; }
        public bool CacheHit { get; set; }
        public AiPerformanceSummaryDto Summary { get; set; } = new();
        public List<AiPerformanceLabelDistributionDto> LabelDistribution { get; set; } = new();
        public List<AiPerformanceEmployeeSummaryDto> Employees { get; set; } = new();
        public List<AiPerformanceEmployeeSummaryDto> TopPerformers { get; set; } = new();
        public List<AiPerformanceEmployeeSummaryDto> NeedSupport { get; set; } = new();
        public List<string> Insights { get; set; } = new();
    }

    public sealed class AiPerformancePeriodDto
    {
        public int Thang { get; set; }
        public int Nam { get; set; }
    }

    public sealed class AiPerformanceSummaryDto
    {
        public int TotalEmployees { get; set; }
        public int ExcellentCount { get; set; }
        public int GoodCount { get; set; }
        public int NeedImproveCount { get; set; }
        public int NeedSupportCount { get; set; }
        public int InsufficientDataCount { get; set; }
        public decimal AverageKpi { get; set; }
        public decimal LateTaskRate { get; set; }
    }

    public sealed class AiPerformanceLabelDistributionDto
    {
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public sealed class AiPerformanceEmployeeSummaryDto
    {
        public int MaNhanVien { get; set; }
        public string? HoTen { get; set; }
        public int? MaPhongBan { get; set; }
        public string? TenPhongBan { get; set; }
        public decimal? Kpi { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int LateTasks { get; set; }
        public decimal LateRate { get; set; }
        public string PerformanceLabel { get; set; } = string.Empty;
        public decimal Confidence { get; set; }
        public string Source { get; set; } = string.Empty;
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




