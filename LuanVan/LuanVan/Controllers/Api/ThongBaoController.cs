using LuanVan.Contracts;
using LuanVan.Data;
using LuanVan.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LuanVan.Controllers.Api;

[ApiController]
[Route("thongbao")]
[Authorize]
public class ThongBaoController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly IAiRuntimeSettingsProvider _aiRuntimeSettingsProvider;
    private readonly INotificationRuntimeSettingsProvider _notificationRuntimeSettingsProvider;
    private readonly IEmailService _emailService;

    public ThongBaoController(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        IAiRuntimeSettingsProvider aiRuntimeSettingsProvider,
        INotificationRuntimeSettingsProvider notificationRuntimeSettingsProvider,
        IEmailService emailService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _configuration = configuration;
        _aiRuntimeSettingsProvider = aiRuntimeSettingsProvider;
        _notificationRuntimeSettingsProvider = notificationRuntimeSettingsProvider;
        _emailService = emailService;
    }

    private void EnsureDbConnectionStringInitialized()
    {
        var connection = _dbContext.Database.GetDbConnection();
        if (!string.IsNullOrWhiteSpace(connection.ConnectionString))
        {
            return;
        }

        var configured = _configuration.GetConnectionString("DefaultConnection")
            ?? _configuration["ConnectionStrings:DefaultConnection"]
            ?? _configuration["ConnectionStrings__DefaultConnection"];

        if (string.IsNullOrWhiteSpace(configured))
        {
            return;
        }

        try
        {
            var builder = new SqlConnectionStringBuilder(configured);
            if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
            {
                builder.InitialCatalog = "LV2026";
            }

            _dbContext.Database.SetConnectionString(builder.ConnectionString);
        }
        catch
        {
            _dbContext.Database.SetConnectionString(configured);
        }
    }

    [HttpGet]
    [Authorize(Policy = Permissions.NotificationsReceive)]
    public async Task<ActionResult<ApiResponse<ThongBaoListDto>>> GetList(
        [FromQuery] string tab = "all",
        [FromQuery] int page = 1,
        [FromQuery] int size = 10,
        [FromQuery] int? maNhanVien = null)
    {
        try
        {
            page = Math.Max(1, page);
            size = Math.Clamp(size, 5, 50);

            var scope = await ResolveNhanVienScope(maNhanVien);
            if (scope.IsForbidden)
            {
                return Forbid();
            }

            if (scope.StatusCode.HasValue)
            {
                return StatusCode(scope.StatusCode.Value, ApiResponse<ThongBaoListDto>.Fail(scope.ErrorMessage ?? "Không có quyền truy cập."));
            }

            var targetNhanVienId = scope.TargetNhanVienId;
            var canSeeAll = scope.CanSeeAll;

            var links = _dbContext.ThongBaoNhanViens
                .AsNoTracking()
                .Select(x => new
                {
                    x.MaThongBao,
                    x.MaNhanVien,
                    IsUnread = !x.DaDoc.GetValueOrDefault()
                });

            if (targetNhanVienId.HasValue)
            {
                var employeeId = targetNhanVienId.Value;
                links = links.Where(x => x.MaNhanVien == employeeId);
            }

            var projected =
                from tb in _dbContext.ThongBaos.AsNoTracking()
                join loai in _dbContext.LoaiThongBaos.AsNoTracking() on tb.MaLoai equals loai.MaLoai into loaiGroup
                from loai in loaiGroup.DefaultIfEmpty()
                join l in links on tb.MaThongBao equals l.MaThongBao into groupedLinks
                select new
                {
                    tb.MaThongBao,
                    tb.NoiDung,
                    tb.ThoiGian,
                    TenLoai = loai != null ? loai.TenLoai : null,
                    TotalRecipients = groupedLinks.Count(),
                    UnreadRecipients = groupedLinks.Count(x => x.IsUnread),
                    IsUnread = groupedLinks.Any(x => x.IsUnread)
                };

            if (!canSeeAll)
            {
                projected = projected.Where(x => x.TotalRecipients > 0);
            }

            var unreadOnly = string.Equals(tab, "unread", StringComparison.OrdinalIgnoreCase);
            if (unreadOnly)
            {
                projected = projected.Where(x => x.IsUnread);
            }

            var unreadCount = await projected.CountAsync(x => x.IsUnread);
            var totalItems = await projected.CountAsync();

            var rows = await projected
                .OrderByDescending(x => x.ThoiGian ?? DateTime.MinValue)
                .ThenByDescending(x => x.MaThongBao)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync();

            var items = rows.Select(x => BuildItem(x.MaThongBao, x.NoiDung, x.ThoiGian, x.TenLoai, x.IsUnread, x.UnreadRecipients, x.TotalRecipients)).ToList();

            var result = new ThongBaoListDto
            {
                Scope = targetNhanVienId.HasValue ? "personal" : (canSeeAll ? "all" : "team"),
                MaNhanVien = targetNhanVienId,
                UnreadCount = unreadCount,
                Page = new PagedResult<ThongBaoItemDto>
                {
                    Items = items,
                    Page = page,
                    Size = size,
                    TotalItems = totalItems,
                    TotalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)size)
                }
            };

            return Ok(ApiResponse<ThongBaoListDto>.Ok(result));
        }
        catch (Exception ex)
        {
            var fallback = new ThongBaoListDto
            {
                Scope = "personal",
                UnreadCount = 0,
                Page = new PagedResult<ThongBaoItemDto>
                {
                    Items = new List<ThongBaoItemDto>(),
                    Page = Math.Max(1, page),
                    Size = Math.Clamp(size, 5, 50),
                    TotalItems = 0,
                    TotalPages = 1
                }
            };

            return Ok(ApiResponse<ThongBaoListDto>.Ok(fallback, $"Thong bao list fallback: {ex.Message}"));
        }
    }

    [HttpGet("summary")]
    [Authorize(Policy = Permissions.NotificationsReceive)]
    public async Task<ActionResult<ApiResponse<ThongBaoSummaryDto>>> GetSummary(
        [FromQuery] int? maNhanVien = null)
    {
        try
        {
            var scope = await ResolveNhanVienScope(maNhanVien);
            if (scope.IsForbidden)
            {
                return Forbid();
            }

            if (scope.StatusCode.HasValue)
            {
                return StatusCode(scope.StatusCode.Value, ApiResponse<ThongBaoSummaryDto>.Fail(scope.ErrorMessage ?? "Không có quyền truy cập."));
            }

            var targetNhanVienId = scope.TargetNhanVienId;
            var canSeeAll = scope.CanSeeAll;

            var links = _dbContext.ThongBaoNhanViens
                .AsNoTracking()
                .Select(x => new
                {
                    x.MaThongBao,
                    x.MaNhanVien,
                    IsUnread = !x.DaDoc.GetValueOrDefault()
                });

            if (targetNhanVienId.HasValue)
            {
                var employeeId = targetNhanVienId.Value;
                links = links.Where(x => x.MaNhanVien == employeeId);
            }

            var projected =
                from tb in _dbContext.ThongBaos.AsNoTracking()
                join l in links on tb.MaThongBao equals l.MaThongBao into groupedLinks
                select new
                {
                    tb.ThoiGian,
                    TotalRecipients = groupedLinks.Count(),
                    IsUnread = groupedLinks.Any(x => x.IsUnread)
                };

            if (!canSeeAll)
            {
                projected = projected.Where(x => x.TotalRecipients > 0);
            }

            var unreadCount = await projected.CountAsync(x => x.IsUnread);
            var latest = await projected.OrderByDescending(x => x.ThoiGian ?? DateTime.MinValue).Select(x => x.ThoiGian).FirstOrDefaultAsync();

            var dto = new ThongBaoSummaryDto
            {
                UnreadCount = unreadCount,
                LatestTime = latest,
                PollingIntervalMs = 20000
            };

            return Ok(ApiResponse<ThongBaoSummaryDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            var fallback = new ThongBaoSummaryDto
            {
                UnreadCount = 0,
                LatestTime = null,
                PollingIntervalMs = 20000
            };

            return Ok(ApiResponse<ThongBaoSummaryDto>.Ok(fallback, $"Thong bao summary fallback: {ex.Message}"));
        }
    }

    [HttpPost("mark-all-read")]
    [Authorize(Policy = Permissions.NotificationsReceive)]
    public async Task<ActionResult<ApiResponse<object>>> MarkAllRead([FromBody] ThongBaoBulkRequest? request)
    {
        request ??= new ThongBaoBulkRequest();
        var scope = await ResolveNhanVienScope(request.MaNhanVien);
        if (scope.IsForbidden)
        {
            return Forbid();
        }

        if (scope.StatusCode.HasValue)
        {
            return StatusCode(scope.StatusCode.Value, ApiResponse<object>.Fail(scope.ErrorMessage ?? "Không có quyền truy cập."));
        }

        var targetNhanVienId = scope.TargetNhanVienId;
        var canSeeAll = scope.CanSeeAll;

        var query = _dbContext.ThongBaoNhanViens.AsQueryable();

        if (targetNhanVienId.HasValue)
        {
            query = query.Where(x => x.MaNhanVien == targetNhanVienId.Value);
        }
        else if (!canSeeAll)
        {
            return BadRequest(ApiResponse<object>.Fail("Không xác định được pham vi thông báo de danh dau đã đọc."));
        }

        var updated = await query
            .Where(x => !x.DaDoc.GetValueOrDefault())
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.DaDoc, true));

        return Ok(ApiResponse<object>.Ok(new { updated }, "Đã danh dau tất cả đã đọc."));
    }

    [HttpPost("{maThongBao:int}/mark-read")]
    [Authorize(Policy = Permissions.NotificationsReceive)]
    public async Task<ActionResult<ApiResponse<object>>> MarkRead(int maThongBao, [FromBody] ThongBaoBulkRequest? request)
    {
        request ??= new ThongBaoBulkRequest();
        var scope = await ResolveNhanVienScope(request.MaNhanVien);
        if (scope.IsForbidden)
        {
            return Forbid();
        }

        if (scope.StatusCode.HasValue)
        {
            return StatusCode(scope.StatusCode.Value, ApiResponse<object>.Fail(scope.ErrorMessage ?? "Không có quyền truy cập."));
        }

        var targetNhanVienId = scope.TargetNhanVienId;
        var canSeeAll = scope.CanSeeAll;

        var query = _dbContext.ThongBaoNhanViens.Where(x => x.MaThongBao == maThongBao);

        if (targetNhanVienId.HasValue)
        {
            query = query.Where(x => x.MaNhanVien == targetNhanVienId.Value);
        }
        else if (!canSeeAll)
        {
            return BadRequest(ApiResponse<object>.Fail("Không xác định được nhân viên de cập nhật thông báo."));
        }

        var updated = await query.ExecuteUpdateAsync(setters => setters.SetProperty(x => x.DaDoc, true));
        return Ok(ApiResponse<object>.Ok(new { updated }, "Đã cập nhật trạng thái thông báo."));
    }

    [HttpDelete("{maThongBao:int}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int maThongBao, [FromQuery] int? maNhanVien = null)
    {
        var scope = await ResolveNhanVienScope(maNhanVien);
        if (scope.IsForbidden)
        {
            return Forbid();
        }

        if (scope.StatusCode.HasValue)
        {
            return StatusCode(scope.StatusCode.Value, ApiResponse<object>.Fail(scope.ErrorMessage ?? "Không có quyền truy cập."));
        }

        var targetNhanVienId = scope.TargetNhanVienId;
        var canSeeAll = scope.CanSeeAll;

        if (targetNhanVienId.HasValue)
        {
            var removedLinks = await _dbContext.ThongBaoNhanViens
                .Where(x => x.MaThongBao == maThongBao && x.MaNhanVien == targetNhanVienId.Value)
                .ExecuteDeleteAsync();

            return Ok(ApiResponse<object>.Ok(new { removed = removedLinks }, "Đã xóa thông báo khoi danh sach cua ban."));
        }

        if (!canSeeAll)
        {
            return BadRequest(ApiResponse<object>.Fail("Ban không co quyền xóa toan bo thông báo."));
        }

        await _dbContext.ThongBaoNhanViens.Where(x => x.MaThongBao == maThongBao).ExecuteDeleteAsync();
        var removedNotifications = await _dbContext.ThongBaos.Where(x => x.MaThongBao == maThongBao).ExecuteDeleteAsync();

        return Ok(ApiResponse<object>.Ok(new { removed = removedNotifications }, "Đã xóa thông báo."));
    }

    [HttpPost("kpi-alerts/sync")]
    [Authorize(Policy = Permissions.NotificationsReceive)]
    public async Task<ActionResult<ApiResponse<object>>> SyncKpiAlerts(
        [FromQuery] int? thang = null,
        [FromQuery] int? nam = null)
    {
        EnsureDbConnectionStringInitialized();
        var notifySettings = await _notificationRuntimeSettingsProvider.GetAsync();
        if (!notifySettings.System)
        {
            return Ok(ApiResponse<object>.Ok(new { created = 0, skipped = true }, "Đang tắt thông báo trong cài đặt hệ thống."));
        }

        var runtime = await _aiRuntimeSettingsProvider.GetAsync();
        if (!runtime.General.Enabled)
        {
            return Ok(ApiResponse<object>.Ok(new { created = 0, skipped = true }, "AI đang tắt nên không đồng bộ cảnh báo KPI tự động."));
        }

        if (!runtime.Automation.AutoLateAlert || !runtime.Automation.AutoSendNotification)
        {
            return Ok(ApiResponse<object>.Ok(new { created = 0, skipped = true }, "Tự động hóa cảnh báo/notification đang tắt trong Cấu hình AI."));
        }

        var roleKey = ResolveRoleKeyFromUser();
        if (roleKey != "manager" && roleKey != "admin")
        {
            return Forbid();
        }

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(ApiResponse<object>.Fail("Không xác định được tài khoản đăng nhập."));
        }

        var actor = await _dbContext.NhanViens
            .AsNoTracking()
            .Where(x => x.AspNetUserId == userId)
            .Select(x => new { x.MaNhanVien, x.MaPhongBan })
            .FirstOrDefaultAsync();

        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Tài khoản chưa liên kết nhân viên."));
        }

        var targetMonth = thang is >= 1 and <= 12 ? thang.Value : DateTime.Now.Month;
        var targetYear = nam is > 2000 and <= 3000 ? nam.Value : DateTime.Now.Year;
        var previousMonthDate = new DateTime(targetYear, targetMonth, 1).AddMonths(-1);
        var prevMonth = previousMonthDate.Month;
        var prevYear = previousMonthDate.Year;

        var employeeQuery = _dbContext.NhanViens
            .AsNoTracking()
            .Where(x => (x.TrangThai ?? 1) == 1);

        if (roleKey == "manager")
        {
            employeeQuery = employeeQuery.Where(x => x.MaPhongBan == actor.MaPhongBan);
        }

        var employees = await employeeQuery
            .Select(x => new { x.MaNhanVien, x.HoTen })
            .ToListAsync();

        var employeeIds = employees.Select(x => x.MaNhanVien).ToList();
        if (employeeIds.Count == 0)
        {
            return Ok(ApiResponse<object>.Ok(new { created = 0, message = "Không có nhân viên trong phạm vi." }));
        }

        var latestKpi = await _dbContext.KetQuaKpis
            .AsNoTracking()
            .Where(x => x.MaKpi == 1 && x.thang == targetMonth && x.nam == targetYear && employeeIds.Contains(x.MaNhanVien))
            .Select(x => new { x.MaNhanVien, Score = x.DiemSo.HasValue ? (double)x.DiemSo.Value : 0.0 })
            .ToListAsync();

        var prevKpi = await _dbContext.KetQuaKpis
            .AsNoTracking()
            .Where(x => x.MaKpi == 1 && x.thang == prevMonth && x.nam == prevYear && employeeIds.Contains(x.MaNhanVien))
            .Select(x => new { x.MaNhanVien, Score = x.DiemSo.HasValue ? (double)x.DiemSo.Value : 0.0 })
            .ToDictionaryAsync(x => x.MaNhanVien, x => x.Score);

        var today = DateTime.Today;
        var lateTaskByEmployee = await (
            from pc in _dbContext.PhanCongNhanViens.AsNoTracking()
            join cv in _dbContext.CongViecs.AsNoTracking() on pc.MaCongViec equals cv.MaCongViec
            where employeeIds.Contains(pc.MaNhanVien)
                  && cv.DaXoa != true
                  && (cv.MaTrangThai == 4 || (cv.MaTrangThai != 3 && cv.HanHoanThanh.HasValue && cv.HanHoanThanh.Value.Date < today))
            group cv by pc.MaNhanVien into g
            select new { MaNhanVien = g.Key, Count = g.Select(x => x.MaCongViec).Distinct().Count() }
        ).ToDictionaryAsync(x => x.MaNhanVien, x => x.Count);

        var lowKpiAlerts = latestKpi
            .Where(x => x.Score < 50)
            .Select(x => $"KPI thấp: {(employees.FirstOrDefault(e => e.MaNhanVien == x.MaNhanVien)?.HoTen ?? $"NV {x.MaNhanVien}")} đạt {x.Score:F1} điểm trong {targetMonth:00}/{targetYear}.")
            .ToList();

        var droppingAlerts = latestKpi
            .Where(x => prevKpi.ContainsKey(x.MaNhanVien) && (prevKpi[x.MaNhanVien] - x.Score) >= 10)
            .Select(x => $"KPI giảm mạnh: {(employees.FirstOrDefault(e => e.MaNhanVien == x.MaNhanVien)?.HoTen ?? $"NV {x.MaNhanVien}")} giảm {(prevKpi[x.MaNhanVien] - x.Score):F1} điểm so với tháng trước.")
            .ToList();

        var overdueAlerts = notifySettings.OverdueTask
            ? lateTaskByEmployee
                .Where(x => x.Value >= 3)
                .Select(x => $"Task trễ hạn nhiều: {(employees.FirstOrDefault(e => e.MaNhanVien == x.Key)?.HoTen ?? $"NV {x.Key}")} có {x.Value} công việc trễ hạn.")
                .ToList()
            : new List<string>();

        var alertMessages = lowKpiAlerts
            .Concat(droppingAlerts)
            .Concat(overdueAlerts)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();

        if (alertMessages.Count == 0)
        {
            return Ok(ApiResponse<object>.Ok(new { created = 0, message = "Không có cảnh báo KPI mới." }));
        }

        var loai = await _dbContext.LoaiThongBaos.FirstOrDefaultAsync(x => x.TenLoai == "KPI_ALERT");
        if (loai == null)
        {
            loai = new Models.LoaiThongBao { TenLoai = "KPI_ALERT" };
            _dbContext.LoaiThongBaos.Add(loai);
            await _dbContext.SaveChangesAsync();
        }

        var beginOfDay = today;
        var endOfDay = today.AddDays(1);
        var existingToday = await (
            from tb in _dbContext.ThongBaos.AsNoTracking()
            join link in _dbContext.ThongBaoNhanViens.AsNoTracking() on tb.MaThongBao equals link.MaThongBao
            where link.MaNhanVien == actor.MaNhanVien
                  && tb.ThoiGian.HasValue
                  && tb.ThoiGian.Value >= beginOfDay
                  && tb.ThoiGian.Value < endOfDay
            select tb.NoiDung
        ).ToListAsync();

        var toCreate = alertMessages
            .Where(msg => !existingToday.Any(x => string.Equals((x ?? string.Empty).Trim(), msg.Trim(), StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var msg in toCreate)
        {
            var thongBao = new Models.ThongBao
            {
                MaLoai = loai.MaLoai,
                NoiDung = msg,
                ThoiGian = DateTime.Now
            };
            _dbContext.ThongBaos.Add(thongBao);
            await _dbContext.SaveChangesAsync();

            _dbContext.ThongBaoNhanViens.Add(new Models.ThongBaoNhanVien
            {
                MaThongBao = thongBao.MaThongBao,
                MaNhanVien = actor.MaNhanVien,
                DaDoc = false
            });
            await _dbContext.SaveChangesAsync();
        }

        if (notifySettings.OverdueTask && !string.IsNullOrWhiteSpace(notifySettings.Email) && overdueAlerts.Count > 0)
        {
            var recipientName = User.Identity?.Name ?? "Quản trị viên";
            var htmlBody = $"<p>Hệ thống vừa phát hiện các cảnh báo trễ hạn:</p><ul>{string.Join(string.Empty, overdueAlerts.Select(x => $"<li>{System.Net.WebUtility.HtmlEncode(x)}</li>"))}</ul>";
            await _emailService.SendSystemNotificationEmailAsync(
                notifySettings.Email,
                recipientName,
                "[LuanVan KPI] Cảnh báo task trễ hạn",
                htmlBody);
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            created = toCreate.Count,
            totalDetected = alertMessages.Count,
            thang = targetMonth,
            nam = targetYear
        }, toCreate.Count > 0 ? "Đã đồng bộ cảnh báo KPI." : "Không có cảnh báo KPI mới."));
    }

    private string ResolveRoleKeyFromUser()
    {
        if (User.IsInRole("Admin"))
        {
            return "admin";
        }

        if (User.IsInRole("Manager"))
        {
            return "manager";
        }

        return "employee";
    }

    private async Task<ScopeResolveResult> ResolveNhanVienScope(int? requestedNhanVienId)
    {
        EnsureDbConnectionStringInitialized();
        var roleKey = ResolveRoleKeyFromUser();

        if (roleKey == "admin")
        {
            return new ScopeResolveResult
            {
                TargetNhanVienId = requestedNhanVienId,
                CanSeeAll = !requestedNhanVienId.HasValue
            };
        }

        if (roleKey == "manager")
        {
            var managerUserId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(managerUserId))
            {
                return new ScopeResolveResult
                {
                    StatusCode = StatusCodes.Status401Unauthorized,
                    ErrorMessage = "Không xác định được tài khoản đăng nhập."
                };
            }

            var managerNhanVienId = await _dbContext.NhanViens
                .AsNoTracking()
                .Where(x => x.AspNetUserId == managerUserId)
                .Select(x => (int?)x.MaNhanVien)
                .FirstOrDefaultAsync();

            if (!managerNhanVienId.HasValue)
            {
                return new ScopeResolveResult
                {
                    StatusCode = StatusCodes.Status401Unauthorized,
                    ErrorMessage = "Tài khoản quản lý chưa liên kết nhân viên."
                };
            }

            if (requestedNhanVienId.HasValue && requestedNhanVienId.Value != managerNhanVienId.Value)
            {
                return new ScopeResolveResult
                {
                    IsForbidden = true
                };
            }

            return new ScopeResolveResult
            {
                TargetNhanVienId = requestedNhanVienId,
                CanSeeAll = !requestedNhanVienId.HasValue
            };
        }

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new ScopeResolveResult
            {
                StatusCode = StatusCodes.Status401Unauthorized,
                ErrorMessage = "Không xác định được tài khoản đăng nhập."
            };
        }

        var currentNhanVienId = await _dbContext.NhanViens
            .AsNoTracking()
            .Where(x => x.AspNetUserId == userId)
            .Select(x => (int?)x.MaNhanVien)
            .FirstOrDefaultAsync();

        if (!currentNhanVienId.HasValue)
        {
            return new ScopeResolveResult
            {
                StatusCode = StatusCodes.Status401Unauthorized,
                ErrorMessage = "Tài khoản chưa được liên kết nhân viên."
            };
        }

        if (requestedNhanVienId.HasValue && requestedNhanVienId.Value != currentNhanVienId.Value)
        {
            return new ScopeResolveResult
            {
                IsForbidden = true
            };
        }

        return new ScopeResolveResult
        {
            TargetNhanVienId = currentNhanVienId.Value,
            CanSeeAll = false
        };
    }

    private sealed class ScopeResolveResult
    {
        public int? TargetNhanVienId { get; set; }
        public bool CanSeeAll { get; set; }
        public int? StatusCode { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsForbidden { get; set; }
    }

    private static ThongBaoItemDto BuildItem(
        int maThongBao,
        string? noiDung,
        DateTime? thoiGian,
        string? tenLoai,
        bool isUnread,
        int unreadRecipients,
        int totalRecipients)
    {
        var typeKey = DetectType(tenLoai, noiDung);
        var (icon, colorClass) = typeKey switch
        {
            "task" => ("bi bi-journal-check", "type-task"),
            "kpi" => ("bi bi-bar-chart-line", "type-kpi"),
            "alert" => ("bi bi-exclamation-triangle", "type-alert"),
            "ai" => ("bi bi-robot", "type-ai"),
            _ => ("bi bi-gear", "type-system")
        };

        var detailUrl = typeKey switch
        {
            "task" => "/Portal/Tasks",
            "kpi" => "/Portal/Kpi",
            "ai" => "/Portal/AiInsights",
            "alert" => "/Portal/Dashboard",
            _ => "/Portal/Settings"
        };

        return new ThongBaoItemDto
        {
            MaThongBao = maThongBao,
            NoiDung = string.IsNullOrWhiteSpace(noiDung) ? "Thông báo mới" : noiDung.Trim(),
            ThoiGian = thoiGian,
            Loai = typeKey,
            LoaiHienThi = ToLoaiLabel(typeKey),
            Icon = icon,
            Mau = colorClass,
            IsUnread = isUnread,
            UnreadRecipients = unreadRecipients,
            TotalRecipients = totalRecipients,
            DetailUrl = detailUrl
        };
    }

    private static string DetectType(string? tenLoai, string? noiDung)
    {
        var source = $"{tenLoai} {noiDung}".ToLowerInvariant();

        if (source.Contains("ai") || source.Contains("robot")) return "ai";
        if (source.Contains("kpi")) return "kpi";
        if (source.Contains("canh bao") || source.Contains("rui ro") || source.Contains("tre") || source.Contains("quahan") || source.Contains("tr\u1ec5")) return "alert";
        if (source.Contains("task") || source.Contains("công việc") || source.Contains("deadline")) return "task";
        if (source.Contains("hệ thống") || source.Contains("mật khẩu") || source.Contains("tài khoản") || source.Contains("account") || source.Contains("reset")) return "system";

        return "system";
    }

    private static string ToLoaiLabel(string typeKey)
    {
        return typeKey switch
        {
            "task" => "Task",
            "kpi" => "KPI",
            "alert" => "Canh bao",
            "ai" => "AI",
            _ => "He thong"
        };
    }

    public class ThongBaoBulkRequest
    {
        public int? MaNhanVien { get; set; }
    }

    public class ThongBaoSummaryDto
    {
        public int UnreadCount { get; set; }
        public DateTime? LatestTime { get; set; }
        public int PollingIntervalMs { get; set; }
    }

    public class ThongBaoListDto
    {
        public string Scope { get; set; } = "personal";
        public int? MaNhanVien { get; set; }
        public int UnreadCount { get; set; }
        public PagedResult<ThongBaoItemDto> Page { get; set; } = new();
    }

    public class ThongBaoItemDto
    {
        public int MaThongBao { get; set; }
        public string NoiDung { get; set; } = string.Empty;
        public DateTime? ThoiGian { get; set; }
        public string Loai { get; set; } = "system";
        public string LoaiHienThi { get; set; } = "He thong";
        public string Icon { get; set; } = "bi bi-gear";
        public string Mau { get; set; } = "type-system";
        public bool IsUnread { get; set; }
        public int UnreadRecipients { get; set; }
        public int TotalRecipients { get; set; }
        public string DetailUrl { get; set; } = "/Portal/Dashboard";
    }
}



