using System.Security.Claims;
using System.Text.Json;
using ClosedXML.Excel;
using LuanVan.Contracts;
using LuanVan.Data;
using LuanVan.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LuanVan.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/report")]
[Route("api/baocao")]
public class ReportController : ControllerBase
{
    private const string ReportNotificationTypeKey = "BAOCAO";
    private readonly AppDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;

    public ReportController(AppDbContext dbContext, UserManager<ApplicationUser> userManager, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _configuration = configuration;
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

    private sealed class ActorContext
    {
        public string? UserId { get; set; }
        public int MaNhanVien { get; set; }
        public int? MaPhongBan { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsManager { get; set; }
        public bool IsEmployee { get; set; }
    }

    [HttpGet("load-page")]
    [Authorize(Policy = Permissions.ReportsView)]
    public async Task<ActionResult<ApiResponse<ReportPageLoadDto>>> LoadPage(CancellationToken cancellationToken)
    {
        var actor = await GetActorContextAsync(cancellationToken);
        if (actor == null)
        {
            return Unauthorized(ApiResponse<ReportPageLoadDto>.Fail("Không xác định được người dùng."));
        }

        List<EmployeeTaskDto> myTasks;
        if (actor.IsManager && !actor.IsAdmin)
        {
            // Quản lý cần xem công việc theo phạm vi phòng ban/dự án để tạo báo cáo tổng hợp.
            var taskQuery = _dbContext.CongViecs
                .AsNoTracking()
                .Where(x => !(x.DaXoa ?? false));

            if (actor.MaPhongBan.HasValue)
            {
                var maPhongBan = actor.MaPhongBan.Value;
                taskQuery = taskQuery.Where(x =>
                    _dbContext.PhanCongPhongBans.Any(pc => pc.MaCongViec == x.MaCongViec && pc.MaPhongBan == maPhongBan)
                    || _dbContext.DuAnPhongBans.Any(dp => dp.MaDuAn == x.MaDuAn && dp.MaPhongBan == maPhongBan)
                    || _dbContext.PhanCongNhanViens.Any(pc => pc.MaCongViec == x.MaCongViec && pc.NhanVien.MaPhongBan == maPhongBan));
            }

            myTasks = await taskQuery
                .Select(x => new EmployeeTaskDto
                {
                    MaCongViec = x.MaCongViec,
                    TenCongViec = x.TenCongViec,
                    MoTa = x.MoTa,
                    MaDuAn = x.MaDuAn,
                    TenDuAn = x.DuAn != null ? x.DuAn.TenDuAn : null,
                    NgayBatDau = x.NgayBatDau,
                    HanHoanThanh = x.HanHoanThanh,
                    TienDoPhanTram = x.PhanTramHoanThanh ?? 0,
                    TenTrangThai = x.MaTrangThai == 3 ? "Hoàn thành" : (x.MaTrangThai == 2 ? "Đang thực hiện" : "Chưa bắt đầu"),
                    TenDoUuTien = x.DoUuTien != null ? x.DoUuTien.TenDoUuTien : null
                })
                .OrderByDescending(x => x.HanHoanThanh ?? DateTime.MaxValue)
                .Take(400)
                .ToListAsync(cancellationToken);
        }
        else
        {
            myTasks = await _dbContext.PhanCongNhanViens
                .AsNoTracking()
                .Where(x => x.MaNhanVien == actor.MaNhanVien)
                .Select(x => x.CongViec)
                .Where(x => x != null && !(x.DaXoa ?? false))
                .Select(x => new EmployeeTaskDto
                {
                    MaCongViec = x!.MaCongViec,
                    TenCongViec = x.TenCongViec,
                    MoTa = x.MoTa,
                    MaDuAn = x.MaDuAn,
                    TenDuAn = x.DuAn != null ? x.DuAn.TenDuAn : null,
                    NgayBatDau = x.NgayBatDau,
                    HanHoanThanh = x.HanHoanThanh,
                    TienDoPhanTram = x.PhanTramHoanThanh ?? 0,
                    TenTrangThai = x.MaTrangThai == 3 ? "Hoàn thành" : (x.MaTrangThai == 2 ? "Đang thực hiện" : "Chưa bắt đầu"),
                    TenDoUuTien = x.DoUuTien != null ? x.DoUuTien.TenDoUuTien : null
                })
                .OrderByDescending(x => x.HanHoanThanh ?? DateTime.MaxValue)
                .Take(200)
                .ToListAsync(cancellationToken);
        }

        var myRequests = await _dbContext.YeuCauBaoCaos
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.NguoiNhanYeuCau == actor.UserId)
            .OrderByDescending(x => x.NgayTao ?? DateTime.MinValue)
            .Select(x => new ReportRequestDto
            {
                MaYeuCau = x.MaYeuCau,
                TieuDe = x.TieuDe,
                MoTa = x.MoTa,
                Priority = x.Priority,
                HanChot = x.HanChot,
                TrangThai = x.TrangThai,
                NgayTao = x.NgayTao,
                NguoiYeuCau = x.NguoiYeuCauNavigation != null ? x.NguoiYeuCauNavigation.UserName : null
            })
            .ToListAsync(cancellationToken);

        List<EmployeeDto> managers;
        if (actor.IsManager && !actor.IsAdmin)
        {
            // Manager gửi báo cáo lên Admin.
            managers = await _dbContext.NhanViens
                .AsNoTracking()
                .Where(x => x.AspNetUserId != null)
                .Where(x => _dbContext.UserRoles.Any(ur =>
                    ur.UserId == x.AspNetUserId &&
                    _dbContext.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Admin)))
                .Select(x => new EmployeeDto
                {
                    MaNhanVien = x.MaNhanVien,
                    HoTen = x.HoTen,
                    AspNetUserId = x.AspNetUserId
                })
                .Distinct()
                .Take(200)
                .ToListAsync(cancellationToken);
        }
        else
        {
            // Employee/Admin mặc định chọn Manager nhận báo cáo.
            managers = await _dbContext.NhanViens
                .AsNoTracking()
                .Where(x => x.AspNetUserId != null)
                .Where(x => x.MaPhongBan == actor.MaPhongBan || actor.IsAdmin)
                .Where(x => _dbContext.UserRoles.Any(ur =>
                    ur.UserId == x.AspNetUserId &&
                    _dbContext.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Manager)))
                .Select(x => new EmployeeDto
                {
                    MaNhanVien = x.MaNhanVien,
                    HoTen = x.HoTen,
                    AspNetUserId = x.AspNetUserId
                })
                .Distinct()
                .ToListAsync(cancellationToken);

            if (!managers.Any())
            {
                managers = await _dbContext.NhanViens
                    .AsNoTracking()
                    .Where(x => x.AspNetUserId != null)
                    .Where(x => _dbContext.UserRoles.Any(ur =>
                        ur.UserId == x.AspNetUserId &&
                        _dbContext.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Manager)))
                    .Select(x => new EmployeeDto
                    {
                        MaNhanVien = x.MaNhanVien,
                        HoTen = x.HoTen,
                        AspNetUserId = x.AspNetUserId
                    })
                    .Distinct()
                    .Take(200)
                    .ToListAsync(cancellationToken);
            }
        }

        var latestKpi = await _dbContext.KetQuaKpis
            .AsNoTracking()
            .Where(x => x.MaNhanVien == actor.MaNhanVien)
            .OrderByDescending(x => x.nam)
            .ThenByDescending(x => x.thang)
            .FirstOrDefaultAsync(cancellationToken);

        var kpiHistory = await _dbContext.KetQuaKpis
            .AsNoTracking()
            .Where(x => x.MaNhanVien == actor.MaNhanVien)
            .OrderByDescending(x => x.nam)
            .ThenByDescending(x => x.thang)
            .Take(6)
            .Select(x => new KpiHistoryItemDto
            {
                Thang = x.thang ?? 0,
                Nam = x.nam ?? 0,
                DiemSo = x.DiemSo ?? 0,
                XepLoai = null
            })
            .ToListAsync(cancellationToken);

        var payload = new ReportPageLoadDto
        {
            MyTasks = myTasks,
            MyRequests = myRequests,
            Managers = managers,
            PersonalKpi = new PersonalKpiSidebarDto
            {
                DiemHienTai = latestKpi?.DiemSo ?? 0,
                XepLoai = "Chưa xếp loại",
                XuHuongPhanTram = null,
                NgayCapNhatGanNhat = null,
                LichSu = kpiHistory
            }
        };

        return Ok(ApiResponse<ReportPageLoadDto>.Ok(payload));
    }

    [HttpPost("save-draft")]
    [Authorize(Policy = Permissions.ReportsCreate)]
    public async Task<ActionResult<ApiResponse<object>>> SaveDraft([FromBody] SaveReportDraftRequest request, CancellationToken cancellationToken)
    {
        var actor = await GetActorContextAsync(cancellationToken);
        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Không xác định được người dùng."));
        }

        if (string.IsNullOrWhiteSpace(request.TenBaoCao) || string.IsNullOrWhiteSpace(request.LoaiBaoCao))
        {
            return BadRequest(ApiResponse<object>.Fail("Thiếu tiêu đề hoặc loại báo cáo."));
        }

        var tenBaoCao = request.TenBaoCao.Trim();
        if (request.NgayBatDau.HasValue && request.NgayKetThuc.HasValue && request.NgayKetThuc.Value.Date <= request.NgayBatDau.Value.Date)
        {
            return BadRequest(ApiResponse<object>.Fail("Ngày kết thúc phải lớn hơn ngày bắt đầu."));
        }

        var duplicateReportName = await _dbContext.BaoCaos.AnyAsync(x =>
            !x.IsDeleted
            && x.TenBaoCao != null
            && EF.Functions.Collate(x.TenBaoCao, "Latin1_General_CI_AI") == tenBaoCao, cancellationToken);
        if (duplicateReportName)
        {
            return Conflict(ApiResponse<object>.Fail("Tên báo cáo đã tồn tại."));
        }

        var recipientLabel = await ResolveRecipientLabelAsync(request.NguoiNhanUserId, request.NguoiNhanBaoCao, cancellationToken);
        if (string.IsNullOrWhiteSpace(recipientLabel))
        {
            return BadRequest(ApiResponse<object>.Fail("Vui lòng chọn người nhận báo cáo."));
        }

        var report = new BaoCao
        {
            TenBaoCao = tenBaoCao,
            LoaiBaoCao = request.LoaiBaoCao?.Trim(),
            MaDuAn = request.MaDuAn,
            MaPhongBan = actor.MaPhongBan,
            NguoiTao = actor.UserId,
            NgayTao = DateTime.Now,
            NgayCapNhat = DateTime.Now,
            NgayBatDau = request.NgayBatDau,
            NgayKetThuc = request.NgayKetThuc,
            DinhDang = "PDF",
            TrangThai = ReportWorkflowStatus.Draft,
            NoiDung = request.NoiDung,
            IsDeleted = false
        };
        _dbContext.BaoCaos.Add(report);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await TrySaveRecipientMetadataAsync(report.MaBaoCao, recipientLabel, cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { report.MaBaoCao, report.TrangThai }, "Lưu nháp thành công."));
    }

    [HttpPost("submit")]
    [Authorize(Policy = Permissions.ReportsCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Submit([FromBody] SubmitReportRequest request, CancellationToken cancellationToken)
    {
        var actor = await GetActorContextAsync(cancellationToken);
        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Không xác định được người dùng."));
        }

        if (string.IsNullOrWhiteSpace(request.TenBaoCao) || string.IsNullOrWhiteSpace(request.LoaiBaoCao))
        {
            return BadRequest(ApiResponse<object>.Fail("Thiếu tiêu đề hoặc loại báo cáo."));
        }

        var tenBaoCao = request.TenBaoCao.Trim();
        if (request.NgayBatDau.HasValue && request.NgayKetThuc.HasValue && request.NgayKetThuc.Value.Date <= request.NgayBatDau.Value.Date)
        {
            return BadRequest(ApiResponse<object>.Fail("Ngày kết thúc phải lớn hơn ngày bắt đầu."));
        }

        var duplicateReportName = await _dbContext.BaoCaos.AnyAsync(x =>
            !x.IsDeleted
            && x.TenBaoCao != null
            && EF.Functions.Collate(x.TenBaoCao, "Latin1_General_CI_AI") == tenBaoCao, cancellationToken);
        if (duplicateReportName)
        {
            return Conflict(ApiResponse<object>.Fail("Tên báo cáo đã tồn tại."));
        }

        var recipientLabel = await ResolveRecipientLabelAsync(request.NguoiNhanUserId, request.NguoiNhanBaoCao, cancellationToken);
        if (string.IsNullOrWhiteSpace(recipientLabel))
        {
            return BadRequest(ApiResponse<object>.Fail("Vui lòng chọn người nhận báo cáo."));
        }

        var report = new BaoCao
        {
            TenBaoCao = tenBaoCao,
            LoaiBaoCao = request.LoaiBaoCao?.Trim(),
            MaDuAn = request.MaDuAn,
            MaPhongBan = actor.MaPhongBan,
            NguoiTao = actor.UserId,
            NgayTao = DateTime.Now,
            NgayCapNhat = DateTime.Now,
            NgayBatDau = request.NgayBatDau,
            NgayKetThuc = request.NgayKetThuc,
            DinhDang = "PDF",
            TrangThai = ReportWorkflowStatus.Submitted,
            NoiDung = request.NoiDung,
            IsDeleted = false
        };
        _dbContext.BaoCaos.Add(report);

        if (request.MaYeuCau > 0)
        {
            var ycb = await _dbContext.YeuCauBaoCaos.FirstOrDefaultAsync(x => x.MaYeuCau == request.MaYeuCau && !x.IsDeleted, cancellationToken);
            if (ycb != null)
            {
                ycb.TrangThai = ReportWorkflowStatus.Submitted;
                ycb.NgayCapNhat = DateTime.Now;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await TrySaveRecipientMetadataAsync(report.MaBaoCao, recipientLabel, cancellationToken);
        try
        {
            var notifyUserIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(request.NguoiNhanUserId))
            {
                notifyUserIds.Add(request.NguoiNhanUserId);
            }

            if (request.MaYeuCau > 0)
            {
                var ycbNotify = await _dbContext.YeuCauBaoCaos
                    .AsNoTracking()
                    .Where(x => x.MaYeuCau == request.MaYeuCau && !x.IsDeleted)
                    .Select(x => x.NguoiYeuCau)
                    .FirstOrDefaultAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(ycbNotify))
                {
                    notifyUserIds.Add(ycbNotify);
                }
            }

            if (notifyUserIds.Count == 0 && actor.MaPhongBan.HasValue)
            {
                var managerIds = await _dbContext.NhanViens
                    .AsNoTracking()
                    .Where(x => x.AspNetUserId != null && x.MaPhongBan == actor.MaPhongBan)
                    .Where(x => _dbContext.UserRoles.Any(ur =>
                        ur.UserId == x.AspNetUserId &&
                        _dbContext.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Manager)))
                    .Select(x => x.AspNetUserId!)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                foreach (var id in managerIds)
                {
                    notifyUserIds.Add(id);
                }
            }

            notifyUserIds.RemoveWhere(x => string.IsNullOrWhiteSpace(x) || string.Equals(x, actor.UserId, StringComparison.OrdinalIgnoreCase));
            await PushNotificationToUsersAsync(notifyUserIds, $"Bạn có báo cáo mới cần xem: \"{report.TenBaoCao}\".", cancellationToken);
        }
        catch
        {
            // Ignore notification failures to avoid breaking submit flow.
        }

        return Ok(ApiResponse<object>.Ok(new { report.MaBaoCao, report.TrangThai }, "Gửi báo cáo thành công."));
    }

    [HttpGet("list")]
    [Authorize(Policy = Permissions.ReportsView)]
    public async Task<IActionResult> List(
        [FromQuery] DateTime? NgayBatDau,
        [FromQuery] DateTime? NgayKetThuc,
        [FromQuery] string? LoaiBaoCao,
        [FromQuery] int? MaPhongBan,
        [FromQuery] int? MaNhanVien,
        [FromQuery] string? TuKhoaNhanVien,
        [FromQuery] bool MineOnly = false,
        [FromQuery] int PageNumber = 1,
        [FromQuery] int PageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorContextAsync(cancellationToken);
        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Không xác định người dùng."));
        }

        PageNumber = Math.Max(1, PageNumber);
        PageSize = Math.Clamp(PageSize, 1, 200);

        var query = _dbContext.BaoCaos
            .AsNoTracking()
            .Include(x => x.DuAn)
            .Include(x => x.PhongBan)
            .Include(x => x.NguoiTaoNavigation)
            .Where(x => !x.IsDeleted);

        if (MineOnly)
        {
            query = query.Where(x => x.NguoiTao == actor.UserId);
        }
        else if (!actor.IsAdmin)
        {
            if (actor.IsManager)
            {
                query = query
                    .Where(x => x.MaPhongBan == actor.MaPhongBan)
                    .Where(x => _dbContext.UserRoles.Any(ur =>
                        ur.UserId == x.NguoiTao &&
                        _dbContext.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Employee)));
            }
            else
            {
                query = query.Where(x => x.NguoiTao == actor.UserId);
            }
        }

        if (NgayBatDau.HasValue)
        {
            query = query.Where(x => (x.NgayTao ?? DateTime.MinValue) >= NgayBatDau.Value);
        }
        if (NgayKetThuc.HasValue)
        {
            query = query.Where(x => (x.NgayTao ?? DateTime.MinValue) <= NgayKetThuc.Value);
        }
        if (!string.IsNullOrWhiteSpace(LoaiBaoCao) && !string.Equals(LoaiBaoCao, "personal", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.LoaiBaoCao == LoaiBaoCao);
        }
        if (MaPhongBan.HasValue && MaPhongBan.Value > 0)
        {
            query = query.Where(x => x.MaPhongBan == MaPhongBan.Value);
        }

        if (MaNhanVien.HasValue && MaNhanVien.Value > 0)
        {
            var employeeUserId = await _dbContext.NhanViens
                .AsNoTracking()
                .Where(x => x.MaNhanVien == MaNhanVien.Value)
                .Select(x => x.AspNetUserId)
                .FirstOrDefaultAsync(cancellationToken);

            query = string.IsNullOrWhiteSpace(employeeUserId)
                ? query.Where(_ => false)
                : query.Where(x => x.NguoiTao == employeeUserId);
        }

        if (!string.IsNullOrWhiteSpace(TuKhoaNhanVien))
        {
            var keyword = TuKhoaNhanVien.Trim();
            var matchedUserIds = await _dbContext.NhanViens
                .AsNoTracking()
                .Where(x =>
                    (x.HoTen != null && x.HoTen.Contains(keyword)) ||
                    (x.Email != null && x.Email.Contains(keyword)) ||
                    x.MaNhanVien.ToString().Contains(keyword))
                .Select(x => x.AspNetUserId)
                .Where(x => x != null)
                .Distinct()
                .ToListAsync(cancellationToken);

            query = matchedUserIds.Count == 0
                ? query.Where(_ => false)
                : query.Where(x => matchedUserIds.Contains(x.NguoiTao));
        }

        var statusGroups = await query
            .GroupBy(x => x.TrangThai ?? "Unknown")
            .Select(g => new
            {
                status = g.Key,
                count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var teamEmployeeQuery = _dbContext.NhanViens.AsNoTracking();
        if (actor.IsManager && !actor.IsAdmin)
        {
            teamEmployeeQuery = teamEmployeeQuery.Where(x => x.MaPhongBan == actor.MaPhongBan);
        }
        else if (actor.IsEmployee && !actor.IsAdmin && !actor.IsManager)
        {
            teamEmployeeQuery = teamEmployeeQuery.Where(x => x.MaNhanVien == actor.MaNhanVien);
        }

        var teamEmployees = await teamEmployeeQuery
            .OrderBy(x => x.HoTen)
            .Select(x => new
            {
                maNhanVien = x.MaNhanVien,
                hoTen = x.HoTen,
                email = x.Email,
                aspNetUserId = x.AspNetUserId
            })
            .Take(500)
            .ToListAsync(cancellationToken);

        var total = await query.CountAsync(cancellationToken);
        var itemsRaw = await query
            .OrderByDescending(x => x.NgayTao ?? DateTime.MinValue)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .Select(x => new BaoCaoDto
            {
                MaBaoCao = x.MaBaoCao,
                TenBaoCao = x.TenBaoCao,
                LoaiBaoCao = x.LoaiBaoCao,
                TenDuAn = x.DuAn != null ? x.DuAn.TenDuAn : null,
                TenPhongBan = x.PhongBan != null ? x.PhongBan.TenPhongBan : null,
                NguoiTao = _dbContext.NhanViens
                    .Where(nv => nv.AspNetUserId == x.NguoiTao)
                    .Select(nv => nv.HoTen)
                    .FirstOrDefault()
                    ?? (x.NguoiTaoNavigation != null ? (x.NguoiTaoNavigation.UserName ?? x.NguoiTaoNavigation.Email) : x.NguoiTao),
                NgayTao = x.NgayTao,
                NgayBatDau = x.NgayBatDau,
                NgayKetThuc = x.NgayKetThuc,
                DefinDang = x.DinhDang,
                TrangThai = x.TrangThai,
                LoaiBaoCaoLabel = x.LoaiBaoCao,
                TrangThaiLabel = x.TrangThai,
                NoiDungPlaceholder = x.NoiDung
            })
            .ToListAsync(cancellationToken);

        var reportIds = itemsRaw.Select(x => x.MaBaoCao).ToList();
        Dictionary<int, string?> recipients = new();
        try
        {
            recipients = await _dbContext.BaoCaoChiTiets
                .AsNoTracking()
                .Where(x => reportIds.Contains(x.MaBaoCao) && x.TieuDe == "NguoiNhanBaoCao")
                .GroupBy(x => x.MaBaoCao)
                .Select(g => new
                {
                    MaBaoCao = g.Key,
                    NguoiNhan = g.OrderByDescending(x => x.MaBaoCaoChiTiet).Select(x => x.DuLieu).FirstOrDefault()
                })
                .ToDictionaryAsync(x => x.MaBaoCao, x => x.NguoiNhan, cancellationToken);
        }
        catch
        {
            // Database may not have BAOCAOCHITIET_PORTAL in older schema.
        }

        var items = itemsRaw.Select(x =>
        {
            x.NguoiNhanBaoCao = recipients.TryGetValue(x.MaBaoCao, out var nguoiNhan) && !string.IsNullOrWhiteSpace(nguoiNhan)
                ? nguoiNhan
                : ExtractRecipientFromNoiDung(x.NoiDungPlaceholder);
            return x;
        }).ToList();

        return Ok(new
        {
            items,
            pageNumber = PageNumber,
            pageSize = PageSize,
            totalItems = total,
            totalPages = total == 0 ? 1 : (int)Math.Ceiling(total / (double)PageSize),
            scopeStats = new
            {
                total,
                submitted = statusGroups.FirstOrDefault(x => x.status == ReportWorkflowStatus.Submitted)?.count ?? 0,
                approved = statusGroups.FirstOrDefault(x => x.status == ReportWorkflowStatus.Approved)?.count ?? 0,
                rejected = statusGroups.FirstOrDefault(x => x.status == ReportWorkflowStatus.Rejected)?.count ?? 0,
                draft = statusGroups.FirstOrDefault(x => x.status == ReportWorkflowStatus.Draft)?.count ?? 0
            },
            teamEmployees
        });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.ReportsManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var actor = await GetActorContextAsync(cancellationToken);
        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Không xác định người dùng."));
        }

        var report = await _dbContext.BaoCaos.FirstOrDefaultAsync(x => x.MaBaoCao == id && !x.IsDeleted, cancellationToken);
        if (report == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy báo cáo."));
        }

        if (!actor.IsAdmin && report.NguoiTao != actor.UserId && !(actor.IsManager && report.MaPhongBan == actor.MaPhongBan))
        {
            return Forbid();
        }

        report.IsDeleted = true;
        report.NgayCapNhat = DateTime.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }, "Đã xóa báo cáo."));
    }

    [HttpGet("kpi-summary")]
    [Authorize(Policy = Permissions.ReportsView)]
    public async Task<ActionResult<ApiResponse<object>>> GetKpiSummary(
        [FromQuery] DateTime? NgayBatDau,
        [FromQuery] DateTime? NgayKetThuc,
        [FromQuery] int? MaPhongBan,
        CancellationToken cancellationToken)
    {
        var actor = await GetActorContextAsync(cancellationToken);
        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Không xác định người dùng."));
        }

        var employeeQuery = _dbContext.NhanViens
            .AsNoTracking()
            .Where(x => (x.TrangThai ?? 1) == 1);

        if (actor.IsManager && !actor.IsAdmin)
        {
            employeeQuery = employeeQuery.Where(x => x.MaPhongBan == actor.MaPhongBan);
        }
        else if (actor.IsEmployee && !actor.IsAdmin && !actor.IsManager)
        {
            employeeQuery = employeeQuery.Where(x => x.MaNhanVien == actor.MaNhanVien);
        }

        if (MaPhongBan.HasValue && MaPhongBan.Value > 0)
        {
            employeeQuery = employeeQuery.Where(x => x.MaPhongBan == MaPhongBan.Value);
        }

        var employeeIds = await employeeQuery.Select(x => x.MaNhanVien).ToListAsync(cancellationToken);
        if (employeeIds.Count == 0)
        {
            return Ok(ApiResponse<object>.Ok(new
            {
                kpiTrungBinh = 0d,
                tyLeDatKpi = 0d,
                soNhanVien = 0,
                soNhanVienDatKpi = 0,
                taskDungHan = 0,
                taskTreHan = 0
            }));
        }

        var from = NgayBatDau?.Date;
        var to = NgayKetThuc?.Date;
        int? fromYm = null;
        int? toYm = null;
        if (from.HasValue && to.HasValue && from <= to)
        {
            fromYm = (from.Value.Year * 100) + from.Value.Month;
            toYm = (to.Value.Year * 100) + to.Value.Month;
        }

        var kpiQuery = _dbContext.KetQuaKpis
            .AsNoTracking()
            .Where(x => x.MaKpi == 1 && employeeIds.Contains(x.MaNhanVien));

        if (fromYm.HasValue && toYm.HasValue)
        {
            kpiQuery = kpiQuery.Where(x => x.thang.HasValue
                                           && x.nam.HasValue
                                           && (((x.nam.Value * 100) + x.thang.Value) >= fromYm.Value)
                                           && (((x.nam.Value * 100) + x.thang.Value) <= toYm.Value));
        }

        var kpiRows = await kpiQuery
            .Select(x => new
            {
                x.MaNhanVien,
                Score = (double)(x.DiemSo ?? 0)
            })
            .ToListAsync(cancellationToken);

        var kpiTrungBinh = kpiRows.Count == 0 ? 0 : kpiRows.Average(x => x.Score);
        var nvDatKpi = kpiRows.Select(x => x.MaNhanVien).Distinct().Count(id =>
        {
            var avg = kpiRows.Where(r => r.MaNhanVien == id).Average(r => r.Score);
            return avg >= 70;
        });

        var taskQuery = from pc in _dbContext.PhanCongNhanViens.AsNoTracking()
                        join cv in _dbContext.CongViecs.AsNoTracking() on pc.MaCongViec equals cv.MaCongViec
                        where employeeIds.Contains(pc.MaNhanVien)
                              && !(cv.DaXoa ?? false)
                              && cv.HanHoanThanh.HasValue
                        select new
                        {
                            cv.MaCongViec,
                            cv.MaTrangThai,
                            cv.HanHoanThanh
                        };

        if (from.HasValue)
        {
            taskQuery = taskQuery.Where(x => x.HanHoanThanh!.Value.Date >= from.Value);
        }

        if (to.HasValue)
        {
            taskQuery = taskQuery.Where(x => x.HanHoanThanh!.Value.Date <= to.Value);
        }

        var tasks = await taskQuery.Distinct().ToListAsync(cancellationToken);
        var today = DateTime.Today;
        var taskTreHan = tasks.Count(x => (x.MaTrangThai ?? 0) == 4 || ((x.MaTrangThai ?? 0) != 3 && x.HanHoanThanh.HasValue && x.HanHoanThanh.Value.Date < today));
        var taskDungHan = tasks.Count(x => (x.MaTrangThai ?? 0) == 3) - tasks.Count(x => (x.MaTrangThai ?? 0) == 4);
        if (taskDungHan < 0) taskDungHan = 0;

        return Ok(ApiResponse<object>.Ok(new
        {
            kpiTrungBinh = Math.Round(kpiTrungBinh, 2),
            tyLeDatKpi = kpiRows.Count == 0 ? 0 : Math.Round((nvDatKpi / (double)employeeIds.Count) * 100, 2),
            soNhanVien = employeeIds.Count,
            soNhanVienDatKpi = nvDatKpi,
            taskDungHan,
            taskTreHan
        }));
    }

    [HttpGet("detail/{id:int}")]
    [Authorize(Policy = Permissions.ReportsView)]
    public async Task<ActionResult<ApiResponse<BaoCaoDto>>> Detail(int id, CancellationToken cancellationToken)
    {
        var actor = await GetActorContextAsync(cancellationToken);
        if (actor == null)
        {
            return Unauthorized(ApiResponse<BaoCaoDto>.Fail("Không xác định người dùng."));
        }

        var report = await _dbContext.BaoCaos
            .AsNoTracking()
            .Include(x => x.DuAn)
            .Include(x => x.PhongBan)
            .Include(x => x.NguoiTaoNavigation)
            .FirstOrDefaultAsync(x => x.MaBaoCao == id && !x.IsDeleted, cancellationToken);

        if (report == null)
        {
            return NotFound(ApiResponse<BaoCaoDto>.Fail("Không tìm thấy báo cáo."));
        }

        if (!actor.IsAdmin && actor.IsManager)
        {
            var isEmployeeReport = await _dbContext.UserRoles
                .AsNoTracking()
                .AnyAsync(ur => ur.UserId == report.NguoiTao &&
                                _dbContext.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Employee), cancellationToken);
            if (!(report.MaPhongBan == actor.MaPhongBan && isEmployeeReport))
            {
                return Forbid();
            }
        }
        else if (!actor.IsAdmin && report.NguoiTao != actor.UserId)
        {
            return Forbid();
        }

        string? recipient = null;
        try
        {
            recipient = await _dbContext.BaoCaoChiTiets
                .AsNoTracking()
                .Where(x => x.MaBaoCao == report.MaBaoCao && x.TieuDe == "NguoiNhanBaoCao")
                .OrderByDescending(x => x.MaBaoCaoChiTiet)
                .Select(x => x.DuLieu)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch
        {
            // Database may not have BAOCAOCHITIET_PORTAL in older schema.
        }
        recipient ??= ExtractRecipientFromNoiDung(report.NoiDung);
        var creatorName = await _dbContext.NhanViens
            .AsNoTracking()
            .Where(nv => nv.AspNetUserId == report.NguoiTao)
            .Select(nv => nv.HoTen)
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(ApiResponse<BaoCaoDto>.Ok(new BaoCaoDto
        {
            MaBaoCao = report.MaBaoCao,
            TenBaoCao = report.TenBaoCao,
            LoaiBaoCao = report.LoaiBaoCao,
            TenDuAn = report.DuAn?.TenDuAn,
            TenPhongBan = report.PhongBan?.TenPhongBan,
            NguoiTao = creatorName ?? report.NguoiTaoNavigation?.UserName ?? report.NguoiTaoNavigation?.Email ?? report.NguoiTao,
            NguoiNhanBaoCao = recipient,
            NgayTao = report.NgayTao,
            NgayBatDau = report.NgayBatDau,
            NgayKetThuc = report.NgayKetThuc,
                DefinDang = report.DinhDang,
                TrangThai = report.TrangThai,
                LoaiBaoCaoLabel = report.LoaiBaoCao,
                TrangThaiLabel = report.TrangThai,
                NoiDung = report.NoiDung
        }));
    }

    [HttpPost("request/create")]
    [Authorize(Policy = Permissions.ReportsRequest)]
    public async Task<ActionResult<ApiResponse<object>>> CreateRequest([FromBody] CreateReportRequestDto request, CancellationToken cancellationToken)
    {
        var actor = await GetActorContextAsync(cancellationToken);
        if (actor == null || string.IsNullOrWhiteSpace(actor.UserId))
        {
            return Unauthorized(ApiResponse<object>.Fail("Không xác định người dùng."));
        }

        if (string.IsNullOrWhiteSpace(request.NguoiNhanUserId) || string.IsNullOrWhiteSpace(request.TieuDe))
        {
            return BadRequest(ApiResponse<object>.Fail("Thiếu người nhận hoặc tiêu đề."));
        }

        var entity = new YeuCauBaoCao
        {
            NguoiYeuCau = actor.UserId,
            NguoiNhanYeuCau = request.NguoiNhanUserId,
            TieuDe = request.TieuDe?.Trim(),
            MoTa = request.MoTa,
            Priority = request.Priority ?? "normal",
            HanChot = request.HanChot,
            TrangThai = ReportWorkflowStatus.Draft,
            NgayTao = DateTime.Now,
            NgayCapNhat = DateTime.Now,
            IsDeleted = false
        };
        _dbContext.YeuCauBaoCaos.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await PushNotificationToUserIdAsync(entity.NguoiNhanYeuCau, $"Bạn có yêu cầu báo cáo mới: {entity.TieuDe}", cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { entity.MaYeuCau }, "Tạo yêu cầu thành công."));
    }

    [HttpGet("request/list")]
    [Authorize(Policy = Permissions.ReportsView)]
    public async Task<ActionResult<ApiResponse<List<ReportRequestDto>>>> RequestList(CancellationToken cancellationToken)
    {
        var actor = await GetActorContextAsync(cancellationToken);
        if (actor == null)
        {
            return Unauthorized(ApiResponse<List<ReportRequestDto>>.Fail("Không xác định người dùng."));
        }

        var query = _dbContext.YeuCauBaoCaos.AsNoTracking().Where(x => !x.IsDeleted);
        if (!actor.IsAdmin)
        {
            query = query.Where(x => x.NguoiYeuCau == actor.UserId || x.NguoiNhanYeuCau == actor.UserId);
        }

        var items = await query.OrderByDescending(x => x.NgayTao ?? DateTime.MinValue)
            .Select(x => new ReportRequestDto
            {
                MaYeuCau = x.MaYeuCau,
                TieuDe = x.TieuDe,
                MoTa = x.MoTa,
                Priority = x.Priority,
                HanChot = x.HanChot,
                TrangThai = x.TrangThai,
                NgayTao = x.NgayTao,
                NguoiYeuCau = x.NguoiYeuCauNavigation != null ? x.NguoiYeuCauNavigation.UserName : x.NguoiYeuCau
            })
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<List<ReportRequestDto>>.Ok(items));
    }

    [HttpGet("request/detail/{id:int}")]
    [Authorize(Policy = Permissions.ReportsView)]
    public async Task<ActionResult<ApiResponse<ReportRequestDto>>> RequestDetail(int id, CancellationToken cancellationToken)
    {
        var actor = await GetActorContextAsync(cancellationToken);
        if (actor == null)
        {
            return Unauthorized(ApiResponse<ReportRequestDto>.Fail("Không xác định người dùng."));
        }

        var row = await _dbContext.YeuCauBaoCaos
            .AsNoTracking()
            .Where(x => x.MaYeuCau == id && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (row == null)
        {
            return NotFound(ApiResponse<ReportRequestDto>.Fail("Không tìm thấy yêu cầu."));
        }
        if (!actor.IsAdmin && row.NguoiYeuCau != actor.UserId && row.NguoiNhanYeuCau != actor.UserId)
        {
            return Forbid();
        }

        var dto = new ReportRequestDto
        {
            MaYeuCau = row.MaYeuCau,
            TieuDe = row.TieuDe,
            MoTa = row.MoTa,
            Priority = row.Priority,
            HanChot = row.HanChot,
            TrangThai = row.TrangThai,
            NgayTao = row.NgayTao,
            NguoiYeuCau = row.NguoiYeuCau
        };
        return Ok(ApiResponse<ReportRequestDto>.Ok(dto));
    }

    [HttpPut("request/cancel/{id:int}")]
    [Authorize(Policy = Permissions.ReportsRequest)]
    public async Task<ActionResult<ApiResponse<object>>> RequestCancel(int id, CancellationToken cancellationToken)
    {
        var actor = await GetActorContextAsync(cancellationToken);
        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Không xác định người dùng."));
        }

        var row = await _dbContext.YeuCauBaoCaos.FirstOrDefaultAsync(x => x.MaYeuCau == id && !x.IsDeleted, cancellationToken);
        if (row == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy yêu cầu."));
        }
        if (!actor.IsAdmin && row.NguoiYeuCau != actor.UserId)
        {
            return Forbid();
        }

        row.TrangThai = ReportWorkflowStatus.Cancelled;
        row.NgayCapNhat = DateTime.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { id }, "Đã hủy yêu cầu báo cáo."));
    }

    [HttpPut("review/approve")]
    [Authorize(Policy = Permissions.ReportsReview)]
    public async Task<ActionResult<ApiResponse<object>>> Approve([FromBody] ReportReviewRequest request, CancellationToken cancellationToken)
    {
        return await ReviewInternal(request, ReportWorkflowStatus.Approved, cancellationToken);
    }

    [HttpPut("review/reject")]
    [Authorize(Policy = Permissions.ReportsReview)]
    public async Task<ActionResult<ApiResponse<object>>> Reject([FromBody] ReportReviewRequest request, CancellationToken cancellationToken)
    {
        return await ReviewInternal(request, ReportWorkflowStatus.Rejected, cancellationToken);
    }

    [HttpPost("export-excel")]
    [Authorize(Policy = Permissions.ReportsExport)]
    public IActionResult ExportExcel([FromBody] SaveReportDraftRequest request)
    {
        var parsed = ParseReportSections(request.NoiDung);
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("BaoCao");
        ws.Cell(1, 1).Value = "Tiêu đề";
        ws.Cell(1, 2).Value = request.TenBaoCao ?? "Báo cáo";
        ws.Cell(2, 1).Value = "Loại";
        ws.Cell(2, 2).Value = ToVietnameseReportType(request.LoaiBaoCao);
        ws.Cell(3, 1).Value = "Người làm báo cáo";
        ws.Cell(3, 2).Value = request.NguoiTao ?? "-";
        ws.Cell(4, 1).Value = "Người nhận báo cáo";
        ws.Cell(4, 2).Value = request.NguoiNhanBaoCao ?? "-";
        ws.Cell(5, 1).Value = "Thời gian tạo";
        ws.Cell(5, 2).Value = request.NgayTao?.ToString("dd/MM/yyyy HH:mm") ?? "-";
        ws.Cell(6, 1).Value = "Từ ngày";
        ws.Cell(6, 2).Value = request.NgayBatDau?.ToString("dd/MM/yyyy") ?? "-";
        ws.Cell(7, 1).Value = "Đến ngày";
        ws.Cell(7, 2).Value = request.NgayKetThuc?.ToString("dd/MM/yyyy") ?? "-";
        ws.Cell(9, 1).Value = "Nội dung";
        ws.Cell(10, 1).Value = "Công việc đã hoàn thành";
        ws.Cell(10, 2).Value = parsed.Completed;
        ws.Cell(11, 1).Value = "Công việc đang thực hiện";
        ws.Cell(11, 2).Value = parsed.Ongoing;
        ws.Cell(12, 1).Value = "Khó khăn / Vướng mắc";
        ws.Cell(12, 2).Value = parsed.Challenges;
        ws.Cell(13, 1).Value = "Đề xuất hỗ trợ";
        ws.Cell(13, 2).Value = parsed.Support;
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "BaoCao.xlsx");
    }

    [HttpPost("export-pdf")]
    [Authorize(Policy = Permissions.ReportsExport)]
    public IActionResult ExportPdf([FromBody] SaveReportDraftRequest request)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var parsed = ParseReportSections(request.NoiDung);
        var typeLabel = ToVietnameseReportType(request.LoaiBaoCao);
        var periodText = request.NgayBatDau.HasValue && request.NgayKetThuc.HasValue
            ? $"{request.NgayBatDau:dd/MM/yyyy} - {request.NgayKetThuc:dd/MM/yyyy}"
            : "-";

        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(12));
                page.Content().Column(col =>
                {
                    col.Item().Text(request.TenBaoCao ?? "Báo cáo").FontSize(20).Bold();
                    col.Item().Text($"Loại báo cáo: {typeLabel}");
                    col.Item().Text($"Người làm báo cáo: {request.NguoiTao ?? "-"}");
                    col.Item().Text($"Người nhận báo cáo: {request.NguoiNhanBaoCao ?? "-"}");
                    col.Item().Text($"Thời gian tạo: {(request.NgayTao.HasValue ? request.NgayTao.Value.ToString("dd/MM/yyyy HH:mm") : "-")}");
                    col.Item().Text($"Khoảng thời gian báo cáo: {periodText}");
                    col.Item().PaddingTop(15).Text("Nội dung báo cáo").Bold();
                    col.Item().PaddingTop(6).Text($"- Công việc đã hoàn thành: {parsed.Completed}");
                    col.Item().Text($"- Công việc đang thực hiện: {parsed.Ongoing}");
                    col.Item().Text($"- Khó khăn / Vướng mắc: {parsed.Challenges}");
                    col.Item().Text($"- Đề xuất hỗ trợ: {parsed.Support}");
                    col.Item().PaddingTop(20).Text($"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}");
                });
            });
        }).GeneratePdf();

        return File(bytes, "application/pdf", "BaoCao.pdf");
    }

    private static string ToVietnameseReportType(string? type)
    {
        return (type ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "personal" => "Báo cáo cá nhân",
            "project" => "Báo cáo dự án",
            "department" => "Báo cáo phòng ban",
            "ai" => "Bao cao AI",
            "admin" => "Báo cáo quản trị",
            "daily" => "Báo cáo hằng ngày",
            "weekly" => "Báo cáo hằng tuần",
            "monthly" => "Báo cáo hằng tháng",
            "quarterly" => "Báo cáo hằng quý",
            "yearly" => "Báo cáo hằng năm",
            _ => string.IsNullOrWhiteSpace(type) ? "-" : type.Trim()
        };
    }

    private static (string Completed, string Ongoing, string Challenges, string Support) ParseReportSections(string? rawContent)
    {
        const string EmptyValue = "-";
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return (EmptyValue, EmptyValue, EmptyValue, EmptyValue);
        }

        try
        {
            using var document = JsonDocument.Parse(rawContent);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return (rawContent.Trim(), EmptyValue, EmptyValue, EmptyValue);
            }

            string Read(params string[] names)
            {
                foreach (var name in names)
                {
                    if (root.TryGetProperty(name, out var value))
                    {
                        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return text.Trim();
                        }
                    }
                }
                return EmptyValue;
            }

            return (
                Read("completed", "completedWork", "daHoanThanh", "done"),
                Read("ongoing", "ongoingWork", "dangThucHien", "inProgress"),
                Read("challenges", "khoKhan", "vuongMac", "issues"),
                Read("support", "suggestions", "deXuatHoTro", "proposals")
            );
        }
        catch
        {
            return (rawContent.Trim(), EmptyValue, EmptyValue, EmptyValue);
        }
    }

    private async Task<ActionResult<ApiResponse<object>>> ReviewInternal(ReportReviewRequest request, string status, CancellationToken cancellationToken)
    {
        var actor = await GetActorContextAsync(cancellationToken);
        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Không xác định người dùng."));
        }

        var report = await _dbContext.BaoCaos.FirstOrDefaultAsync(x => x.MaBaoCao == request.MaBaoCao && !x.IsDeleted, cancellationToken);
        if (report == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy báo cáo."));
        }
        if (!actor.IsAdmin && actor.IsManager)
        {
            var isEmployeeReport = await _dbContext.UserRoles
                .AsNoTracking()
                .AnyAsync(ur => ur.UserId == report.NguoiTao &&
                                _dbContext.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Employee), cancellationToken);
            if (!(report.MaPhongBan == actor.MaPhongBan && isEmployeeReport))
            {
                return Forbid();
            }
        }
        else if (!actor.IsAdmin)
        {
            return Forbid();
        }

        report.TrangThai = status;
        report.NgayCapNhat = DateTime.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await PushNotificationForReportAsync(report, $"Báo cáo \"{report.TenBaoCao}\" đã được {(status == ReportWorkflowStatus.Approved ? "duyệt" : "từ chối")}.", cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { report.MaBaoCao, report.TrangThai, request.Note }));
    }

    private async Task<ActorContext?> GetActorContextAsync(CancellationToken cancellationToken)
    {
        EnsureDbConnectionStringInitialized();

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var actor = await _dbContext.NhanViens.AsNoTracking()
            .Where(x => x.AspNetUserId == userId)
            .Select(x => new ActorContext
            {
                UserId = userId,
                MaNhanVien = x.MaNhanVien,
                MaPhongBan = x.MaPhongBan,
                IsAdmin = User.IsInRole(Roles.Admin),
                IsManager = User.IsInRole(Roles.Manager),
                IsEmployee = User.IsInRole(Roles.Employee)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (actor != null)
        {
            return actor;
        }

        var claimId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(claimId, out var maNhanVien) && maNhanVien > 0)
        {
            actor = await _dbContext.NhanViens.AsNoTracking()
                .Where(x => x.MaNhanVien == maNhanVien)
                .Select(x => new ActorContext
                {
                    UserId = x.AspNetUserId ?? userId,
                    MaNhanVien = x.MaNhanVien,
                    MaPhongBan = x.MaPhongBan,
                    IsAdmin = User.IsInRole(Roles.Admin),
                    IsManager = User.IsInRole(Roles.Manager),
                    IsEmployee = User.IsInRole(Roles.Employee)
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        return actor;
    }

    private async Task PushNotificationForReportAsync(BaoCao report, string message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(report.NguoiTao))
        {
            return;
        }

        await PushNotificationToUserIdAsync(report.NguoiTao, message, cancellationToken);
    }

    private async Task PushNotificationToUsersAsync(IEnumerable<string> userIds, string message, CancellationToken cancellationToken)
    {
        foreach (var userId in userIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await PushNotificationToUserIdAsync(userId, message, cancellationToken);
        }
    }

    private async Task<string?> ResolveRecipientLabelAsync(string? nguoiNhanUserId, string? nguoiNhanBaoCao, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(nguoiNhanBaoCao))
        {
            return nguoiNhanBaoCao.Trim();
        }

        if (string.IsNullOrWhiteSpace(nguoiNhanUserId))
        {
            return null;
        }

        var manager = await _dbContext.NhanViens
            .AsNoTracking()
            .Where(x => x.AspNetUserId == nguoiNhanUserId)
            .Select(x => x.HoTen)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(manager) ? null : manager.Trim();
    }

    private async Task TrySaveRecipientMetadataAsync(int maBaoCao, string recipientLabel, CancellationToken cancellationToken)
    {
        try
        {
            _dbContext.BaoCaoChiTiets.Add(new BaoCaoChiTiet
            {
                MaBaoCao = maBaoCao,
                TieuDe = "NguoiNhanBaoCao",
                DuLieu = recipientLabel,
                ThuTu = 1
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Ignore metadata save issues for compatibility with older schemas.
        }
    }

    private static string? ExtractRecipientFromNoiDung(string? noiDung)
    {
        if (string.IsNullOrWhiteSpace(noiDung))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(noiDung);
            if (doc.RootElement.TryGetProperty("recipientName", out var recipient))
            {
                return recipient.GetString();
            }
        }
        catch
        {
            // ignore json parse errors
        }

        return null;
    }

    private async Task PushNotificationToUserIdAsync(string? userId, string message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var employee = await _dbContext.NhanViens
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AspNetUserId == userId, cancellationToken);
        if (employee == null && int.TryParse(userId, out var maNhanVien) && maNhanVien > 0)
        {
            employee = await _dbContext.NhanViens
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.MaNhanVien == maNhanVien, cancellationToken);
        }
        if (employee == null)
        {
            return;
        }

        var loai = await _dbContext.LoaiThongBaos
            .FirstOrDefaultAsync(x =>
                x.TenLoai == ReportNotificationTypeKey
                || x.TenLoai == "Báo cáo"
                || x.TenLoai == "Bao cao",
                cancellationToken);
        if (loai == null)
        {
            loai = new LoaiThongBao
            {
                TenLoai = ReportNotificationTypeKey
            };
            _dbContext.LoaiThongBaos.Add(loai);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var thongBao = new ThongBao
        {
            MaLoai = loai.MaLoai,
            NoiDung = message,
            ThoiGian = DateTime.Now
        };
        _dbContext.ThongBaos.Add(thongBao);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _dbContext.ThongBaoNhanViens.Add(new ThongBaoNhanVien
        {
            MaThongBao = thongBao.MaThongBao,
            MaNhanVien = employee.MaNhanVien,
            DaDoc = false
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
