using LuanVan.Contracts;
using LuanVan.Data;
using LuanVan.Models;
using LuanVan.Services;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LuanVan.Controllers.Api;

[Authorize]
[ApiController]
[Route("kpi")]
[Route("api/kpi")]
public class KpiController : ControllerBase
{
    private readonly IKpiService _kpiService;
    private readonly AppDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;

    public KpiController(
        IKpiService kpiService,
        AppDbContext dbContext,
        IAuditLogService auditLogService,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        _kpiService = kpiService;
        _dbContext = dbContext;
        _auditLogService = auditLogService;
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

    [Authorize(Policy = Permissions.KpiView)]
    [HttpGet("catalog")]
    public async Task<ActionResult<ApiResponse<KpiCatalogListDto>>> GetCatalog(
        [FromQuery] string? keyword,
        [FromQuery] int? maLoaiKpi,
        [FromQuery] int? thang,
        [FromQuery] int? nam,
        [FromQuery] int page = 1,
        [FromQuery] int size = 50)
    {
        EnsureDbConnectionStringInitialized();
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 200);

        IQueryable<Models.DanhMucKpi> query = _dbContext.DanhMucKpis
            .AsNoTracking()
            .Include(x => x.LoaiKpi);

        if (maLoaiKpi.HasValue)
        {
            query = query.Where(x => x.MaLoaiKpi == maLoaiKpi.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(x => x.TenKpi != null && EF.Functions.Like(EF.Functions.Collate(x.TenKpi, "Latin1_General_CI_AI"), $"%{k}%"));
        }

        var totalItems = await query.CountAsync();
        var totalTrongSo = await query
            .Where(x => (x.TrangThai ?? 1) == 1)
            .SumAsync(x => x.TrongSoGoc ?? 0);

        var pageRows = await query
            .OrderBy(x => x.MaKpi)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(x => new
            {
                x.MaKpi,
                x.TenKpi,
                x.TrongSoGoc,
                x.MaLoaiKpi,
                x.TrangThai,
                TenLoai = x.LoaiKpi.TenLoaiKpi
            })
            .ToListAsync();

        var kpiIds = pageRows.Select(x => x.MaKpi).ToList();

        List<KpiAssignmentCountRow> nvCounts;
        List<KpiAssignmentCountRow> nhomCounts;
        List<KpiAssignmentCountRow> duAnCounts;
        List<KpiAssignmentCountRow> phongBanCounts;

        try
        {
            nvCounts = await _dbContext.KpiNhanViens.AsNoTracking()
                .Where(x => kpiIds.Contains(x.MaKpi) && x.IsActive)
                .GroupBy(x => x.MaKpi)
                .Select(g => new KpiAssignmentCountRow { MaKpi = g.Key, Count = g.Count() })
                .ToListAsync();

            nhomCounts = await _dbContext.KpiNhoms.AsNoTracking()
                .Where(x => kpiIds.Contains(x.MaKpi) && x.IsActive)
                .GroupBy(x => x.MaKpi)
                .Select(g => new KpiAssignmentCountRow { MaKpi = g.Key, Count = g.Count() })
                .ToListAsync();

            duAnCounts = await _dbContext.KpiDuAns.AsNoTracking()
                .Where(x => kpiIds.Contains(x.MaKpi) && x.IsActive)
                .GroupBy(x => x.MaKpi)
                .Select(g => new KpiAssignmentCountRow { MaKpi = g.Key, Count = g.Count() })
                .ToListAsync();

            phongBanCounts = await _dbContext.KpiPhongBans.AsNoTracking()
                .Where(x => kpiIds.Contains(x.MaKpi) && x.IsActive)
                .GroupBy(x => x.MaKpi)
                .Select(g => new KpiAssignmentCountRow { MaKpi = g.Key, Count = g.Count() })
                .ToListAsync();
        }
        catch (Exception ex) when (IsMissingIsActiveColumnError(ex))
        {
            nvCounts = await _dbContext.KpiNhanViens.AsNoTracking()
                .Where(x => kpiIds.Contains(x.MaKpi))
                .GroupBy(x => x.MaKpi)
                .Select(g => new KpiAssignmentCountRow { MaKpi = g.Key, Count = g.Count() })
                .ToListAsync();

            nhomCounts = await _dbContext.KpiNhoms.AsNoTracking()
                .Where(x => kpiIds.Contains(x.MaKpi))
                .GroupBy(x => x.MaKpi)
                .Select(g => new KpiAssignmentCountRow { MaKpi = g.Key, Count = g.Count() })
                .ToListAsync();

            duAnCounts = await _dbContext.KpiDuAns.AsNoTracking()
                .Where(x => kpiIds.Contains(x.MaKpi))
                .GroupBy(x => x.MaKpi)
                .Select(g => new KpiAssignmentCountRow { MaKpi = g.Key, Count = g.Count() })
                .ToListAsync();

            phongBanCounts = await _dbContext.KpiPhongBans.AsNoTracking()
                .Where(x => kpiIds.Contains(x.MaKpi))
                .GroupBy(x => x.MaKpi)
                .Select(g => new KpiAssignmentCountRow { MaKpi = g.Key, Count = g.Count() })
                .ToListAsync();
        }

        var assignmentCountByKpi = nvCounts
            .Concat(nhomCounts)
            .Concat(duAnCounts)
            .Concat(phongBanCounts)
            .GroupBy(x => x.MaKpi)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Count));

        var resultRows = _dbContext.KetQuaKpis
            .AsNoTracking()
            .Where(x => kpiIds.Contains(x.MaKpi));

        if (thang.HasValue)
        {
            resultRows = resultRows.Where(x => x.thang == thang.Value);
        }

        if (nam.HasValue)
        {
            resultRows = resultRows.Where(x => x.nam == nam.Value);
        }

        var avgByKpi = await resultRows
            .GroupBy(x => x.MaKpi)
            .Select(g => new { MaKpi = g.Key, DiemTrungBinh = g.Average(x => x.DiemSo ?? 0) })
            .ToDictionaryAsync(x => x.MaKpi, x => x.DiemTrungBinh);

        var items = pageRows.Select(x => new KpiCatalogItemDto
        {
            MaKpi = x.MaKpi,
            TenKpi = x.TenKpi,
            MoTa = $"KPI {x.TenKpi}",
            TrongSo = (double)(x.TrongSoGoc ?? 0),
            TrongSoGoc = (double)(x.TrongSoGoc ?? 0),
            MaLoaiKpi = x.MaLoaiKpi,
            TenLoaiKpi = x.TenLoai,
            ApDung = x.MaLoaiKpi == 1 ? "Cá nhân" : "Phòng ban",
            TrangThai = GetKpiStatusLabel(x.TrangThai),
            SoDoiTuong = assignmentCountByKpi.GetValueOrDefault(x.MaKpi, 0),
            DiemTrungBinh = (double)avgByKpi.GetValueOrDefault(x.MaKpi, 0)
        }).ToList();

        var payload = new KpiCatalogListDto
        {
            Items = items,
            Page = page,
            Size = size,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)size),
            TongTrongSo = (double)Math.Round(totalTrongSo, 2)
        };

        return Ok(ApiResponse<KpiCatalogListDto>.Ok(payload));
    }

    [Authorize(Policy = Permissions.KpiView)]
    [HttpGet("export")]
    public async Task<IActionResult> ExportCatalog(
        [FromQuery] string? keyword,
        [FromQuery] int? maLoaiKpi,
        [FromQuery] int? thang,
        [FromQuery] int? nam,
        [FromQuery] string? type,
        [FromQuery] string? roleScope)
    {
        var scope = await ResolveActorScopeAsync();
        if (!scope.Success)
        {
            return StatusCode(scope.StatusCode, scope.ErrorMessage);
        }

        // Nếu tài khoản có nhiều role (vd: vừa Admin vừa Manager),
        // cho phép màn hình yêu cầu export theo scope role đang chọn.
        var requestedRoleScope = roleScope?.Trim().ToLowerInvariant();
        if (requestedRoleScope == "manager" && User.IsInRole(Roles.Manager))
        {
            if (!scope.MaPhongBan.HasValue)
            {
                return StatusCode(StatusCodes.Status403Forbidden, "Tài khoản quản lý chưa gán phòng ban.");
            }
            scope = (true, StatusCodes.Status200OK, string.Empty, "manager", scope.MaNhanVien, scope.MaPhongBan);
        }
        else if (requestedRoleScope == "admin" && User.IsInRole(Roles.Admin))
        {
            scope = (true, StatusCodes.Status200OK, string.Empty, "admin", scope.MaNhanVien, scope.MaPhongBan);
        }

        var targetMonth = thang is >= 1 and <= 12 ? thang.Value : DateTime.Now.Month;
        var targetYear = nam is > 2000 and <= 3000 ? nam.Value : DateTime.Now.Year;
        var exportType = string.Equals(type, "detail", StringComparison.OrdinalIgnoreCase) ? "detail" : "overview";

        IQueryable<Models.DanhMucKpi> query = _dbContext.DanhMucKpis
            .AsNoTracking()
            .Include(x => x.LoaiKpi);

        if (maLoaiKpi.HasValue)
        {
            query = query.Where(x => x.MaLoaiKpi == maLoaiKpi.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(x => x.TenKpi != null && EF.Functions.Like(EF.Functions.Collate(x.TenKpi, "Latin1_General_CI_AI"), $"%{k}%"));
        }

        var allCatalogRows = await query
            .OrderBy(x => x.MaKpi)
            .Select(x => new
            {
                x.MaKpi,
                x.TenKpi,
                x.TrongSoGoc,
                x.MaLoaiKpi,
                x.TrangThai,
                TenLoai = x.LoaiKpi.TenLoaiKpi
            })
            .ToListAsync();

        if (allCatalogRows.Count == 0)
        {
            return exportType == "detail"
                ? BuildKpiDetailExcelFile(new List<KpiExportDetailRowDto>(), targetMonth, targetYear)
                : BuildKpiOverviewExcelFile(new List<KpiExportOverviewRowDto>(), targetMonth, targetYear);
        }

        var kpiIds = allCatalogRows.Select(x => x.MaKpi).ToList();
        var kpiLookup = allCatalogRows.ToDictionary(x => x.MaKpi);

        List<int> scopedEmployeeIds = new();
        if (scope.RoleKey == "manager" && scope.MaPhongBan.HasValue)
        {
            scopedEmployeeIds = await _dbContext.NhanViens
                .AsNoTracking()
                .Where(x => (x.TrangThai ?? 1) == 1 && x.MaPhongBan == scope.MaPhongBan.Value)
                .Select(x => x.MaNhanVien)
                .ToListAsync();
        }

        var nvAssignments = await _dbContext.KpiNhanViens
            .AsNoTracking()
            .Where(x => kpiIds.Contains(x.MaKpi))
            .Select(x => new
            {
                x.MaKpi,
                x.MaNhanVien,
                HoTen = x.NhanVien.HoTen,
                x.TuNgay,
                x.DenNgay,
                x.IsActive,
                x.TrongSoApDung
            })
            .ToListAsync();

        var teamMembershipRows = await (
            from tv in _dbContext.ThanhVienNhoms.AsNoTracking()
            join nv in _dbContext.NhanViens.AsNoTracking() on tv.MaNhanVien equals nv.MaNhanVien
            where (nv.TrangThai ?? 1) == 1
            select new { tv.MaNhom, tv.MaNhanVien, nv.MaPhongBan })
            .ToListAsync();

        var nhomAssignments = await _dbContext.KpiNhoms
            .AsNoTracking()
            .Where(x => kpiIds.Contains(x.MaKpi))
            .Select(x => new
            {
                x.MaKpi,
                x.MaNhom,
                TenNhom = x.Nhom.TenNhom,
                x.TuNgay,
                x.DenNgay,
                x.IsActive,
                x.TrongSoApDung
            })
            .ToListAsync();

        var phongBanAssignments = await _dbContext.KpiPhongBans
            .AsNoTracking()
            .Where(x => kpiIds.Contains(x.MaKpi))
            .Select(x => new
            {
                x.MaKpi,
                x.MaPhongBan,
                TenPhongBan = x.PhongBan.TenPhongBan,
                x.TuNgay,
                x.DenNgay,
                x.IsActive,
                x.TrongSoApDung
            })
            .ToListAsync();

        var duAnAssignments = await _dbContext.KpiDuAns
            .AsNoTracking()
            .Where(x => kpiIds.Contains(x.MaKpi))
            .Select(x => new
            {
                x.MaKpi,
                x.MaDuAn,
                TenDuAn = x.DuAn.TenDuAn,
                x.TuNgay,
                x.DenNgay,
                x.IsActive,
                x.TrongSoApDung
            })
            .ToListAsync();

        var duAnNhanVienRows = await _dbContext.DuAnNhanViens
            .AsNoTracking()
            .Select(x => new { x.MaDuAn, x.MaNhanVien })
            .ToListAsync();

        var resultRows = await _dbContext.KetQuaKpis
            .AsNoTracking()
            .Where(x => kpiIds.Contains(x.MaKpi) && x.thang == targetMonth && x.nam == targetYear)
            .Select(x => new { x.MaKpi, x.MaNhanVien, Diem = x.DiemSo.HasValue ? (double)x.DiemSo.Value : 0.0 })
            .ToListAsync();

        if (scope.RoleKey == "manager" && scope.MaPhongBan.HasValue)
        {
            var scopedSet = scopedEmployeeIds.ToHashSet();

            nvAssignments = nvAssignments.Where(x => scopedSet.Contains(x.MaNhanVien)).ToList();
            resultRows = resultRows.Where(x => scopedSet.Contains(x.MaNhanVien)).ToList();

            var scopedTeamIds = teamMembershipRows
                .Where(x => scopedSet.Contains(x.MaNhanVien))
                .Select(x => x.MaNhom)
                .Distinct()
                .ToHashSet();

            nhomAssignments = nhomAssignments.Where(x => scopedTeamIds.Contains(x.MaNhom)).ToList();

            phongBanAssignments = phongBanAssignments
                .Where(x => x.MaPhongBan == scope.MaPhongBan.Value)
                .ToList();

            var scopedDuAnIds = duAnNhanVienRows
                .Where(x => scopedSet.Contains(x.MaNhanVien))
                .Select(x => x.MaDuAn)
                .Distinct()
                .ToHashSet();

            duAnAssignments = duAnAssignments.Where(x => scopedDuAnIds.Contains(x.MaDuAn)).ToList();

            // Chốt phạm vi KPI cho manager: chỉ những KPI có liên quan trực tiếp
            // đến nhân viên/phòng ban thuộc quyền quản lý.
            var scopedKpiIds = new HashSet<int>(
                nvAssignments.Select(x => x.MaKpi)
                    .Concat(nhomAssignments.Select(x => x.MaKpi))
                    .Concat(phongBanAssignments.Select(x => x.MaKpi))
                    .Concat(duAnAssignments.Select(x => x.MaKpi))
                    .Concat(resultRows.Select(x => x.MaKpi)));

            allCatalogRows = allCatalogRows
                .Where(x => scopedKpiIds.Contains(x.MaKpi))
                .ToList();
        }

        var activeNvAssignments = nvAssignments.Where(x => x.IsActive).ToList();
        var activeNhomAssignments = nhomAssignments.Where(x => x.IsActive).ToList();
        var activePhongBanAssignments = phongBanAssignments.Where(x => x.IsActive).ToList();
        var activeDuAnAssignments = duAnAssignments.Where(x => x.IsActive).ToList();

        var overviewRows = allCatalogRows.Select(kpi =>
        {
            var kpiNv = activeNvAssignments.Where(x => x.MaKpi == kpi.MaKpi).ToList();
            var kpiNhom = activeNhomAssignments.Where(x => x.MaKpi == kpi.MaKpi).ToList();
            var kpiPb = activePhongBanAssignments.Where(x => x.MaKpi == kpi.MaKpi).ToList();
            var kpiDuAn = activeDuAnAssignments.Where(x => x.MaKpi == kpi.MaKpi).ToList();
            var kpiRs = resultRows.Where(x => x.MaKpi == kpi.MaKpi).ToList();

            var avg = kpiRs.Count == 0 ? 0 : kpiRs.Average(x => x.Diem);
            var max = kpiRs.Count == 0 ? 0 : kpiRs.Max(x => x.Diem);
            var min = kpiRs.Count == 0 ? 0 : kpiRs.Min(x => x.Diem);

            return new KpiExportOverviewRowDto
            {
                MaKpi = kpi.MaKpi,
                TenKpi = kpi.TenKpi,
                TrongSoGoc = (double)(kpi.TrongSoGoc ?? 0),
                LoaiKpi = kpi.TenLoai,
                TrangThai = GetKpiStatusLabel(kpi.TrangThai),
                SoGanNhanVien = kpiNv.Count,
                SoGanNhom = kpiNhom.Count,
                SoGanPhongBan = kpiPb.Count,
                SoGanDuAn = kpiDuAn.Count,
                SoNhanVienCoDiem = kpiRs.Select(x => x.MaNhanVien).Distinct().Count(),
                DiemTrungBinh = avg,
                DiemCaoNhat = max,
                DiemThapNhat = min
            };
        })
        .OrderByDescending(x => x.SoGanNhanVien)
        .ThenBy(x => x.MaKpi)
        .ToList();

        if (scope.RoleKey == "manager")
        {
            overviewRows = overviewRows.Where(x =>
                x.SoGanNhanVien > 0 || x.SoGanNhom > 0 || x.SoGanPhongBan > 0 || x.SoGanDuAn > 0 || x.SoNhanVienCoDiem > 0).ToList();
        }

        if (exportType == "overview")
        {
            return BuildKpiOverviewExcelFile(overviewRows, targetMonth, targetYear);
        }

        var resultMap = resultRows
            .GroupBy(x => new { x.MaKpi, x.MaNhanVien })
            .ToDictionary(g => (g.Key.MaKpi, g.Key.MaNhanVien), g => g.Average(y => y.Diem));

        var detailRows = new List<KpiExportDetailRowDto>();

        detailRows.AddRange(nvAssignments.Select(x =>
        {
            var diemThanhPhan = resultMap.TryGetValue((x.MaKpi, x.MaNhanVien), out var diem) ? diem : 0d;
            var trongSoApDung = (double)x.TrongSoApDung;
            var kpiMeta = kpiLookup[x.MaKpi];
            return new KpiExportDetailRowDto
            {
                MaKpi = x.MaKpi,
                TenKpi = kpiMeta.TenKpi,
                LoaiKpi = kpiMeta.TenLoai,
                LoaiApDung = "NhanVien",
                MaDoiTuong = x.MaNhanVien,
                TenDoiTuong = x.HoTen,
                TrongSoGoc = (double)kpiMeta.TrongSoGoc,
                TrongSoApDung = trongSoApDung,
                IsActive = x.IsActive,
                TuNgay = x.TuNgay,
                DenNgay = x.DenNgay,
                DiemThanhPhan = diemThanhPhan,
                DongGop = Math.Round((diemThanhPhan * trongSoApDung) / 100d, 2, MidpointRounding.AwayFromZero)
            };
        }));

        // Với manager: file chi tiết chỉ xuất theo từng nhân viên thuộc phạm vi quản lý.
        // Với admin: giữ đầy đủ các loại áp dụng để phục vụ audit toàn hệ thống.
        if (scope.RoleKey != "manager")
        {
            detailRows.AddRange(nhomAssignments.Select(x =>
            {
                var kpiMeta = kpiLookup[x.MaKpi];
                return new KpiExportDetailRowDto
                {
                MaKpi = x.MaKpi,
                TenKpi = kpiMeta.TenKpi,
                LoaiKpi = kpiMeta.TenLoai,
                LoaiApDung = "Nhom",
                MaDoiTuong = x.MaNhom,
                TenDoiTuong = x.TenNhom,
                TrongSoGoc = (double)kpiMeta.TrongSoGoc,
                TrongSoApDung = (double)x.TrongSoApDung,
                IsActive = x.IsActive,
                TuNgay = x.TuNgay,
                DenNgay = x.DenNgay,
                DiemThanhPhan = 0,
                DongGop = 0
                };
            }));

            detailRows.AddRange(phongBanAssignments.Select(x =>
            {
                var kpiMeta = kpiLookup[x.MaKpi];
                return new KpiExportDetailRowDto
                {
                MaKpi = x.MaKpi,
                TenKpi = kpiMeta.TenKpi,
                LoaiKpi = kpiMeta.TenLoai,
                LoaiApDung = "PhongBan",
                MaDoiTuong = x.MaPhongBan,
                TenDoiTuong = x.TenPhongBan,
                TrongSoGoc = (double)kpiMeta.TrongSoGoc,
                TrongSoApDung = (double)x.TrongSoApDung,
                IsActive = x.IsActive,
                TuNgay = x.TuNgay,
                DenNgay = x.DenNgay,
                DiemThanhPhan = 0,
                DongGop = 0
                };
            }));

            detailRows.AddRange(duAnAssignments.Select(x =>
            {
                var kpiMeta = kpiLookup[x.MaKpi];
                return new KpiExportDetailRowDto
                {
                MaKpi = x.MaKpi,
                TenKpi = kpiMeta.TenKpi,
                LoaiKpi = kpiMeta.TenLoai,
                LoaiApDung = "DuAn",
                MaDoiTuong = x.MaDuAn,
                TenDoiTuong = x.TenDuAn,
                TrongSoGoc = (double)kpiMeta.TrongSoGoc,
                TrongSoApDung = (double)x.TrongSoApDung,
                IsActive = x.IsActive,
                TuNgay = x.TuNgay,
                DenNgay = x.DenNgay,
                DiemThanhPhan = 0,
                DongGop = 0
                };
            }));
        }

        return BuildKpiDetailExcelFile(detailRows
            .OrderBy(x => x.MaKpi)
            .ThenBy(x => x.LoaiApDung)
            .ThenBy(x => x.MaDoiTuong)
            .ToList(), targetMonth, targetYear);
    }

    [Authorize(Policy = Permissions.KpiView)]
    [HttpGet("catalog/{id:int}")]
    public async Task<ActionResult<ApiResponse<KpiCatalogDetailDto>>> GetCatalogDetail(
        int id,
        [FromQuery] int? thang,
        [FromQuery] int? nam)
    {
        var kpi = await _dbContext.DanhMucKpis
            .AsNoTracking()
            .Include(x => x.LoaiKpi)
            .FirstOrDefaultAsync(x => x.MaKpi == id);

        if (kpi == null)
        {
            return NotFound(ApiResponse<KpiCatalogDetailDto>.Fail("Không tìm thấy KPI."));
        }

        var kpiDisplayWeight = (double)(kpi.TrongSoGoc ?? 0);
        var kpiRawWeight = (double)(kpi.TrongSoGoc ?? 0);

        List<KpiAssignmentDto> nvAssignments;
        List<KpiAssignmentDto> nhomAssignments;
        List<KpiAssignmentDto> duAnAssignments;
        List<KpiAssignmentDto> phongBanAssignments;
        try
        {
            nvAssignments = await _dbContext.KpiNhanViens
                .AsNoTracking()
                .Where(x => x.MaKpi == id && x.IsActive)
                .Select(x => new KpiAssignmentDto
                {
                    ObjectId = x.MaNhanVien,
                    TenDoiTuong = x.NhanVien.HoTen,
                    Loai = "Nhân viên",
                    LoaiCode = "employee",
                    TrongSo = kpiDisplayWeight,
                    TrangThai = (x.TrangThai ?? 1) == 1 ? "Active" : "Inactive",
                    GhiChu = x.GhiChu
                })
                .ToListAsync();

            nhomAssignments = await _dbContext.KpiNhoms
                .AsNoTracking()
                .Where(x => x.MaKpi == id && x.IsActive)
                .Select(x => new KpiAssignmentDto
                {
                    ObjectId = x.MaNhom,
                    TenDoiTuong = x.Nhom.TenNhom,
                    Loai = "Nhóm",
                    LoaiCode = "team",
                    TrongSo = kpiDisplayWeight,
                    TrangThai = (x.TrangThai ?? 1) == 1 ? "Active" : "Inactive",
                    GhiChu = x.GhiChu
                })
                .ToListAsync();

            duAnAssignments = await _dbContext.KpiDuAns
                .AsNoTracking()
                .Where(x => x.MaKpi == id && x.IsActive)
                .Select(x => new KpiAssignmentDto
                {
                    ObjectId = x.MaDuAn,
                    TenDoiTuong = x.DuAn.TenDuAn,
                    Loai = "Dự án",
                    LoaiCode = "project",
                    TrongSo = kpiDisplayWeight,
                    TrangThai = (x.TrangThai ?? 1) == 1 ? "Active" : "Inactive",
                    GhiChu = x.GhiChu
                })
                .ToListAsync();

            phongBanAssignments = await _dbContext.KpiPhongBans
                .AsNoTracking()
                .Where(x => x.MaKpi == id && x.IsActive)
                .Select(x => new KpiAssignmentDto
                {
                    ObjectId = x.MaPhongBan,
                    TenDoiTuong = x.PhongBan.TenPhongBan,
                    Loai = "Phòng ban",
                    LoaiCode = "department",
                    TrongSo = kpiDisplayWeight,
                    TrangThai = (x.TrangThai ?? 1) == 1 ? "Active" : "Inactive",
                    GhiChu = x.GhiChu
                })
                .ToListAsync();
        }
        catch (Exception ex) when (IsMissingIsActiveColumnError(ex))
        {
            nvAssignments = await _dbContext.KpiNhanViens
                .AsNoTracking()
                .Where(x => x.MaKpi == id)
                .Select(x => new KpiAssignmentDto
                {
                    ObjectId = x.MaNhanVien,
                    TenDoiTuong = x.NhanVien.HoTen,
                    Loai = "Nhân viên",
                    LoaiCode = "employee",
                    TrongSo = kpiDisplayWeight,
                    TrangThai = (x.TrangThai ?? 1) == 1 ? "Active" : "Inactive",
                    GhiChu = x.GhiChu
                })
                .ToListAsync();

            nhomAssignments = await _dbContext.KpiNhoms
                .AsNoTracking()
                .Where(x => x.MaKpi == id)
                .Select(x => new KpiAssignmentDto
                {
                    ObjectId = x.MaNhom,
                    TenDoiTuong = x.Nhom.TenNhom,
                    Loai = "Nhóm",
                    LoaiCode = "team",
                    TrongSo = kpiDisplayWeight,
                    TrangThai = (x.TrangThai ?? 1) == 1 ? "Active" : "Inactive",
                    GhiChu = x.GhiChu
                })
                .ToListAsync();

            duAnAssignments = await _dbContext.KpiDuAns
                .AsNoTracking()
                .Where(x => x.MaKpi == id)
                .Select(x => new KpiAssignmentDto
                {
                    ObjectId = x.MaDuAn,
                    TenDoiTuong = x.DuAn.TenDuAn,
                    Loai = "Dự án",
                    LoaiCode = "project",
                    TrongSo = kpiDisplayWeight,
                    TrangThai = (x.TrangThai ?? 1) == 1 ? "Active" : "Inactive",
                    GhiChu = x.GhiChu
                })
                .ToListAsync();

            phongBanAssignments = await _dbContext.KpiPhongBans
                .AsNoTracking()
                .Where(x => x.MaKpi == id)
                .Select(x => new KpiAssignmentDto
                {
                    ObjectId = x.MaPhongBan,
                    TenDoiTuong = x.PhongBan.TenPhongBan,
                    Loai = "Phòng ban",
                    LoaiCode = "department",
                    TrongSo = kpiDisplayWeight,
                    TrangThai = (x.TrangThai ?? 1) == 1 ? "Active" : "Inactive",
                    GhiChu = x.GhiChu
                })
                .ToListAsync();
        }

        var assignments = nvAssignments
            .Concat(nhomAssignments)
            .Concat(duAnAssignments)
            .Concat(phongBanAssignments)
            .Take(200)
            .ToList();

        var resultRows = _dbContext.KetQuaKpis
            .AsNoTracking()
            .Where(x => x.MaKpi == id);

        if (thang.HasValue)
        {
            resultRows = resultRows.Where(x => x.thang == thang.Value);
        }

        if (nam.HasValue)
        {
            resultRows = resultRows.Where(x => x.nam == nam.Value);
        }

        var results = await resultRows
            .OrderByDescending(x => x.nam)
            .ThenByDescending(x => x.thang)
            .Select(x => new KpiResultRowDto
            {
                MaNhanVien = x.MaNhanVien,
                HoTen = x.NhanVien.HoTen,
                Diem = x.DiemSo.HasValue ? (double)x.DiemSo.Value : 0.0,
                thang = x.thang ?? 0,
                nam = x.nam ?? 0,
                XepLoai = Classify(x.DiemSo.HasValue ? (double)x.DiemSo.Value : 0.0)
            })
            .ToListAsync();

        var trend = results
            .GroupBy(x => new { x.nam, x.thang })
            .OrderBy(x => x.Key.nam).ThenBy(x => x.Key.thang)
            .Select(g => new KpiTrendPointDto
            {
                Label = $"{g.Key.thang:00}/{g.Key.nam}",
                Value = Math.Round(g.Average(x => x.Diem), 2)
            })
            .ToList();

        var detail = new KpiCatalogDetailDto
        {
            MaKpi = kpi.MaKpi,
            TenKpi = kpi.TenKpi,
            MoTa = $"KPI {kpi.TenKpi}",
            TrongSo = kpiDisplayWeight,
            TrongSoGoc = kpiRawWeight,
            MaLoaiKpi = kpi.MaLoaiKpi,
            TenLoaiKpi = kpi.LoaiKpi.TenLoaiKpi,
            TrangThai = GetKpiStatusLabel(kpi.TrangThai),
            Assignments = assignments,
            Results = results,
            Trend = trend
        };

        return Ok(ApiResponse<KpiCatalogDetailDto>.Ok(detail));
    }

    [Authorize(Policy = "KpiManage")]
    [HttpPost("catalog/{id:int}/assignments/sync")]
    public async Task<ActionResult<ApiResponse<object>>> SyncAssignments(int id, [FromBody] List<SaveKpiAssignmentRequest> assignments)
    {
        var kpiExists = await _dbContext.DanhMucKpis.AnyAsync(x => x.MaKpi == id);
        if (!kpiExists)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy KPI."));
        }

        var existingEmployeeIds = await _dbContext.KpiNhanViens
            .Where(x => x.MaKpi == id && x.IsActive)
            .Select(x => x.MaNhanVien)
            .Distinct()
            .ToListAsync();

        assignments ??= new List<SaveKpiAssignmentRequest>();
        var normalized = assignments
            .Where(x => x.ObjectId > 0 && !string.IsNullOrWhiteSpace(x.LoaiCode))
            .GroupBy(x => new { Type = x.LoaiCode!.Trim().ToLowerInvariant(), x.ObjectId })
            .Select(g => g.Last())
            .ToList();

        _dbContext.KpiNhanViens.RemoveRange(await _dbContext.KpiNhanViens.Where(x => x.MaKpi == id).ToListAsync());
        _dbContext.KpiNhoms.RemoveRange(await _dbContext.KpiNhoms.Where(x => x.MaKpi == id).ToListAsync());
        _dbContext.KpiDuAns.RemoveRange(await _dbContext.KpiDuAns.Where(x => x.MaKpi == id).ToListAsync());
        _dbContext.KpiPhongBans.RemoveRange(await _dbContext.KpiPhongBans.Where(x => x.MaKpi == id).ToListAsync());

        var now = DateTime.Now;
        foreach (var item in normalized)
        {
            var type = item.LoaiCode!.Trim().ToLowerInvariant();
            var trangThai = item.TrangThai ?? 1;

            if (type == "employee")
            {
                _dbContext.KpiNhanViens.Add(new Models.KpiNhanVien
                {
                    MaKpi = id,
                    MaNhanVien = item.ObjectId,
                    TuNgay = item.TuNgay ?? now,
                    DenNgay = item.DenNgay,
                    IsActive = true,
                    NgayKetThucApDung = null,
                    TrangThai = trangThai,
                    GhiChu = item.GhiChu
                });
                continue;
            }

            if (type == "team")
            {
                _dbContext.KpiNhoms.Add(new Models.KpiNhom
                {
                    MaKpi = id,
                    MaNhom = item.ObjectId,
                    TuNgay = item.TuNgay ?? now,
                    DenNgay = item.DenNgay,
                    IsActive = true,
                    NgayKetThucApDung = null,
                    TrangThai = trangThai,
                    GhiChu = item.GhiChu
                });
                continue;
            }

            if (type == "project")
            {
                _dbContext.KpiDuAns.Add(new Models.KpiDuAn
                {
                    MaKpi = id,
                    MaDuAn = item.ObjectId,
                    TuNgay = item.TuNgay ?? now,
                    DenNgay = item.DenNgay,
                    IsActive = true,
                    NgayKetThucApDung = null,
                    TrangThai = trangThai,
                    GhiChu = item.GhiChu
                });
                continue;
            }

            if (type == "department")
            {
                _dbContext.KpiPhongBans.Add(new Models.KpiPhongBan
                {
                    MaKpi = id,
                    MaPhongBan = item.ObjectId,
                    TuNgay = item.TuNgay ?? now,
                    DenNgay = item.DenNgay,
                    IsActive = true,
                    NgayKetThucApDung = null,
                    TrangThai = trangThai,
                    GhiChu = item.GhiChu
                });
            }
        }

        await _dbContext.SaveChangesAsync();

        var syncedEmployeeIds = normalized
            .Where(x => string.Equals(x.LoaiCode?.Trim(), "employee", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.ObjectId)
            .Distinct()
            .ToList();

        var affectedEmployeeIds = existingEmployeeIds
            .Concat(syncedEmployeeIds)
            .Distinct()
            .ToList();

        foreach (var employeeId in affectedEmployeeIds)
        {
            await NormalizeEmployeeKpiWeightsAsync(employeeId);
        }

        await _dbContext.SaveChangesAsync();

        var userId = _userManager.GetUserId(User);
        await _auditLogService.LogStructuredByUserIdAsync(
            userId,
            "UPDATE",
            "KPI_ASSIGNMENT",
            duLieuMoi: new { MaKpi = id, Tong = normalized.Count });

        return Ok(ApiResponse<object>.Ok(null, "Lưu gán KPI thành công"));
    }


    [Authorize(Policy = "KpiManage")]
    [HttpPost("catalog")]
    public async Task<ActionResult<ApiResponse<KpiCatalogItemDto>>> CreateCatalog([FromBody] SaveKpiCatalogRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TenKpi))
        {
            return BadRequest(ApiResponse<KpiCatalogItemDto>.Fail("Tên KPI là bắt buộc."));
        }

        var catalogBaseWeight = ResolveCatalogBaseWeight(request);
        if (catalogBaseWeight is <= 0 or > 100)
        {
            return BadRequest(ApiResponse<KpiCatalogItemDto>.Fail("Trọng số KPI gốc bắt buộc > 0 và <= 100."));
        }

        var loaiExists = await _dbContext.LoaiKpis.AnyAsync(x => x.MaLoaiKpi == request.MaLoaiKpi);
        if (!loaiExists)
        {
            return BadRequest(ApiResponse<KpiCatalogItemDto>.Fail("Loai KPI không tồn tại."));
        }

        var normalizedTenKpi = request.TenKpi.Trim();
        var existed = await _dbContext.DanhMucKpis.AnyAsync(x =>
            x.TenKpi != null
            && EF.Functions.Collate(x.TenKpi, "Latin1_General_CI_AI") == normalizedTenKpi);
        if (existed)
        {
            return Conflict(ApiResponse<KpiCatalogItemDto>.Fail("KPI đã tồn tại."));
        }

        var entity = new Models.DanhMucKpi
        {
            TenKpi = normalizedTenKpi,
            MaLoaiKpi = request.MaLoaiKpi,
            TrongSoGoc = (decimal)catalogBaseWeight,
            TrangThai = 1
        };

        _dbContext.DanhMucKpis.Add(entity);
        await _dbContext.SaveChangesAsync();

        var userId = _userManager.GetUserId(User);
        await _auditLogService.LogStructuredByUserIdAsync(
            userId,
            "CREATE",
            "DANHMUCKPI",
            duLieuMoi: new
            {
                entity.MaKpi,
                entity.TenKpi,
                entity.MaLoaiKpi,
                entity.TrongSoGoc
            });

        var loai = await _dbContext.LoaiKpis.AsNoTracking().FirstAsync(x => x.MaLoaiKpi == request.MaLoaiKpi);
        var dto = new KpiCatalogItemDto
        {
            MaKpi = entity.MaKpi,
            TenKpi = entity.TenKpi,
            MoTa = request.MoTa,
            TrongSo = (double)(entity.TrongSoGoc ?? 0),
            TrongSoGoc = (double)(entity.TrongSoGoc ?? 0),
            MaLoaiKpi = entity.MaLoaiKpi,
            TenLoaiKpi = loai.TenLoaiKpi,
            ApDung = entity.MaLoaiKpi == 1 ? "Cá nhân" : "Phòng ban",
            TrangThai = GetKpiStatusLabel(entity.TrangThai),
            SoDoiTuong = 0,
            DiemTrungBinh = 0
        };

        return Ok(ApiResponse<KpiCatalogItemDto>.Ok(dto, "Tạo KPI thành công"));
    }

    [Authorize(Policy = "KpiManage")]
    [HttpPut("catalog/{id:int}")]
    public async Task<ActionResult<ApiResponse<KpiCatalogItemDto>>> UpdateCatalog(int id, [FromBody] SaveKpiCatalogRequest request)
    {
        var entity = await _dbContext.DanhMucKpis.FirstOrDefaultAsync(x => x.MaKpi == id);
        if (entity == null)
        {
            return NotFound(ApiResponse<KpiCatalogItemDto>.Fail("Không tìm thấy KPI."));
        }

        if (string.IsNullOrWhiteSpace(request.TenKpi))
        {
            return BadRequest(ApiResponse<KpiCatalogItemDto>.Fail("Tên KPI là bắt buộc."));
        }

        var catalogBaseWeight = ResolveCatalogBaseWeight(request);
        if (catalogBaseWeight is <= 0 or > 100)
        {
            return BadRequest(ApiResponse<KpiCatalogItemDto>.Fail("Trọng số KPI gốc bắt buộc > 0 và <= 100."));
        }

        var normalizedTenKpi = request.TenKpi.Trim();
        var duplicate = await _dbContext.DanhMucKpis.AnyAsync(x =>
            x.MaKpi != id
            && x.TenKpi != null
            && EF.Functions.Collate(x.TenKpi, "Latin1_General_CI_AI") == normalizedTenKpi);
        if (duplicate)
        {
            return Conflict(ApiResponse<KpiCatalogItemDto>.Fail("Tên KPI bị trùng."));
        }

        var oldTen = entity.TenKpi;
        var oldLoai = entity.MaLoaiKpi;
        var oldTrongSoGoc = entity.TrongSoGoc;

        entity.TenKpi = normalizedTenKpi;
        entity.MaLoaiKpi = request.MaLoaiKpi;
        entity.TrongSoGoc = (decimal)catalogBaseWeight;
        await _dbContext.SaveChangesAsync();

        var userId = _userManager.GetUserId(User);
        await _auditLogService.LogStructuredByUserIdAsync(
            userId,
            "UPDATE",
            "DANHMUCKPI",
            duLieuCu: new
            {
                id,
                TenKpi = oldTen,
                MaLoaiKpi = oldLoai,
                TrongSoGoc = oldTrongSoGoc
            },
            duLieuMoi: new
            {
                entity.MaKpi,
                entity.TenKpi,
                entity.MaLoaiKpi,
                entity.TrongSoGoc
            });

        var loai = await _dbContext.LoaiKpis.AsNoTracking().FirstOrDefaultAsync(x => x.MaLoaiKpi == entity.MaLoaiKpi);
        var assCount = await _dbContext.KpiNhanViens.CountAsync(x => x.MaKpi == id && x.IsActive)
                 + await _dbContext.KpiNhoms.CountAsync(x => x.MaKpi == id && x.IsActive)
                 + await _dbContext.KpiDuAns.CountAsync(x => x.MaKpi == id && x.IsActive)
                 + await _dbContext.KpiPhongBans.CountAsync(x => x.MaKpi == id && x.IsActive);
        var avg = await _dbContext.KetQuaKpis.Where(x => x.MaKpi == id).Select(x => x.DiemSo ?? 0).DefaultIfEmpty(0).AverageAsync();

        var dto = new KpiCatalogItemDto
        {
            MaKpi = entity.MaKpi,
            TenKpi = entity.TenKpi,
            MoTa = request.MoTa,
            TrongSo = (double)(entity.TrongSoGoc ?? 0),
            TrongSoGoc = (double)(entity.TrongSoGoc ?? 0),
            MaLoaiKpi = entity.MaLoaiKpi,
            TenLoaiKpi = loai?.TenLoaiKpi,
            ApDung = entity.MaLoaiKpi == 1 ? "Cá nhân" : "Phòng ban",
            TrangThai = GetKpiStatusLabel(entity.TrangThai),
            SoDoiTuong = assCount,
            DiemTrungBinh = (double)avg
        };

        return Ok(ApiResponse<KpiCatalogItemDto>.Ok(dto, "Cập nhật KPI thành công"));
    }

    [Authorize(Policy = "KpiManage")]
    [HttpPut("catalog/{id:int}/status/toggle")]
    public async Task<ActionResult<ApiResponse<object>>> ToggleCatalogStatus(int id)
    {
        var entity = await _dbContext.DanhMucKpis.FirstOrDefaultAsync(x => x.MaKpi == id);
        if (entity == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy KPI."));
        }

        var oldStatus = entity.TrangThai ?? 1;
        entity.TrangThai = oldStatus == 1 ? 0 : 1;
        await _dbContext.SaveChangesAsync();

        var userId = _userManager.GetUserId(User);
        await _auditLogService.LogStructuredByUserIdAsync(
            userId,
            "UPDATE",
            "DANHMUCKPI",
            duLieuCu: new { entity.MaKpi, TrangThai = oldStatus },
            duLieuMoi: new { entity.MaKpi, TrangThai = entity.TrangThai, TrangThaiText = GetKpiStatusLabel(entity.TrangThai), entity.TrongSoGoc });

        var message = entity.TrangThai == 1 ? "Kích hoạt KPI thành công" : "Tạm dừng KPI thành công";
        return Ok(ApiResponse<object>.Ok(new
        {
            entity.MaKpi,
            TrangThai = entity.TrangThai,
            TrangThaiText = GetKpiStatusLabel(entity.TrangThai)
        }, message));
    }

    [Authorize(Policy = "KpiManage")]
    [HttpDelete("catalog/{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteCatalog(int id)
    {
        var entity = await _dbContext.DanhMucKpis.FirstOrDefaultAsync(x => x.MaKpi == id);
        if (entity == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy KPI."));
        }

        var hasResults = await _dbContext.KetQuaKpis.AnyAsync(x => x.MaKpi == id);
        var hasAssignments = await _dbContext.KpiNhanViens.AnyAsync(x => x.MaKpi == id && x.IsActive)
            || await _dbContext.KpiNhoms.AnyAsync(x => x.MaKpi == id && x.IsActive)
            || await _dbContext.KpiDuAns.AnyAsync(x => x.MaKpi == id && x.IsActive)
            || await _dbContext.KpiPhongBans.AnyAsync(x => x.MaKpi == id && x.IsActive);
        if (hasResults || hasAssignments)
        {
            return BadRequest(ApiResponse<object>.Fail("Không thể xóa KPI đã co ket qua hoặc đã gan doi tuong."));
        }

        _dbContext.DanhMucKpis.Remove(entity);
        await _dbContext.SaveChangesAsync();

        var userId = _userManager.GetUserId(User);
        await _auditLogService.LogStructuredByUserIdAsync(
            userId,
            "DELETE",
            "DANHMUCKPI",
            duLieuCu: new
            {
                entity.MaKpi,
                entity.TenKpi,
                entity.MaLoaiKpi,
                entity.TrongSoGoc
            },
            duLieuMoi: new { entity.MaKpi, DaXoa = true });

        return Ok(ApiResponse<object>.Ok(null, "Xóa KPI thành công"));
    }

    [Authorize(Policy = "KpiView")]
    [HttpGet("types")]
    public async Task<ActionResult<ApiResponse<List<KpiTypeDto>>>> GetKpiTypes()
    {
        EnsureDbConnectionStringInitialized();
        var types = await _dbContext.LoaiKpis
            .AsNoTracking()
            .OrderBy(x => x.MaLoaiKpi)
            .Select(x => new KpiTypeDto
            {
                MaLoaiKpi = x.MaLoaiKpi,
                TenLoaiKpi = x.TenLoaiKpi
            })
            .ToListAsync();

        return Ok(ApiResponse<List<KpiTypeDto>>.Ok(types));
    }

    [Authorize(Policy = "KpiManage")]
    [HttpPost("calculate")]
    public async Task<ActionResult<ApiResponse<KpiCalculateResult>>> Calculate([FromBody] KpiCalculateRequest request)
    {
        try
        {
            var kpiId = request.MaKpi ?? 1;
            var kpi = await _dbContext.DanhMucKpis.AsNoTracking().FirstOrDefaultAsync(x => x.MaKpi == kpiId);
            if (kpi == null)
            {
                return NotFound(ApiResponse<KpiCalculateResult>.Fail("Không tìm thấy KPI."));
            }

            if ((kpi.TrangThai ?? 1) == 0)
            {
                return BadRequest(ApiResponse<KpiCalculateResult>.Fail("KPI đang tạm dừng."));
            }

            var result = await _kpiService.CalculateAsync(request);
            var userId = _userManager.GetUserId(User);
            await _auditLogService.LogStructuredByUserIdAsync(
                userId,
                "EVALUATE",
                "KETQUAKPI",
                duLieuMoi: new
                {
                    request.MaNhanVien,
                    request.MaPhongBan,
                    request.MaKpi,
                    request.thang,
                    request.nam,
                    result.TongNhanVien,
                    result.SoBanGhiTaoMoi,
                    result.SoBanGhiCapNhat
                });
            return Ok(ApiResponse<KpiCalculateResult>.Ok(result, "Tinh KPI thành công"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<KpiCalculateResult>.Fail(ex.Message));
        }
    }

    [Authorize(Policy = "KpiManage")]
    [HttpPost("calculate-all")]
    public async Task<ActionResult<ApiResponse<CalculateAllKpiResult>>> CalculateAll([FromBody] CalculateAllKpiRequest request)
    {
        if (request.thang is < 1 or > 12)
        {
            return BadRequest(ApiResponse<CalculateAllKpiResult>.Fail("thang phải trong khoảng 1..12."));
        }

        if (request.nam <= 2000 || request.nam > 3000)
        {
            return BadRequest(ApiResponse<CalculateAllKpiResult>.Fail("nam không hợp lệ."));
        }

        var result = await _kpiService.CalculateAsync(new KpiCalculateRequest
        {
            MaKpi = null,
            thang = request.thang,
            nam = request.nam,
            MaNhanVien = request.MaNhanVien,
            MaPhongBan = request.MaPhongBan
        });

        var userId = _userManager.GetUserId(User);
        await _auditLogService.LogStructuredByUserIdAsync(
            userId,
            "EVALUATE",
            "KETQUAKPI",
            duLieuMoi: new
            {
                request.thang,
                request.nam,
                TongKPI = "ALL_ACTIVE_ASSIGNMENTS",
                TongBanGhiTaoMoi = result.SoBanGhiTaoMoi,
                TongBanGhiCapNhat = result.SoBanGhiCapNhat,
                TongBanGhiTongTaoMoi = result.SoBanGhiTongTaoMoi,
                TongBanGhiTongCapNhat = result.SoBanGhiTongCapNhat
            });

        var payload = new CalculateAllKpiResult
        {
            thang = request.thang,
            nam = request.nam,
            TongKPI = 1,
            TongNhanVien = result.TongNhanVien,
            TongTaskTrongKy = result.TongTaskTrongKy,
            SoBanGhiTaoMoi = result.SoBanGhiTaoMoi,
            SoBanGhiCapNhat = result.SoBanGhiCapNhat
        };

        return Ok(ApiResponse<CalculateAllKpiResult>.Ok(payload, "Tinh tất cả KPI thành công"));
    }

    [Authorize(Policy = "KpiView")]
    [HttpGet("scope")]
    public async Task<ActionResult<ApiResponse<KpiScopeDto>>> GetKpiScope()
    {
        var scope = await ResolveActorScopeAsync();
        if (!scope.Success)
        {
            return StatusCode(scope.StatusCode, ApiResponse<KpiScopeDto>.Fail(scope.ErrorMessage));
        }

        return Ok(ApiResponse<KpiScopeDto>.Ok(new KpiScopeDto
        {
            RoleKey = scope.RoleKey,
            MaNhanVien = scope.MaNhanVien > 0 ? scope.MaNhanVien : null,
            MaPhongBan = scope.MaPhongBan
        }));
    }

    [Authorize(Policy = "KpiView")]
    [HttpGet("team-options")]
    public async Task<ActionResult<ApiResponse<List<KpiTeamOptionDto>>>> GetTeamOptions([FromQuery] int? maPhongBan)
    {
        try
        {
            var scope = await ResolveActorScopeAsync();
            if (!scope.Success)
            {
                return StatusCode(scope.StatusCode, ApiResponse<List<KpiTeamOptionDto>>.Fail(scope.ErrorMessage));
            }

            var memberships = await (
                from tv in _dbContext.ThanhVienNhoms.AsNoTracking()
                join n in _dbContext.Nhoms.AsNoTracking() on tv.MaNhom equals n.MaNhom
                join nv in _dbContext.NhanViens.AsNoTracking() on tv.MaNhanVien equals nv.MaNhanVien
                where (nv.TrangThai ?? 1) == 1
                select new
                {
                    tv.MaNhom,
                    n.TenNhom,
                    tv.MaNhanVien,
                    nv.MaPhongBan
                })
                .ToListAsync();

            if (scope.RoleKey == "manager" && scope.MaPhongBan.HasValue)
            {
                memberships = memberships.Where(x => x.MaPhongBan == scope.MaPhongBan.Value).ToList();
            }
            else if (scope.RoleKey == "employee")
            {
                memberships = memberships.Where(x => x.MaNhanVien == scope.MaNhanVien).ToList();
            }

            if (maPhongBan.HasValue)
            {
                memberships = memberships.Where(x => x.MaPhongBan == maPhongBan.Value).ToList();
            }

            var teams = memberships
                .GroupBy(x => new { x.MaNhom, x.TenNhom })
                .Select(g => new KpiTeamOptionDto
                {
                    MaNhom = g.Key.MaNhom,
                    TenNhom = g.Key.TenNhom
                })
                .OrderBy(x => x.TenNhom)
                .ToList();

            return Ok(ApiResponse<List<KpiTeamOptionDto>>.Ok(teams));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<List<KpiTeamOptionDto>>.Ok(new List<KpiTeamOptionDto>(), $"KPI team-options fallback: {ex.Message}"));
        }
    }

    [Authorize(Policy = "KpiView")]
    [HttpGet("dashboard-summary")]
    public async Task<ActionResult<ApiResponse<KpiDashboardSummaryDto>>> GetDashboardSummary(
        [FromQuery] int? thang,
        [FromQuery] int? nam,
        [FromQuery] int? maPhongBan,
        [FromQuery] int? maNhom,
        [FromQuery] int maKpi = 1)
    {
        var scope = await ResolveActorScopeAsync();
        if (!scope.Success)
        {
            return StatusCode(scope.StatusCode, ApiResponse<KpiDashboardSummaryDto>.Fail(scope.ErrorMessage));
        }

        var targetMonth = thang is >= 1 and <= 12 ? thang.Value : DateTime.Now.Month;
        var targetYear = nam is > 2000 and <= 3000 ? nam.Value : DateTime.Now.Year;
        var today = DateTime.Today;

        var employeeQuery = _dbContext.NhanViens
            .AsNoTracking()
            .Where(x => (x.TrangThai ?? 1) == 1);

        if (scope.RoleKey == "manager" && scope.MaPhongBan.HasValue)
        {
            employeeQuery = employeeQuery.Where(x => x.MaPhongBan == scope.MaPhongBan.Value);
        }
        else if (scope.RoleKey == "employee")
        {
            employeeQuery = employeeQuery.Where(x => x.MaNhanVien == scope.MaNhanVien);
        }

        if (maPhongBan.HasValue)
        {
            employeeQuery = employeeQuery.Where(x => x.MaPhongBan == maPhongBan.Value);
        }

        if (maNhom.HasValue)
        {
            employeeQuery = employeeQuery.Where(x => _dbContext.ThanhVienNhoms.Any(tv => tv.MaNhom == maNhom.Value && tv.MaNhanVien == x.MaNhanVien));
        }

        var employeeIds = await employeeQuery.Select(x => x.MaNhanVien).ToListAsync();

        var scoreRows = await _dbContext.KetQuaKpis
            .AsNoTracking()
            .Where(x => x.MaKpi == maKpi
                        && x.thang == targetMonth
                        && x.nam == targetYear
                        && employeeIds.Contains(x.MaNhanVien))
            .Select(x => new { x.MaNhanVien, Score = x.DiemSo.HasValue ? (double)x.DiemSo.Value : 0.0 })
            .ToListAsync();

        var avgKpi = scoreRows.Count == 0 ? 0 : scoreRows.Average(x => x.Score);
        var achievedCount = scoreRows.Count(x => x.Score >= 70);
        var lowKpiCount = scoreRows.Count(x => x.Score < 50);

        var taskRows = await (
            from pc in _dbContext.PhanCongNhanViens.AsNoTracking()
            join cv in _dbContext.CongViecs.AsNoTracking() on pc.MaCongViec equals cv.MaCongViec
            where employeeIds.Contains(pc.MaNhanVien)
                && cv.DaXoa != true
                  && cv.HanHoanThanh.HasValue
                  && cv.HanHoanThanh.Value.Month == targetMonth
                  && cv.HanHoanThanh.Value.Year == targetYear
            select new
            {
                pc.MaCongViec,
                cv.MaTrangThai,
                cv.HanHoanThanh
            })
            .Distinct()
            .ToListAsync();

        var onTimeCount = taskRows.Count(x => (x.MaTrangThai ?? 0) == 3 && x.HanHoanThanh.HasValue && x.HanHoanThanh.Value.Date >= today);
        var lateCount = taskRows.Count(x => (x.MaTrangThai ?? 0) == 4 || ((x.MaTrangThai ?? 0) != 3 && x.HanHoanThanh.HasValue && x.HanHoanThanh.Value.Date < today));

        var topEmployees = await (
            from nv in _dbContext.NhanViens.AsNoTracking()
            join kq in _dbContext.KetQuaKpis.AsNoTracking().Where(x => x.MaKpi == maKpi && x.thang == targetMonth && x.nam == targetYear)
                on nv.MaNhanVien equals kq.MaNhanVien
            where employeeIds.Contains(nv.MaNhanVien)
            orderby kq.DiemSo descending
            select new KpiTopEmployeeDto
            {
                MaNhanVien = nv.MaNhanVien,
                HoTen = nv.HoTen,
                DiemKpi = (double)(kq.DiemSo ?? 0)
            })
            .Take(5)
            .ToListAsync();

        return Ok(ApiResponse<KpiDashboardSummaryDto>.Ok(new KpiDashboardSummaryDto
        {
            thang = targetMonth,
            nam = targetYear,
            KpiTrungBinh = Math.Round(avgKpi, 2),
            SoNhanVienDatKpi = achievedCount,
            SoNhanVienKpiThap = lowKpiCount,
            SoCongViecDungHan = onTimeCount,
            SoCongViecTreHan = lateCount,
            TopNhanViens = topEmployees
        }));
    }

    [Authorize(Policy = "KpiView")]
    [HttpGet("employees")]
    public async Task<ActionResult<ApiResponse<KpiEmployeeListDto>>> GetEmployeeKpiList(
        [FromQuery] int? thang,
        [FromQuery] int? nam,
        [FromQuery] int? maPhongBan,
        [FromQuery] int? maNhom,
        [FromQuery] string? mucKpi,
        [FromQuery] int? maKpi,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        try
        {
            var scope = await ResolveActorScopeAsync();
            if (!scope.Success)
            {
                return StatusCode(scope.StatusCode, ApiResponse<KpiEmployeeListDto>.Fail(scope.ErrorMessage));
            }

            page = Math.Max(1, page);
            size = Math.Clamp(size, 1, 200);
            var targetMonth = thang is >= 1 and <= 12 ? thang.Value : DateTime.Now.Month;
            var targetYear = nam is > 2000 and <= 3000 ? nam.Value : DateTime.Now.Year;
            var today = DateTime.Today;

            var employeeQuery = _dbContext.NhanViens
                .AsNoTracking()
                .Where(x => (x.TrangThai ?? 1) == 1);

            if (scope.RoleKey == "manager" && scope.MaPhongBan.HasValue)
            {
                employeeQuery = employeeQuery.Where(x => x.MaPhongBan == scope.MaPhongBan.Value);
            }
            else if (scope.RoleKey == "employee")
            {
                employeeQuery = employeeQuery.Where(x => x.MaNhanVien == scope.MaNhanVien);
            }

            if (maPhongBan.HasValue)
            {
                employeeQuery = employeeQuery.Where(x => x.MaPhongBan == maPhongBan.Value);
            }

            if (maNhom.HasValue)
            {
                employeeQuery = employeeQuery.Where(x => _dbContext.ThanhVienNhoms.Any(tv => tv.MaNhom == maNhom.Value && tv.MaNhanVien == x.MaNhanVien));
            }

            var baseEmployees = await employeeQuery
                .Select(x => new
                {
                    x.MaNhanVien,
                    x.HoTen,
                    x.MaPhongBan,
                    TenPhongBan = x.PhongBanQuanLy != null ? x.PhongBanQuanLy.TenPhongBan : null
                })
                .ToListAsync();

            var employeeIds = baseEmployees.Select(x => x.MaNhanVien).ToList();

            // Nếu UI không truyền tháng/năm thì ưu tiên kỳ dữ liệu KPI mới nhất trong DB
            // để tránh trường hợp tháng hiện tại không có dữ liệu và toàn bộ list ra 0.00.
            if (!(thang is >= 1 and <= 12) || !(nam is > 2000 and <= 3000))
            {
                var latestTong = await _dbContext.KetQuaKpiTongs
                    .AsNoTracking()
                    .Where(x => employeeIds.Contains(x.MaNhanVien))
                    .OrderByDescending(x => x.Nam)
                    .ThenByDescending(x => x.Thang)
                    .Select(x => new { x.Nam, x.Thang })
                    .FirstOrDefaultAsync();

                if (latestTong != null)
                {
                    targetYear = latestTong.Nam;
                    targetMonth = latestTong.Thang;
                }
                else
                {
                    var latestRaw = await _dbContext.KetQuaKpis
                        .AsNoTracking()
                        .Where(x => employeeIds.Contains(x.MaNhanVien))
                        .OrderByDescending(x => x.nam)
                        .ThenByDescending(x => x.thang)
                        .Select(x => new { Nam = x.nam, Thang = x.thang })
                        .FirstOrDefaultAsync();

                    if (latestRaw != null)
                    {
                        targetYear = latestRaw.Nam ?? targetYear;
                        targetMonth = latestRaw.Thang ?? targetMonth;
                    }
                }
            }

            Dictionary<int, double> scoreByEmployee;
            if (maKpi.HasValue && maKpi.Value > 0)
            {
                scoreByEmployee = await _dbContext.KetQuaKpis
                    .AsNoTracking()
                    .Where(x => x.MaKpi == maKpi.Value
                                && x.thang == targetMonth
                                && x.nam == targetYear
                                && employeeIds.Contains(x.MaNhanVien))
                    .GroupBy(x => x.MaNhanVien)
                    .Select(g => new { MaNhanVien = g.Key, Score = g.Average(v => (double)(v.DiemSo ?? 0)) })
                    .ToDictionaryAsync(x => x.MaNhanVien, x => x.Score);
            }
            else
            {
                // Khi không chỉ định MAKPI, dùng điểm KPI tổng tháng để tránh hiển thị 0.00 do lệch KPI mặc định.
                scoreByEmployee = await _dbContext.KetQuaKpiTongs
                    .AsNoTracking()
                    .Where(x => x.Thang == targetMonth
                                && x.Nam == targetYear
                                && employeeIds.Contains(x.MaNhanVien))
                    .ToDictionaryAsync(x => x.MaNhanVien, x => (double)x.DiemTong);

                // Fallback: nếu kỳ hiện tại chưa có bản ghi KETQUAKPI_TONG cho một số nhân viên,
                // lấy trung bình từ KETQUAKPI theo tháng/năm để tránh hiển thị 0 giả.
                var missingIds = employeeIds.Where(id => !scoreByEmployee.ContainsKey(id)).ToList();
                if (missingIds.Count > 0)
                {
                    var fallbackScores = await _dbContext.KetQuaKpis
                        .AsNoTracking()
                        .Where(x => x.thang == targetMonth
                                    && x.nam == targetYear
                                    && missingIds.Contains(x.MaNhanVien))
                        .GroupBy(x => x.MaNhanVien)
                        .Select(g => new { MaNhanVien = g.Key, Score = g.Average(v => (double)(v.DiemSo ?? 0)) })
                        .ToListAsync();

                    foreach (var row in fallbackScores)
                    {
                        scoreByEmployee[row.MaNhanVien] = row.Score;
                    }
                }
            }

            var taskStatsByEmployee = await (
                from pc in _dbContext.PhanCongNhanViens.AsNoTracking()
                join cv in _dbContext.CongViecs.AsNoTracking() on pc.MaCongViec equals cv.MaCongViec
                where employeeIds.Contains(pc.MaNhanVien)
                      && cv.DaXoa != true
                      && cv.HanHoanThanh.HasValue
                      && cv.HanHoanThanh.Value.Month == targetMonth
                      && cv.HanHoanThanh.Value.Year == targetYear
                group cv by pc.MaNhanVien into g
                select new
                {
                    MaNhanVien = g.Key,
                    TongTask = g.Select(x => x.MaCongViec).Distinct().Count(),
                    HoanThanh = g.Where(x => x.MaTrangThai == 3).Select(x => x.MaCongViec).Distinct().Count(),
                    TreHan = g.Where(x => x.MaTrangThai == 4 || (x.MaTrangThai != 3 && x.HanHoanThanh.HasValue && x.HanHoanThanh.Value.Date < today))
                              .Select(x => x.MaCongViec).Distinct().Count()
                })
                .ToDictionaryAsync(x => x.MaNhanVien);

            var rows = baseEmployees.Select(x =>
            {
                var score = scoreByEmployee.GetValueOrDefault(x.MaNhanVien, 0);
                var stats = taskStatsByEmployee.GetValueOrDefault(x.MaNhanVien);
                return new KpiEmployeeListItemDto
                {
                    MaNhanVien = x.MaNhanVien,
                    HoTen = x.HoTen,
                    MaPhongBan = x.MaPhongBan,
                    TenPhongBan = x.TenPhongBan,
                    DiemKpi = Math.Round(score, 2),
                    TongCongViec = stats?.TongTask ?? 0,
                    CongViecHoanThanh = stats?.HoanThanh ?? 0,
                    CongViecTreHan = stats?.TreHan ?? 0,
                    MucKpi = ClassifyKpiBand(score)
                };
            });

            if (!string.IsNullOrWhiteSpace(mucKpi))
            {
                var filter = mucKpi.Trim().ToLowerInvariant();
                rows = rows.Where(x => string.Equals((x.MucKpi ?? string.Empty).ToLowerInvariant(), filter, StringComparison.Ordinal));
            }

            var materialized = rows.OrderByDescending(x => x.DiemKpi).ThenBy(x => x.HoTen).ToList();
            var totalItems = materialized.Count;
            var paged = materialized.Skip((page - 1) * size).Take(size).ToList();

            return Ok(ApiResponse<KpiEmployeeListDto>.Ok(new KpiEmployeeListDto
            {
                thang = targetMonth,
                nam = targetYear,
                Page = page,
                Size = size,
                TotalItems = totalItems,
                TotalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)size),
                Items = paged
            }));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<KpiEmployeeListDto>.Ok(new KpiEmployeeListDto
            {
                thang = thang is >= 1 and <= 12 ? thang.Value : DateTime.Now.Month,
                nam = nam is > 2000 and <= 3000 ? nam.Value : DateTime.Now.Year,
                Page = Math.Max(1, page),
                Size = Math.Clamp(size, 1, 200),
                TotalItems = 0,
                TotalPages = 1,
                Items = new List<KpiEmployeeListItemDto>()
            }, $"KPI employees fallback: {ex.Message}"));
        }
    }

    [Authorize(Policy = "KpiView")]
    [HttpGet("employees/{id:int}/detail")]
    public async Task<ActionResult<ApiResponse<KpiEmployeeDetailDto>>> GetEmployeeKpiDetail(
        int id,
        [FromQuery] int? thang,
        [FromQuery] int? nam,
        [FromQuery] int maKpi = 1)
    {
        var scope = await ResolveActorScopeAsync();
        if (!scope.Success)
        {
            return StatusCode(scope.StatusCode, ApiResponse<KpiEmployeeDetailDto>.Fail(scope.ErrorMessage));
        }

        var targetMonth = thang is >= 1 and <= 12 ? thang.Value : DateTime.Now.Month;
        var targetYear = nam is > 2000 and <= 3000 ? nam.Value : DateTime.Now.Year;
        var today = DateTime.Today;

        var selectedMaKpi = maKpi;
        var hasRequestedKpi = selectedMaKpi > 0 && await _dbContext.KetQuaKpis
            .AsNoTracking()
            .AnyAsync(x => x.MaNhanVien == id && x.MaKpi == selectedMaKpi);

        if (!hasRequestedKpi)
        {
            selectedMaKpi = await _dbContext.KetQuaKpis
                .AsNoTracking()
                .Where(x => x.MaNhanVien == id)
                .OrderByDescending(x => x.nam)
                .ThenByDescending(x => x.thang)
                .ThenByDescending(x => x.MaKpi)
                .Select(x => x.MaKpi)
                .FirstOrDefaultAsync();
        }

        var scopedEmployeeQuery = _dbContext.NhanViens.AsNoTracking().AsQueryable();
        if (scope.RoleKey == "manager" && scope.MaPhongBan.HasValue)
        {
            scopedEmployeeQuery = scopedEmployeeQuery.Where(x => x.MaPhongBan == scope.MaPhongBan.Value);
        }
        else if (scope.RoleKey == "employee")
        {
            scopedEmployeeQuery = scopedEmployeeQuery.Where(x => x.MaNhanVien == scope.MaNhanVien);
        }

        var employee = await scopedEmployeeQuery
            .AsNoTracking()
            .Where(x => x.MaNhanVien == id)
            .Select(x => new
            {
                x.MaNhanVien,
                x.HoTen,
                x.MaPhongBan,
                TenPhongBan = x.PhongBanQuanLy != null ? x.PhongBanQuanLy.TenPhongBan : null
            })
            .FirstOrDefaultAsync();

        if (employee == null)
        {
            return NotFound(ApiResponse<KpiEmployeeDetailDto>.Fail("Không tìm thấy nhân viên."));
        }

        var currentScores = await _dbContext.KetQuaKpis
            .AsNoTracking()
            .Where(x => x.MaNhanVien == id && x.MaKpi == selectedMaKpi && x.thang == targetMonth && x.nam == targetYear)
            .Select(x => x.DiemSo.HasValue ? (double)x.DiemSo.Value : 0.0)
            .ToListAsync();
        var currentScore = currentScores.Count == 0 ? 0 : currentScores.Average();

        var taskRows = await (
            from pc in _dbContext.PhanCongNhanViens.AsNoTracking()
            join cv in _dbContext.CongViecs.AsNoTracking() on pc.MaCongViec equals cv.MaCongViec
            where pc.MaNhanVien == id
                  && cv.DaXoa != true
                  && cv.HanHoanThanh.HasValue
                  && cv.HanHoanThanh.Value.Month == targetMonth
                  && cv.HanHoanThanh.Value.Year == targetYear
            select new
            {
                cv.MaCongViec,
                cv.MaTrangThai,
                cv.HanHoanThanh
            })
            .Distinct()
            .ToListAsync();

        var totalTasks = taskRows.Count;
        var completedTasks = taskRows.Count(x => x.MaTrangThai == 3);
        var lateTasks = taskRows.Count(x => x.MaTrangThai == 4 || (x.MaTrangThai != 3 && x.HanHoanThanh.HasValue && x.HanHoanThanh.Value.Date < today));
        var onTimeTasks = Math.Max(0, completedTasks - lateTasks);

        var history = await _dbContext.KetQuaKpis
            .AsNoTracking()
            .Where(x => x.MaNhanVien == id && x.MaKpi == selectedMaKpi)
            .OrderByDescending(x => x.nam)
            .ThenByDescending(x => x.thang)
            .Take(12)
            .Select(x => new KpiDiemKyDto
            {
                thang = x.thang ?? 0,
                nam = x.nam ?? 0,
                Diem = x.DiemSo.HasValue ? (double)x.DiemSo.Value : 0.0,
                XepLoai = Classify(x.DiemSo.HasValue ? (double)x.DiemSo.Value : 0.0)
            })
            .ToListAsync();

        history = history
            .OrderBy(x => x.nam)
            .ThenBy(x => x.thang)
            .ToList();

        var periodStart = new DateTime(targetYear, targetMonth, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
        var appliedWeights = await (
            from kn in _dbContext.KpiNhanViens.AsNoTracking()
            join dm in _dbContext.DanhMucKpis.AsNoTracking() on kn.MaKpi equals dm.MaKpi
            join lk in _dbContext.LoaiKpis.AsNoTracking() on dm.MaLoaiKpi equals lk.MaLoaiKpi into lkJoin
            from lk in lkJoin.DefaultIfEmpty()
            where kn.MaNhanVien == id
                  && kn.IsActive
                  && (!kn.TuNgay.HasValue || kn.TuNgay.Value <= periodEnd)
                  && (!kn.DenNgay.HasValue || kn.DenNgay.Value >= periodStart)
            orderby kn.TrongSoApDung descending, kn.MaKpi
            select new KpiEmployeeAppliedWeightDto
            {
                MaKpi = kn.MaKpi,
                TenKpi = dm.TenKpi,
                TenLoaiKpi = lk != null ? lk.TenLoaiKpi : null,
                TrongSoGoc = (double)(dm.TrongSoGoc ?? 0),
                TrongSoApDung = (double)kn.TrongSoApDung,
                TuNgay = kn.TuNgay,
                DenNgay = kn.DenNgay,
                IsActive = kn.IsActive
            })
            .ToListAsync();

        var aiMonthly = await _dbContext.DuDoanAis
            .AsNoTracking()
            .Where(x => x.MaNhanVien == id && x.thang == targetMonth && x.nam == targetYear && x.DiemDuDoan.HasValue)
            .GroupBy(x => 1)
            .Select(g => new
            {
                Avg = g.Average(x => x.DiemDuDoan!.Value),
                Count = g.Count()
            })
            .FirstOrDefaultAsync();

        var aiCount = aiMonthly?.Count ?? 0;
        var aiScore = aiMonthly?.Avg;
        var aiUsed = aiCount >= 3 && aiScore.HasValue;

        var finalScores = await _dbContext.KetQuaKpis
            .AsNoTracking()
            .Where(x => x.MaNhanVien == id && x.thang == targetMonth && x.nam == targetYear)
            .GroupBy(x => x.MaKpi)
            .Select(g => new
            {
                MaKpi = g.Key,
                FinalScore = g.Average(x => x.DiemSo ?? 0m)
            })
            .ToDictionaryAsync(x => x.MaKpi, x => x.FinalScore);

        var scoreBreakdown = appliedWeights
            .Select(x =>
            {
                finalScores.TryGetValue(x.MaKpi, out var finalScoreRaw);
                var finalScore = finalScoreRaw;
                var ruleScore = finalScore;
                if (aiUsed && aiScore.HasValue)
                {
                    var computedRule = (finalScore - (0.3m * aiScore.Value)) / 0.7m;
                    ruleScore = Math.Max(0m, Math.Min(100m, computedRule));
                }

                var contribution = finalScore * ((decimal)x.TrongSoApDung / 100m);
                return new KpiEmployeeScoreBreakdownDto
                {
                    MaKpi = x.MaKpi,
                    TenKpi = x.TenKpi,
                    RuleScore = Math.Round((double)ruleScore, 2),
                    AiScore = aiUsed && aiScore.HasValue ? Math.Round((double)aiScore.Value, 2) : null,
                    FinalScore = Math.Round((double)finalScore, 2),
                    TrongSoApDung = x.TrongSoApDung,
                    DongGop = Math.Round((double)contribution, 2),
                    CoSuDungAi = aiUsed
                };
            })
            .OrderByDescending(x => x.TrongSoApDung)
            .ThenBy(x => x.MaKpi)
            .ToList();

        return Ok(ApiResponse<KpiEmployeeDetailDto>.Ok(new KpiEmployeeDetailDto
        {
            MaNhanVien = employee.MaNhanVien,
            HoTen = employee.HoTen,
            MaPhongBan = employee.MaPhongBan,
            TenPhongBan = employee.TenPhongBan,
            thang = targetMonth,
            nam = targetYear,
            DiemKpiHienTai = Math.Round(currentScore, 2),
            MucKpi = ClassifyKpiBand(currentScore),
            TongCongViec = totalTasks,
            CongViecHoanThanh = completedTasks,
            CongViecDungHan = onTimeTasks,
            CongViecTreHan = lateTasks,
            KpiTheoThang = history,
            KpiTrongSoApDung = appliedWeights,
            KpiDiemChiTiet = scoreBreakdown
        }));
    }

    [Authorize(Policy = "KpiView")]
    [HttpGet("team-summary")]
    public async Task<ActionResult<ApiResponse<KpiTeamSummaryDto>>> GetTeamSummary(
        [FromQuery] int? thang,
        [FromQuery] int? nam,
        [FromQuery] int? maPhongBan,
        [FromQuery] int maKpi = 1)
    {
        var scope = await ResolveActorScopeAsync();
        if (!scope.Success)
        {
            return StatusCode(scope.StatusCode, ApiResponse<KpiTeamSummaryDto>.Fail(scope.ErrorMessage));
        }

        var targetMonth = thang is >= 1 and <= 12 ? thang.Value : DateTime.Now.Month;
        var targetYear = nam is > 2000 and <= 3000 ? nam.Value : DateTime.Now.Year;

        var baseEmployees = _dbContext.NhanViens
            .AsNoTracking()
            .Where(x => (x.TrangThai ?? 1) == 1);

        if (scope.RoleKey == "manager" && scope.MaPhongBan.HasValue)
        {
            baseEmployees = baseEmployees.Where(x => x.MaPhongBan == scope.MaPhongBan.Value);
        }
        else if (scope.RoleKey == "employee")
        {
            baseEmployees = baseEmployees.Where(x => x.MaNhanVien == scope.MaNhanVien);
        }

        if (maPhongBan.HasValue)
        {
            baseEmployees = baseEmployees.Where(x => x.MaPhongBan == maPhongBan.Value);
        }

        var employeeProfiles = await baseEmployees
            .Select(x => new
            {
                x.MaNhanVien,
                x.MaPhongBan,
                TenPhongBan = x.PhongBanQuanLy != null ? x.PhongBanQuanLy.TenPhongBan : null
            })
            .ToListAsync();

        var employeeIds = employeeProfiles.Select(x => x.MaNhanVien).ToList();

        var teamRows = await (
            from tv in _dbContext.ThanhVienNhoms.AsNoTracking()
            join n in _dbContext.Nhoms.AsNoTracking() on tv.MaNhom equals n.MaNhom
            join nv in _dbContext.NhanViens.AsNoTracking() on tv.MaNhanVien equals nv.MaNhanVien
            where employeeIds.Contains(tv.MaNhanVien)
            select new
            {
                tv.MaNhom,
                n.TenNhom,
                tv.MaNhanVien,
                nv.MaPhongBan,
                TenPhongBan = nv.PhongBanQuanLy != null ? nv.PhongBanQuanLy.TenPhongBan : null
            })
            .ToListAsync();

        var scoreByEmployee = await _dbContext.KetQuaKpis
            .AsNoTracking()
            .Where(x => x.MaKpi == maKpi
                        && x.thang == targetMonth
                        && x.nam == targetYear
                        && employeeIds.Contains(x.MaNhanVien))
            .GroupBy(x => x.MaNhanVien)
            .Select(g => new { MaNhanVien = g.Key, Score = g.Average(v => (double)(v.DiemSo ?? 0)) })
            .ToDictionaryAsync(x => x.MaNhanVien, x => x.Score);

        var teamItems = teamRows
            .GroupBy(x => new { x.MaNhom, x.TenNhom })
            .Select(g =>
            {
                var members = g.Select(v => v.MaNhanVien).Distinct().ToList();
                var scores = members.Select(m => scoreByEmployee.GetValueOrDefault(m, 0d)).ToList();
                var avg = scores.Count == 0 ? 0 : scores.Average();
                return new KpiTeamSummaryItemDto
                {
                    MaNhom = g.Key.MaNhom,
                    TenNhom = g.Key.TenNhom,
                    SoThanhVien = members.Count,
                    KpiTrungBinh = Math.Round(avg, 2),
                    MucKpi = ClassifyKpiBand(avg)
                };
            })
            .OrderByDescending(x => x.KpiTrungBinh)
            .ToList();

        var deptItems = employeeProfiles
            .GroupBy(x => new { x.MaPhongBan, x.TenPhongBan })
            .Select(g =>
            {
                var members = g.Select(v => v.MaNhanVien).Distinct().ToList();
                var scores = members.Select(m => scoreByEmployee.GetValueOrDefault(m, 0d)).ToList();
                var avg = scores.Count == 0 ? 0 : scores.Average();
                return new KpiDepartmentSummaryItemDto
                {
                    MaPhongBan = g.Key.MaPhongBan,
                    TenPhongBan = g.Key.TenPhongBan,
                    SoNhanVien = members.Count,
                    KpiTrungBinh = Math.Round(avg, 2),
                    MucKpi = ClassifyKpiBand(avg)
                };
            })
            .OrderByDescending(x => x.KpiTrungBinh)
            .ToList();

        var bestTeam = teamItems.FirstOrDefault();
        var lowTeam = teamItems.LastOrDefault();

        return Ok(ApiResponse<KpiTeamSummaryDto>.Ok(new KpiTeamSummaryDto
        {
            thang = targetMonth,
            nam = targetYear,
            KpiTrungBinhNhom = teamItems.Count == 0 ? 0 : Math.Round(teamItems.Average(x => x.KpiTrungBinh), 2),
            KpiTrungBinhPhongBan = deptItems.Count == 0 ? 0 : Math.Round(deptItems.Average(x => x.KpiTrungBinh), 2),
            NhomTotNhat = bestTeam,
            NhomKpiThap = lowTeam,
            Teams = teamItems,
            Departments = deptItems
        }));
    }

    [Authorize(Policy = "KpiView")]
    [HttpGet("nhanvien/{id:int}")]
    public async Task<ActionResult<ApiResponse<KpiNhanVienDto>>> GetByNhanVien(
        int id,
        [FromQuery] int? thang,
        [FromQuery] int? nam,
        [FromQuery] int? maKpi = null)
    {
        var employee = await _dbContext.NhanViens.AsNoTracking().FirstOrDefaultAsync(x => x.MaNhanVien == id);
        if (employee == null)
        {
            return NotFound(ApiResponse<KpiNhanVienDto>.Fail("Không tìm thấy nhân viên."));
        }

        var targetMonth = thang is >= 1 and <= 12 ? thang.Value : DateTime.Now.Month;
        var targetYear = nam is > 2000 and <= 3000 ? nam.Value : DateTime.Now.Year;
        var useSpecificKpi = maKpi.HasValue && maKpi.Value > 0;
        var selectedMaKpi = maKpi.GetValueOrDefault();

        List<KpiDiemKyDto> ordered;
        if (useSpecificKpi)
        {
            var rows = _dbContext.KetQuaKpis
            .AsNoTracking()
            .Where(x => x.MaNhanVien == id && x.MaKpi == selectedMaKpi);

            if (thang.HasValue)
            {
                rows = rows.Where(x => x.thang == targetMonth);
            }

            if (nam.HasValue)
            {
                rows = rows.Where(x => x.nam == targetYear);
            }

            ordered = await rows
                .OrderByDescending(x => x.nam)
                .ThenByDescending(x => x.thang)
                .Take(12)
                .Select(x => new KpiDiemKyDto
                {
                    thang = x.thang ?? 0,
                    nam = x.nam ?? 0,
                    Diem = x.DiemSo.HasValue ? (double)x.DiemSo.Value : 0.0,
                    XepLoai = Classify(x.DiemSo.HasValue ? (double)x.DiemSo.Value : 0.0)
                })
                .ToListAsync();
        }
        else
        {
            var rows = _dbContext.KetQuaKpis
                .AsNoTracking()
                .Where(x => x.MaNhanVien == id);

            if (thang.HasValue)
            {
                rows = rows.Where(x => x.thang == targetMonth);
            }

            if (nam.HasValue)
            {
                rows = rows.Where(x => x.nam == targetYear);
            }

            ordered = await rows
                .GroupBy(x => new { x.nam, x.thang })
                .Select(g => new
                {
                    Nam = g.Key.nam ?? 0,
                    Thang = g.Key.thang ?? 0,
                    Diem = g.Average(x => x.DiemSo.HasValue ? (double)x.DiemSo.Value : 0.0)
                })
                .OrderByDescending(x => x.Nam)
                .ThenByDescending(x => x.Thang)
                .Take(12)
                .Select(x => new KpiDiemKyDto
                {
                    thang = x.Thang,
                    nam = x.Nam,
                    Diem = Math.Round(x.Diem, 2),
                    XepLoai = Classify(x.Diem)
                })
                .ToListAsync();

            if (ordered.Count == 0)
            {
                var fallbackRows = _dbContext.KetQuaKpiTongs
                    .AsNoTracking()
                    .Where(x => x.MaNhanVien == id);

                if (thang.HasValue)
                {
                    fallbackRows = fallbackRows.Where(x => x.Thang == targetMonth);
                }

                if (nam.HasValue)
                {
                    fallbackRows = fallbackRows.Where(x => x.Nam == targetYear);
                }

                ordered = await fallbackRows
                    .OrderByDescending(x => x.Nam)
                    .ThenByDescending(x => x.Thang)
                    .Select(x => new KpiDiemKyDto
                    {
                        thang = x.Thang,
                        nam = x.Nam,
                        Diem = (double)x.DiemTong,
                        XepLoai = string.IsNullOrWhiteSpace(x.XepLoai) ? Classify((double)x.DiemTong) : x.XepLoai
                    })
                    .Take(12)
                    .ToListAsync();
            }
        }

        ordered = ordered
            .OrderBy(x => x.nam)
            .ThenBy(x => x.thang)
            .ToList();

        var latest = ordered.Count > 0 ? ordered[^1] : null;
        var previous = ordered.Count > 1 ? ordered[ordered.Count - 2] : null;

        double? trend = null;
        if (latest != null && previous != null)
        {
            if (Math.Abs(previous.Diem) < 0.0001)
            {
                trend = latest.Diem > 0 ? 100 : 0;
            }
            else
            {
                trend = ((latest.Diem - previous.Diem) / previous.Diem) * 100;
            }
        }

        var result = new KpiNhanVienDto
        {
            MaNhanVien = employee.MaNhanVien,
            HoTen = employee.HoTen,
            MaKpi = useSpecificKpi ? selectedMaKpi : 0,
            DiemHienTai = latest?.Diem ?? 0,
            XepLoai = Classify(latest?.Diem ?? 0),
            XuHuongPhanTram = trend,
            LichSu12Thang = ordered,
            KpiTheoThang = ordered
        };

        return Ok(ApiResponse<KpiNhanVienDto>.Ok(result));
    }

    [Authorize(Policy = "KpiView")]
    [HttpGet("phongban/{id:int}")]
    public async Task<ActionResult<ApiResponse<KpiPhongBanDto>>> GetByPhongBan(
        int id,
        [FromQuery] int? thang,
        [FromQuery] int? nam,
        [FromQuery] int? maKpi = null)
    {
        var scope = await ResolveActorScopeAsync();
        if (!scope.Success)
        {
            return StatusCode(scope.StatusCode, ApiResponse<KpiPhongBanDto>.Fail(scope.ErrorMessage));
        }

        if (scope.RoleKey == "manager" && scope.MaPhongBan.HasValue && id != scope.MaPhongBan.Value)
        {
            return Forbid();
        }

        if (scope.RoleKey == "employee")
        {
            return Forbid();
        }

        var targetMonth = thang is >= 1 and <= 12 ? thang.Value : DateTime.Now.Month;
        var targetYear = nam is > 2000 and <= 3000 ? nam.Value : DateTime.Now.Year;

        var phongBan = await _dbContext.PhongBans.AsNoTracking().FirstOrDefaultAsync(x => x.MaPhongBan == id);
        if (phongBan == null)
        {
            return NotFound(ApiResponse<KpiPhongBanDto>.Fail("Không tìm thấy phòng ban."));
        }

        var departmentEmployees = await _dbContext.NhanViens
            .AsNoTracking()
            .Where(x => x.MaPhongBan == id)
            .Select(x => new { x.MaNhanVien, x.HoTen })
            .ToListAsync();

        var employeeIds = departmentEmployees.Select(x => x.MaNhanVien).ToList();
        Dictionary<int, double> scoreByEmployee;

        if (maKpi.HasValue && maKpi.Value > 0)
        {
            scoreByEmployee = await _dbContext.KetQuaKpis
                .AsNoTracking()
                .Where(x => x.thang == targetMonth
                            && x.nam == targetYear
                            && x.MaKpi == maKpi.Value
                            && employeeIds.Contains(x.MaNhanVien))
                .GroupBy(x => x.MaNhanVien)
                .Select(g => new { MaNhanVien = g.Key, Score = g.Average(v => (double)(v.DiemSo ?? 0)) })
                .ToDictionaryAsync(x => x.MaNhanVien, x => x.Score);
        }
        else
        {
            scoreByEmployee = await _dbContext.KetQuaKpiTongs
                .AsNoTracking()
                .Where(x => x.Thang == targetMonth
                            && x.Nam == targetYear
                            && employeeIds.Contains(x.MaNhanVien))
                .ToDictionaryAsync(x => x.MaNhanVien, x => (double)x.DiemTong);

            var missingIds = employeeIds.Where(employeeId => !scoreByEmployee.ContainsKey(employeeId)).ToList();
            if (missingIds.Count > 0)
            {
                var fallbackScores = await _dbContext.KetQuaKpis
                    .AsNoTracking()
                    .Where(x => x.thang == targetMonth
                                && x.nam == targetYear
                                && missingIds.Contains(x.MaNhanVien))
                    .GroupBy(x => x.MaNhanVien)
                    .Select(g => new { MaNhanVien = g.Key, Score = g.Average(v => (double)(v.DiemSo ?? 0)) })
                    .ToListAsync();

                foreach (var fallback in fallbackScores)
                {
                    scoreByEmployee[fallback.MaNhanVien] = fallback.Score;
                }
            }
        }

        var rows = departmentEmployees
            .Select(nv =>
            {
                var score = scoreByEmployee.GetValueOrDefault(nv.MaNhanVien, 0);
                return new KpiPhongBanNhanVienItemDto
                {
                    MaNhanVien = nv.MaNhanVien,
                    HoTen = nv.HoTen,
                    Diem = score,
                    XepLoai = Classify(score)
                };
            })
            .OrderByDescending(x => x.Diem)
            .ToList();

        var avg = rows.Count == 0 ? 0 : rows.Average(x => x.Diem);

        var result = new KpiPhongBanDto
        {
            MaPhongBan = phongBan.MaPhongBan,
            TenPhongBan = phongBan.TenPhongBan,
            thang = targetMonth,
            nam = targetYear,
            MaKpi = maKpi ?? 0,
            KpiTrungBinh = avg,
            XepLoai = Classify(avg),
            NhanViens = rows
        };

        return Ok(ApiResponse<KpiPhongBanDto>.Ok(result));
    }

    [Authorize(Policy = "KpiManage")]
    [HttpGet("proposals")]
    public async Task<ActionResult<ApiResponse<List<KpiProposalDto>>>> GetProposals([FromQuery] string? trangThai)
    {
        var scope = await ResolveActorScopeAsync();
        if (!scope.Success)
        {
            return StatusCode(scope.StatusCode, ApiResponse<List<KpiProposalDto>>.Fail(scope.ErrorMessage));
        }

        var query = _dbContext.DeXuatKpis.AsNoTracking().AsQueryable();
        if (scope.RoleKey == "manager")
        {
            query = query.Where(x => x.NguoiDeXuat == scope.MaNhanVien);
        }

        if (!string.IsNullOrWhiteSpace(trangThai))
        {
            var status = trangThai.Trim();
            query = query.Where(x => x.TrangThai == status);
        }

        var rows = await query
            .OrderByDescending(x => x.NgayTao)
            .Take(200)
            .Select(x => ToProposalDto(x))
            .ToListAsync();
        await EnrichProposalNamesAsync(rows);

        return Ok(ApiResponse<List<KpiProposalDto>>.Ok(rows));
    }

    [Authorize(Policy = "KpiManage")]
    [HttpGet("proposals/pending")]
    public async Task<ActionResult<ApiResponse<List<KpiProposalDto>>>> GetPendingProposals()
    {
        if (!User.IsInRole(Roles.Admin))
        {
            return Forbid();
        }

        var rows = await _dbContext.DeXuatKpis.AsNoTracking()
            .Where(x => x.TrangThai == KpiProposalStatus.ChoDuyet)
            .OrderBy(x => x.NgayTao)
            .Take(200)
            .Select(x => ToProposalDto(x))
            .ToListAsync();
        await EnrichProposalNamesAsync(rows);

        return Ok(ApiResponse<List<KpiProposalDto>>.Ok(rows));
    }

    [Authorize(Policy = "KpiManage")]
    [HttpPost("proposals")]
    public async Task<ActionResult<ApiResponse<KpiProposalDto>>> CreateProposal([FromBody] SaveKpiProposalRequest request)
    {
        var scope = await ResolveActorScopeAsync();
        if (!scope.Success)
        {
            return StatusCode(scope.StatusCode, ApiResponse<KpiProposalDto>.Fail(scope.ErrorMessage));
        }

        if (scope.RoleKey != "manager" && scope.RoleKey != "admin")
        {
            return Forbid();
        }

        var validateError = await ValidateProposalRequestAsync(request, scope, null);
        if (!string.IsNullOrWhiteSpace(validateError))
        {
            return BadRequest(ApiResponse<KpiProposalDto>.Fail(validateError));
        }

        var duplicateExists = await _dbContext.DeXuatKpis.AnyAsync(x =>
            x.TrangThai == KpiProposalStatus.ChoDuyet &&
            x.MaKpi == request.MaKpi &&
            x.MaNhanVienApDung == request.MaNhanVienApDung &&
            x.MaNhomApDung == request.MaNhomApDung &&
            x.MaPhongBanApDung == request.MaPhongBanApDung &&
            x.MaDuAnApDung == request.MaDuAnApDung &&
            x.TuNgay == request.TuNgay.Date &&
            x.DenNgay == request.DenNgay);

        if (duplicateExists)
        {
            return Conflict(ApiResponse<KpiProposalDto>.Fail("Đã tồn tại đề xuất chờ duyệt trùng KPI + đối tượng + thời gian."));
        }

        var proposal = new DeXuatKpi
        {
            MaKpi = request.MaKpi,
            MaLoaiKpi = request.MaLoaiKpi,
            NguoiDeXuat = scope.MaNhanVien,
            LoaiDeXuat = request.LoaiDeXuat!.Trim(),
            MaNhanVienApDung = request.MaNhanVienApDung,
            MaNhomApDung = request.MaNhomApDung,
            MaPhongBanApDung = request.MaPhongBanApDung,
            MaDuAnApDung = request.MaDuAnApDung,
            TuNgay = request.TuNgay.Date,
            DenNgay = request.DenNgay?.Date,
            TrongSoDeXuat = request.TrongSoDeXuat,
            TenKpiDeXuat = request.TenKpiDeXuat?.Trim(),
            MoTaKpiDeXuat = request.MoTaKpiDeXuat?.Trim(),
            LyDo = request.LyDo?.Trim(),
            GhiChu = request.GhiChu?.Trim()
        };

        _dbContext.DeXuatKpis.Add(proposal);
        await _dbContext.SaveChangesAsync();
        await _dbContext.Entry(proposal).ReloadAsync();

        await _auditLogService.LogStructuredByUserIdAsync(
            _userManager.GetUserId(User),
            "CREATE",
            "DE_XUAT_KPI",
            duLieuMoi: new { proposal.MaDeXuat, proposal.LoaiDeXuat, proposal.MaKpi, proposal.TrangThai });

        try
        {
            var managerName = await _dbContext.NhanViens.AsNoTracking()
                .Where(x => x.MaNhanVien == proposal.NguoiDeXuat)
                .Select(x => x.HoTen ?? $"NV {x.MaNhanVien}")
                .FirstOrDefaultAsync() ?? $"NV {proposal.NguoiDeXuat}";
            var adminIds = await ResolveAdminNotificationRecipientsAsync();
            await CreateKpiProposalNotificationAsync(
                "KPI_PROPOSAL",
                $"Đề xuất KPI mới từ quản lý {managerName}: {proposal.LoaiDeXuat}.",
                adminIds);
        }
        catch (Exception ex)
        {
            try
            {
                await _auditLogService.LogStructuredByUserIdAsync(
                    _userManager.GetUserId(User),
                    "WARNING",
                    "DE_XUAT_KPI",
                    duLieuMoi: new { proposal.MaDeXuat, NotificationError = ex.Message });
            }
            catch
            {
                // không chặn luồng tạo đề xuất nếu cả notification/audit warning đều lỗi
            }
        }

        var createDto = ToProposalDto(proposal);
        await EnrichProposalNameAsync(createDto);
        return Ok(ApiResponse<KpiProposalDto>.Ok(createDto, "Tạo đề xuất KPI thành công."));
    }

    [Authorize(Policy = "KpiManage")]
    [HttpPut("proposals/{id:int}")]
    public async Task<ActionResult<ApiResponse<KpiProposalDto>>> UpdateProposal(int id, [FromBody] SaveKpiProposalRequest request)
    {
        var scope = await ResolveActorScopeAsync();
        if (!scope.Success)
        {
            return StatusCode(scope.StatusCode, ApiResponse<KpiProposalDto>.Fail(scope.ErrorMessage));
        }

        var entity = await _dbContext.DeXuatKpis.FirstOrDefaultAsync(x => x.MaDeXuat == id);
        if (entity == null)
        {
            return NotFound(ApiResponse<KpiProposalDto>.Fail("Không tìm thấy đề xuất KPI."));
        }

        if (scope.RoleKey != "manager" || entity.NguoiDeXuat != scope.MaNhanVien)
        {
            return Forbid();
        }

        if (entity.TrangThai != KpiProposalStatus.CanChinhSua)
        {
            return BadRequest(ApiResponse<KpiProposalDto>.Fail("Chỉ đề xuất ở trạng thái 'CanChinhSua' mới được cập nhật."));
        }

        var validateError = await ValidateProposalRequestAsync(request, scope, entity.MaDeXuat);
        if (!string.IsNullOrWhiteSpace(validateError))
        {
            return BadRequest(ApiResponse<KpiProposalDto>.Fail(validateError));
        }

        entity.MaKpi = request.MaKpi;
        entity.MaLoaiKpi = request.MaLoaiKpi;
        entity.LoaiDeXuat = request.LoaiDeXuat!.Trim();
        entity.MaNhanVienApDung = request.MaNhanVienApDung;
        entity.MaNhomApDung = request.MaNhomApDung;
        entity.MaPhongBanApDung = request.MaPhongBanApDung;
        entity.MaDuAnApDung = request.MaDuAnApDung;
        entity.TuNgay = request.TuNgay.Date;
        entity.DenNgay = request.DenNgay?.Date;
        entity.TrongSoDeXuat = request.TrongSoDeXuat;
        entity.TenKpiDeXuat = request.TenKpiDeXuat?.Trim();
        entity.MoTaKpiDeXuat = request.MoTaKpiDeXuat?.Trim();
        entity.LyDo = request.LyDo?.Trim();
        entity.GhiChu = request.GhiChu?.Trim();
        entity.TrangThai = KpiProposalStatus.ChoDuyet;
        entity.NguoiCapNhat = scope.MaNhanVien;
        entity.NgayCapNhat = DateTime.Now;

        await _dbContext.SaveChangesAsync();

        await _auditLogService.LogStructuredByUserIdAsync(
            _userManager.GetUserId(User),
            "UPDATE",
            "DE_XUAT_KPI",
            duLieuMoi: new { entity.MaDeXuat, entity.TrangThai, entity.NgayCapNhat });

        try
        {
            var managerName = await _dbContext.NhanViens.AsNoTracking()
                .Where(x => x.MaNhanVien == entity.NguoiDeXuat)
                .Select(x => x.HoTen ?? $"NV {x.MaNhanVien}")
                .FirstOrDefaultAsync() ?? $"NV {entity.NguoiDeXuat}";
            var adminIds = await ResolveAdminNotificationRecipientsAsync();
            await CreateKpiProposalNotificationAsync(
                "KPI_PROPOSAL",
                $"Quản lý {managerName} đã gửi lại đề xuất KPI #{entity.MaDeXuat}.",
                adminIds);
        }
        catch (Exception ex)
        {
            try
            {
                await _auditLogService.LogStructuredByUserIdAsync(
                    _userManager.GetUserId(User),
                    "WARNING",
                    "DE_XUAT_KPI",
                    duLieuMoi: new { entity.MaDeXuat, NotificationError = ex.Message });
            }
            catch
            {
                // không chặn luồng cập nhật đề xuất nếu cả notification/audit warning đều lỗi
            }
        }

        var updateDto = ToProposalDto(entity);
        await EnrichProposalNameAsync(updateDto);
        return Ok(ApiResponse<KpiProposalDto>.Ok(updateDto, "Đã cập nhật và gửi lại đề xuất."));
    }

    [Authorize(Policy = "KpiManage")]
    [HttpPost("proposals/{id:int}/review")]
    public async Task<ActionResult<ApiResponse<KpiProposalDto>>> ReviewProposal(int id, [FromBody] ReviewKpiProposalRequest request)
    {
        var scope = await ResolveActorScopeAsync();
        if (!scope.Success)
        {
            return StatusCode(scope.StatusCode, ApiResponse<KpiProposalDto>.Fail(scope.ErrorMessage));
        }

        if (scope.RoleKey != "admin")
        {
            return Forbid();
        }

        var action = request.Action?.Trim();
        if (action is not (KpiProposalAction.Duyet or KpiProposalAction.TuChoi or KpiProposalAction.YeuCauChinhSua))
        {
            return BadRequest(ApiResponse<KpiProposalDto>.Fail("Action không hợp lệ."));
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var entity = await _dbContext.DeXuatKpis.FirstOrDefaultAsync(x => x.MaDeXuat == id);
            if (entity == null)
            {
                return NotFound(ApiResponse<KpiProposalDto>.Fail("Không tìm thấy đề xuất KPI."));
            }

            if (entity.TrangThai is KpiProposalStatus.DaDuyet or KpiProposalStatus.TuChoi)
            {
                return BadRequest(ApiResponse<KpiProposalDto>.Fail("Đề xuất đã đóng, không thể xử lý lại."));
            }

            entity.PhanHoiAdmin = request.PhanHoiAdmin?.Trim();
            entity.NguoiDuyet = scope.MaNhanVien;
            entity.NgayDuyet = DateTime.Now;
            entity.NguoiCapNhat = scope.MaNhanVien;
            entity.NgayCapNhat = DateTime.Now;

            if (action == KpiProposalAction.YeuCauChinhSua)
            {
                entity.TrangThai = KpiProposalStatus.CanChinhSua;
            }
            else if (action == KpiProposalAction.TuChoi)
            {
                entity.TrangThai = KpiProposalStatus.TuChoi;
            }
            else
            {
                await ApplyApprovedProposalAsync(entity);
                entity.TrangThai = KpiProposalStatus.DaDuyet;
            }

            await _dbContext.SaveChangesAsync();

            var auditLogged = await _auditLogService.LogStructuredByUserIdAsync(
                _userManager.GetUserId(User),
                "APPROVE",
                "DE_XUAT_KPI",
                duLieuMoi: new { entity.MaDeXuat, entity.TrangThai, Action = action });
            if (!auditLogged)
            {
                throw new InvalidOperationException("Không thể ghi nhật ký hoạt động khi duyệt đề xuất KPI.");
            }

            await tx.CommitAsync();
            try
            {
                var adminName = await _dbContext.NhanViens.AsNoTracking()
                    .Where(x => x.MaNhanVien == scope.MaNhanVien)
                    .Select(x => x.HoTen ?? $"NV {x.MaNhanVien}")
                    .FirstOrDefaultAsync() ?? $"NV {scope.MaNhanVien}";
                var proposalLabel = $"đề xuất KPI #{entity.MaDeXuat}";
                var noteText = string.IsNullOrWhiteSpace(entity.PhanHoiAdmin)
                    ? string.Empty
                    : $" Phản hồi: {entity.PhanHoiAdmin}";
                var notifyContent = entity.TrangThai switch
                {
                    KpiProposalStatus.DaDuyet
                        => $"Admin {adminName} đã duyệt {proposalLabel}. KPI đã được áp dụng.{noteText}",
                    KpiProposalStatus.CanChinhSua
                        => $"Admin {adminName} yêu cầu chỉnh sửa {proposalLabel}.{noteText}",
                    KpiProposalStatus.TuChoi
                        => $"Admin {adminName} đã từ chối {proposalLabel}.{noteText}",
                    _ => $"Admin {adminName} đã cập nhật trạng thái {proposalLabel}: {entity.TrangThai}.{noteText}"
                };
                await CreateKpiProposalNotificationAsync(
                    "KPI_PROPOSAL",
                    notifyContent,
                    new[] { entity.NguoiDeXuat });
            }
            catch
            {
                // keep review flow unaffected by notification errors
            }

            var reviewDto = ToProposalDto(entity);
            await EnrichProposalNameAsync(reviewDto);
            return Ok(ApiResponse<KpiProposalDto>.Ok(reviewDto, "Xử lý đề xuất KPI thành công."));
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return BadRequest(ApiResponse<KpiProposalDto>.Fail($"Không thể xử lý đề xuất: {ex.Message}"));
        }
    }

    private async Task ApplyApprovedProposalAsync(DeXuatKpi proposal)
    {
        var targetKpiId = proposal.MaKpi;
        if (proposal.LoaiDeXuat == KpiProposalType.TaoMoiKPI || !targetKpiId.HasValue)
        {
            if (!proposal.MaLoaiKpi.HasValue || string.IsNullOrWhiteSpace(proposal.TenKpiDeXuat))
            {
                throw new InvalidOperationException("Thiếu thông tin để tạo KPI mới.");
            }

            var newKpi = new DanhMucKpi
            {
                MaLoaiKpi = proposal.MaLoaiKpi.Value,
                TenKpi = proposal.TenKpiDeXuat!.Trim(),
                TrongSoGoc = proposal.TrongSoDeXuat,
                TrangThai = 1
            };
            _dbContext.DanhMucKpis.Add(newKpi);
            await _dbContext.SaveChangesAsync();
            targetKpiId = newKpi.MaKpi;
        }
        else if (proposal.LoaiDeXuat == KpiProposalType.DieuChinhKPI)
        {
            var oldKpi = await _dbContext.DanhMucKpis.AsNoTracking().FirstOrDefaultAsync(x => x.MaKpi == targetKpiId.Value)
                         ?? throw new InvalidOperationException("Không tìm thấy KPI cần điều chỉnh.");
            var revisedKpi = new DanhMucKpi
            {
                MaLoaiKpi = proposal.MaLoaiKpi ?? oldKpi.MaLoaiKpi,
                TenKpi = string.IsNullOrWhiteSpace(proposal.TenKpiDeXuat) ? oldKpi.TenKpi : proposal.TenKpiDeXuat,
                TrongSoGoc = proposal.TrongSoDeXuat,
                TrangThai = 1
            };
            _dbContext.DanhMucKpis.Add(revisedKpi);
            await _dbContext.SaveChangesAsync();
            targetKpiId = revisedKpi.MaKpi;
        }

        if (!targetKpiId.HasValue)
        {
            throw new InvalidOperationException("Không xác định được KPI áp dụng.");
        }

        if (proposal.LoaiDeXuat == KpiProposalType.HuyApDungKPI)
        {
            await CloseActiveAssignmentsAsync(proposal.MaKpi ?? targetKpiId.Value, proposal);
            if (proposal.MaNhanVienApDung.HasValue)
            {
                await NormalizeEmployeeKpiWeightsAsync(proposal.MaNhanVienApDung.Value);
            }
            return;
        }

        if (proposal.LoaiDeXuat == KpiProposalType.DieuChinhKPI && proposal.MaKpi.HasValue)
        {
            await CloseActiveAssignmentsAsync(proposal.MaKpi.Value, proposal);
        }

        await CreateAssignmentByProposalAsync(targetKpiId.Value, proposal);
        if (proposal.MaNhanVienApDung.HasValue)
        {
            await NormalizeEmployeeKpiWeightsAsync(proposal.MaNhanVienApDung.Value);
        }
        proposal.MaKpi = targetKpiId.Value;
    }

    private async Task NormalizeEmployeeKpiWeightsAsync(int maNhanVien)
    {
        var assignments = await _dbContext.KpiNhanViens
            .Where(x => x.MaNhanVien == maNhanVien && x.IsActive)
            .Join(_dbContext.DanhMucKpis,
                a => a.MaKpi,
                k => k.MaKpi,
                (a, k) => new
                {
                    Assignment = a,
                    TrongSoGoc = k.TrongSoGoc ?? 0m
                })
            .OrderBy(x => x.Assignment.MaKpi)
            .ToListAsync();

        if (assignments.Count == 0)
        {
            return;
        }

        const decimal fallbackBaseWeight = 1m;
        var total = assignments.Sum(x => x.TrongSoGoc > 0m ? x.TrongSoGoc : fallbackBaseWeight);
        var remaining = 100m;
        for (var i = 0; i < assignments.Count; i++)
        {
            var item = assignments[i];
            decimal weight;
            if (i == assignments.Count - 1)
            {
                weight = remaining;
            }
            else if (total > 0m)
            {
                var normalizedBase = item.TrongSoGoc > 0m ? item.TrongSoGoc : fallbackBaseWeight;
                weight = Math.Round((normalizedBase / total) * 100m, 2, MidpointRounding.AwayFromZero);
                remaining -= weight;
            }
            else
            {
                var per = Math.Round(100m / assignments.Count, 2, MidpointRounding.AwayFromZero);
                weight = per;
                remaining -= weight;
            }

            if (weight < 0m) weight = 0m;
            if (weight > 100m) weight = 100m;
            item.Assignment.TrongSoApDung = weight;
        }
    }

    private async Task CloseActiveAssignmentsAsync(int maKpi, DeXuatKpi proposal)
    {
        var now = DateTime.Now;
        if (proposal.MaNhanVienApDung.HasValue)
        {
            var row = await _dbContext.KpiNhanViens.FirstOrDefaultAsync(x => x.MaKpi == maKpi && x.MaNhanVien == proposal.MaNhanVienApDung.Value && x.IsActive);
            if (row != null)
            {
                row.IsActive = false;
                row.NgayKetThucApDung = now;
            }
            return;
        }

        if (proposal.MaNhomApDung.HasValue)
        {
            var row = await _dbContext.KpiNhoms.FirstOrDefaultAsync(x => x.MaKpi == maKpi && x.MaNhom == proposal.MaNhomApDung.Value && x.IsActive);
            if (row != null)
            {
                row.IsActive = false;
                row.NgayKetThucApDung = now;
            }
            return;
        }

        if (proposal.MaPhongBanApDung.HasValue)
        {
            var row = await _dbContext.KpiPhongBans.FirstOrDefaultAsync(x => x.MaKpi == maKpi && x.MaPhongBan == proposal.MaPhongBanApDung.Value && x.IsActive);
            if (row != null)
            {
                row.IsActive = false;
                row.NgayKetThucApDung = now;
            }
            return;
        }

        if (proposal.MaDuAnApDung.HasValue)
        {
            var row = await _dbContext.KpiDuAns.FirstOrDefaultAsync(x => x.MaKpi == maKpi && x.MaDuAn == proposal.MaDuAnApDung.Value && x.IsActive);
            if (row != null)
            {
                row.IsActive = false;
                row.NgayKetThucApDung = now;
            }
        }
    }

    private Task CreateAssignmentByProposalAsync(int maKpi, DeXuatKpi proposal)
    {
        if (proposal.MaNhanVienApDung.HasValue)
        {
            _dbContext.KpiNhanViens.Add(new KpiNhanVien
            {
                MaKpi = maKpi,
                MaNhanVien = proposal.MaNhanVienApDung.Value,
                TuNgay = proposal.TuNgay,
                DenNgay = proposal.DenNgay,
                IsActive = true,
                NgayKetThucApDung = null,
                TrangThai = 1,
                GhiChu = proposal.GhiChu
            });
            return Task.CompletedTask;
        }

        if (proposal.MaNhomApDung.HasValue)
        {
            _dbContext.KpiNhoms.Add(new KpiNhom
            {
                MaKpi = maKpi,
                MaNhom = proposal.MaNhomApDung.Value,
                TuNgay = proposal.TuNgay,
                DenNgay = proposal.DenNgay,
                IsActive = true,
                NgayKetThucApDung = null,
                TrangThai = 1,
                GhiChu = proposal.GhiChu
            });
            return Task.CompletedTask;
        }

        if (proposal.MaPhongBanApDung.HasValue)
        {
            _dbContext.KpiPhongBans.Add(new KpiPhongBan
            {
                MaKpi = maKpi,
                MaPhongBan = proposal.MaPhongBanApDung.Value,
                TuNgay = proposal.TuNgay,
                DenNgay = proposal.DenNgay,
                IsActive = true,
                NgayKetThucApDung = null,
                TrangThai = 1,
                GhiChu = proposal.GhiChu
            });
            return Task.CompletedTask;
        }

        if (proposal.MaDuAnApDung.HasValue)
        {
            _dbContext.KpiDuAns.Add(new KpiDuAn
            {
                MaKpi = maKpi,
                MaDuAn = proposal.MaDuAnApDung.Value,
                TuNgay = proposal.TuNgay,
                DenNgay = proposal.DenNgay,
                IsActive = true,
                NgayKetThucApDung = null,
                TrangThai = 1,
                GhiChu = proposal.GhiChu
            });
        }
        return Task.CompletedTask;
    }

    private async Task<string?> ValidateProposalRequestAsync(SaveKpiProposalRequest request, (bool Success, int StatusCode, string ErrorMessage, string RoleKey, int MaNhanVien, int? MaPhongBan) scope, int? currentProposalId)
    {
        if (request == null)
        {
            return "Dữ liệu đề xuất không hợp lệ.";
        }

        if (request.TuNgay == default)
        {
            return "TU_NGAY là bắt buộc.";
        }

        if (request.DenNgay.HasValue && request.DenNgay.Value.Date < request.TuNgay.Date)
        {
            return "DEN_NGAY phải lớn hơn hoặc bằng TU_NGAY.";
        }

        if (request.TrongSoDeXuat is < 0 or > 100)
        {
            return "TRONGSO_DEXUAT phải nằm trong khoảng 0..100.";
        }

        var applyCount = (request.MaNhanVienApDung.HasValue ? 1 : 0)
            + (request.MaNhomApDung.HasValue ? 1 : 0)
            + (request.MaPhongBanApDung.HasValue ? 1 : 0)
            + (request.MaDuAnApDung.HasValue ? 1 : 0);
        if (applyCount != 1)
        {
            return "Chỉ được chọn đúng 1 đối tượng áp dụng.";
        }

        var type = request.LoaiDeXuat?.Trim();
        if (type is not (KpiProposalType.TaoMoiKPI or KpiProposalType.ApDungKPI or KpiProposalType.DieuChinhKPI or KpiProposalType.HuyApDungKPI))
        {
            return "LOAI_DEXUAT không hợp lệ.";
        }

        if (type == KpiProposalType.TaoMoiKPI)
        {
            if (!request.MaLoaiKpi.HasValue || string.IsNullOrWhiteSpace(request.TenKpiDeXuat))
            {
                return "TaoMoiKPI bắt buộc MALOAIKPI và TENKPI_DEXUAT.";
            }

            if (request.TrongSoDeXuat <= 0)
            {
                return "TaoMoiKPI bắt buộc TRONGSO_DEXUAT > 0.";
            }
        }

        if (type == KpiProposalType.ApDungKPI && !request.MaKpi.HasValue)
        {
            return "ApDungKPI bắt buộc MAKPI.";
        }

        if (type is KpiProposalType.DieuChinhKPI or KpiProposalType.HuyApDungKPI)
        {
            if (!request.MaKpi.HasValue)
            {
                return "Điều chỉnh/Hủy áp dụng bắt buộc MAKPI.";
            }
        }

        if (scope.RoleKey == "manager" && scope.MaPhongBan.HasValue)
        {
            if (request.MaNhanVienApDung.HasValue)
            {
                var valid = await _dbContext.NhanViens.AnyAsync(x => x.MaNhanVien == request.MaNhanVienApDung.Value && x.MaPhongBan == scope.MaPhongBan);
                if (!valid) return "Nhân viên áp dụng không thuộc phạm vi phòng ban quản lý.";
            }

            if (request.MaNhomApDung.HasValue)
            {
                var valid = await (
                    from tv in _dbContext.ThanhVienNhoms
                    join nv in _dbContext.NhanViens on tv.MaNhanVien equals nv.MaNhanVien
                    where tv.MaNhom == request.MaNhomApDung.Value && nv.MaPhongBan == scope.MaPhongBan
                    select tv.MaNhom).AnyAsync();
                if (!valid) return "Nhóm áp dụng không thuộc phạm vi quản lý.";
            }

            if (request.MaPhongBanApDung.HasValue && request.MaPhongBanApDung.Value != scope.MaPhongBan.Value)
            {
                return "Phòng ban áp dụng không thuộc phạm vi quản lý.";
            }

            if (request.MaDuAnApDung.HasValue)
            {
                var valid = await (
                    from dn in _dbContext.DuAnNhanViens
                    join nv in _dbContext.NhanViens on dn.MaNhanVien equals nv.MaNhanVien
                    where dn.MaDuAn == request.MaDuAnApDung.Value && nv.MaPhongBan == scope.MaPhongBan
                    select dn.MaDuAn).AnyAsync();
                if (!valid) return "Dự án áp dụng không thuộc phạm vi quản lý.";
            }
        }

        var duplicate = await _dbContext.DeXuatKpis.AnyAsync(x =>
            x.TrangThai == KpiProposalStatus.ChoDuyet
            && x.MaDeXuat != (currentProposalId ?? 0)
            && x.MaKpi == request.MaKpi
            && x.MaNhanVienApDung == request.MaNhanVienApDung
            && x.MaNhomApDung == request.MaNhomApDung
            && x.MaPhongBanApDung == request.MaPhongBanApDung
            && x.MaDuAnApDung == request.MaDuAnApDung
            && x.TuNgay == request.TuNgay.Date
            && x.DenNgay == request.DenNgay);

        if (duplicate)
        {
            return "Đã tồn tại đề xuất tương tự đang chờ duyệt.";
        }

        return null;
    }

    private static KpiProposalDto ToProposalDto(DeXuatKpi x)
    {
        return new KpiProposalDto
        {
            MaDeXuat = x.MaDeXuat,
            MaKpi = x.MaKpi,
            MaLoaiKpi = x.MaLoaiKpi,
            NguoiDeXuat = x.NguoiDeXuat,
            NguoiDuyet = x.NguoiDuyet,
            LoaiDeXuat = x.LoaiDeXuat,
            MaNhanVienApDung = x.MaNhanVienApDung,
            MaNhomApDung = x.MaNhomApDung,
            MaPhongBanApDung = x.MaPhongBanApDung,
            MaDuAnApDung = x.MaDuAnApDung,
            TuNgay = x.TuNgay,
            DenNgay = x.DenNgay,
            TrongSoDeXuat = x.TrongSoDeXuat,
            TenKpiDeXuat = x.TenKpiDeXuat,
            MoTaKpiDeXuat = x.MoTaKpiDeXuat,
            LyDo = x.LyDo,
            TrangThai = x.TrangThai,
            PhanHoiAdmin = x.PhanHoiAdmin,
            GhiChu = x.GhiChu,
            NgayTao = x.NgayTao,
            NgayCapNhat = x.NgayCapNhat,
            NgayDuyet = x.NgayDuyet
        };
    }

    private async Task EnrichProposalNamesAsync(List<KpiProposalDto> proposals)
    {
        if (proposals.Count == 0) return;
        var ids = proposals
            .SelectMany(x => new[] { x.NguoiDeXuat, x.NguoiDuyet ?? 0 })
            .Where(x => x > 0)
            .Distinct()
            .ToList();
        if (ids.Count == 0) return;

        var names = await _dbContext.NhanViens.AsNoTracking()
            .Where(x => ids.Contains(x.MaNhanVien))
            .Select(x => new { x.MaNhanVien, x.HoTen })
            .ToDictionaryAsync(x => x.MaNhanVien, x => x.HoTen ?? $"NV {x.MaNhanVien}");

        foreach (var item in proposals)
        {
            item.TenNguoiDeXuat = names.TryGetValue(item.NguoiDeXuat, out var deXuatName)
                ? deXuatName
                : $"NV {item.NguoiDeXuat}";
            item.TenNguoiDuyet = item.NguoiDuyet.HasValue
                ? (names.TryGetValue(item.NguoiDuyet.Value, out var duyetName) ? duyetName : $"NV {item.NguoiDuyet.Value}")
                : null;
        }
    }

    private async Task EnrichProposalNameAsync(KpiProposalDto proposal)
    {
        await EnrichProposalNamesAsync(new List<KpiProposalDto> { proposal });
    }

    private async Task<List<int>> ResolveAdminNotificationRecipientsAsync()
    {
        var roleBased = await (
            from nv in _dbContext.NhanViens.AsNoTracking()
            join ur in _dbContext.UserRoles.AsNoTracking() on nv.AspNetUserId equals ur.UserId
            join r in _dbContext.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where r.Name == Roles.Admin
            select nv.MaNhanVien
        ).Distinct().ToListAsync();

        if (roleBased.Count > 0)
        {
            return roleBased;
        }

        return await (
            from nv in _dbContext.NhanViens.AsNoTracking()
            join cv in _dbContext.ChucVus.AsNoTracking() on nv.MaChucVu equals cv.MaChucVu
            where cv.TenChucVu != null
                  && (EF.Functions.Like(cv.TenChucVu, "%Admin%")
                      || EF.Functions.Like(cv.TenChucVu, "%Quản trị%")
                      || EF.Functions.Like(cv.TenChucVu, "%Quan tri%"))
            select nv.MaNhanVien
        ).Distinct().ToListAsync();
    }

    private async Task CreateKpiProposalNotificationAsync(string loai, string noiDung, IEnumerable<int> maNhanVienIds)
    {
        var ids = maNhanVienIds.Where(x => x > 0).Distinct().ToList();
        if (ids.Count == 0) return;

        var loaiRow = await _dbContext.LoaiThongBaos.FirstOrDefaultAsync(x => x.TenLoai == loai);
        if (loaiRow == null)
        {
            loaiRow = new LoaiThongBao { TenLoai = loai };
            _dbContext.LoaiThongBaos.Add(loaiRow);
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IDENTITY_INSERT", StringComparison.OrdinalIgnoreCase) == true)
            {
                // fallback cho DB không dùng identity trên MALOAI
                _dbContext.Entry(loaiRow).State = EntityState.Detached;
                var nextId = (await _dbContext.LoaiThongBaos.MaxAsync(x => (int?)x.MaLoai) ?? 0) + 1;
                loaiRow = new LoaiThongBao { MaLoai = nextId, TenLoai = loai };
                _dbContext.LoaiThongBaos.Add(loaiRow);
                await _dbContext.SaveChangesAsync();
            }
        }

        var tb = new ThongBao
        {
            MaLoai = loaiRow.MaLoai,
            NoiDung = noiDung,
            ThoiGian = DateTime.Now
        };
        _dbContext.ThongBaos.Add(tb);
        await _dbContext.SaveChangesAsync();

        _dbContext.ThongBaoNhanViens.AddRange(ids.Select(id => new ThongBaoNhanVien
        {
            MaThongBao = tb.MaThongBao,
            MaNhanVien = id,
            DaDoc = false
        }));
        await _dbContext.SaveChangesAsync();
    }

    private static string Classify(double diem)
    {
        return diem switch
        {
            >= 80 => "Tot",
            >= 50 => "Trung binh",
            _ => "Kem"
        };
    }

    private static string GetKpiStatusLabel(int? status)
    {
        return (status ?? 1) == 1 ? "Active" : "Inactive";
    }

    private static string ClassifyKpiBand(double score)
    {
        return score switch
        {
            >= 90 => "xuat-sac",
            >= 70 => "tot",
            >= 50 => "trung-binh",
            _ => "thap"
        };
    }

    private static bool IsMissingIsActiveColumnError(Exception ex)
    {
        var message = ex.ToString();
        return message.Contains("IS_ACTIVE", StringComparison.OrdinalIgnoreCase)
               && message.Contains("Invalid column name", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(bool Success, int StatusCode, string ErrorMessage, string RoleKey, int MaNhanVien, int? MaPhongBan)> ResolveActorScopeAsync()
    {
        EnsureDbConnectionStringInitialized();
        var roleKey = User.IsInRole(Roles.Admin)
            ? "admin"
            : (User.IsInRole(Roles.Manager) ? "manager" : "employee");

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return (false, StatusCodes.Status401Unauthorized, "Không xác định người dùng.", roleKey, 0, null);
        }

        var actor = await _dbContext.NhanViens
            .AsNoTracking()
            .Where(x => x.AspNetUserId == userId)
            .Select(x => new { x.MaNhanVien, x.MaPhongBan })
            .FirstOrDefaultAsync();

        if (actor == null)
        {
            if (roleKey == "admin")
            {
                return (true, StatusCodes.Status200OK, string.Empty, roleKey, 0, null);
            }

            return (false, StatusCodes.Status401Unauthorized, "Tài khoản chưa liên kết nhân viên.", roleKey, 0, null);
        }

        if (roleKey == "manager" && !actor.MaPhongBan.HasValue)
        {
            return (false, StatusCodes.Status403Forbidden, "Tài khoản quản lý chưa gán phòng ban.", roleKey, actor.MaNhanVien, actor.MaPhongBan);
        }

        return (true, StatusCodes.Status200OK, string.Empty, roleKey, actor.MaNhanVien, actor.MaPhongBan);
    }

    private FileContentResult BuildKpiOverviewExcelFile(List<KpiExportOverviewRowDto> rows, int thang, int nam)
    {
        var tongKpi = rows.Count;
        var kpiActive = rows.Count(x => string.Equals(x.TrangThai, "Active", StringComparison.OrdinalIgnoreCase));
        var nhanVienCoKpi = rows.Sum(x => x.SoGanNhanVien);
        var diemTbHeThong = rows.Count == 0 ? 0d : rows.Average(x => x.DiemTrungBinh);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("TongQuanKPI");

        worksheet.Cell(1, 1).Value = "Chỉ số";
        worksheet.Cell(1, 2).Value = "Giá trị";
        worksheet.Cell(2, 1).Value = "Tổng KPI";
        worksheet.Cell(2, 2).Value = tongKpi;
        worksheet.Cell(3, 1).Value = "KPI đang hoạt động";
        worksheet.Cell(3, 2).Value = kpiActive;
        worksheet.Cell(4, 1).Value = "Nhân viên có KPI";
        worksheet.Cell(4, 2).Value = nhanVienCoKpi;
        worksheet.Cell(5, 1).Value = "Điểm trung bình hệ thống";
        worksheet.Cell(5, 2).Value = diemTbHeThong;
        worksheet.Cell(5, 2).Style.NumberFormat.Format = "0.00";

        var headerRow = 7;
        var headers = new[]
        {
            "Mã KPI", "Tên KPI", "Loại KPI", "Trọng số gốc", "Trạng thái",
            "Số gán nhân viên", "Số gán nhóm", "Số gán phòng ban", "Số gán dự án",
            "Số nhân viên có điểm", "Điểm trung bình", "Điểm cao nhất", "Điểm thấp nhất",
            "Tháng", "Năm"
        };

        for (var col = 0; col < headers.Length; col++)
        {
            worksheet.Cell(headerRow, col + 1).Value = headers[col];
        }

        var currentRow = headerRow + 1;
        foreach (var row in rows)
        {
            worksheet.Cell(currentRow, 1).Value = row.MaKpi;
            worksheet.Cell(currentRow, 2).Value = row.TenKpi ?? string.Empty;
            worksheet.Cell(currentRow, 3).Value = row.LoaiKpi ?? string.Empty;
            worksheet.Cell(currentRow, 4).Value = row.TrongSoGoc;
            worksheet.Cell(currentRow, 5).Value = string.Equals(row.TrangThai, "Active", StringComparison.OrdinalIgnoreCase) ? "Đang hoạt động" : "Tạm dừng";
            worksheet.Cell(currentRow, 6).Value = row.SoGanNhanVien;
            worksheet.Cell(currentRow, 7).Value = row.SoGanNhom;
            worksheet.Cell(currentRow, 8).Value = row.SoGanPhongBan;
            worksheet.Cell(currentRow, 9).Value = row.SoGanDuAn;
            worksheet.Cell(currentRow, 10).Value = row.SoNhanVienCoDiem;
            worksheet.Cell(currentRow, 11).Value = row.DiemTrungBinh;
            worksheet.Cell(currentRow, 12).Value = row.DiemCaoNhat;
            worksheet.Cell(currentRow, 13).Value = row.DiemThapNhat;
            worksheet.Cell(currentRow, 14).Value = thang;
            worksheet.Cell(currentRow, 15).Value = nam;
            currentRow++;
        }

        worksheet.Range(1, 1, 1, 2).Style.Font.Bold = true;
        worksheet.Range(headerRow, 1, headerRow, headers.Length).Style.Font.Bold = true;
        worksheet.Column(4).Style.NumberFormat.Format = "0.00";
        worksheet.Columns(11, 13).Style.NumberFormat.Format = "0.00";
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var filename = $"kpi-overview-{DateTime.Now:yyyyMMdd-HHmm}.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
    }

    private FileContentResult BuildKpiDetailExcelFile(List<KpiExportDetailRowDto> rows, int thang, int nam)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("ChiTietKPI");

        var headers = new[]
        {
            "Mã KPI", "Tên KPI", "Loại KPI", "Loại áp dụng", "Mã đối tượng", "Tên đối tượng",
            "Trọng số gốc", "Trọng số áp dụng", "Trạng thái áp dụng", "Từ ngày", "Đến ngày",
            "Điểm thành phần", "Đóng góp", "Tháng", "Năm"
        };

        for (var col = 0; col < headers.Length; col++)
        {
            worksheet.Cell(1, col + 1).Value = headers[col];
        }

        var currentRow = 2;
        foreach (var row in rows)
        {
            worksheet.Cell(currentRow, 1).Value = row.MaKpi;
            worksheet.Cell(currentRow, 2).Value = row.TenKpi ?? string.Empty;
            worksheet.Cell(currentRow, 3).Value = row.LoaiKpi ?? string.Empty;
            worksheet.Cell(currentRow, 4).Value = row.LoaiApDung switch
            {
                "NhanVien" => "Nhân viên",
                "Nhom" => "Nhóm",
                "PhongBan" => "Phòng ban",
                "DuAn" => "Dự án",
                _ => row.LoaiApDung
            };
            worksheet.Cell(currentRow, 5).Value = row.MaDoiTuong;
            worksheet.Cell(currentRow, 6).Value = row.TenDoiTuong ?? string.Empty;
            worksheet.Cell(currentRow, 7).Value = row.TrongSoGoc;
            worksheet.Cell(currentRow, 8).Value = row.TrongSoApDung;
            worksheet.Cell(currentRow, 9).Value = row.IsActive ? "Đang áp dụng" : "Ngừng áp dụng";
            worksheet.Cell(currentRow, 10).Value = row.TuNgay;
            worksheet.Cell(currentRow, 11).Value = row.DenNgay;
            worksheet.Cell(currentRow, 12).Value = row.DiemThanhPhan;
            worksheet.Cell(currentRow, 13).Value = row.DongGop;
            worksheet.Cell(currentRow, 14).Value = thang;
            worksheet.Cell(currentRow, 15).Value = nam;
            currentRow++;
        }

        worksheet.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;
        worksheet.Columns(7, 8).Style.NumberFormat.Format = "0.00";
        worksheet.Columns(12, 13).Style.NumberFormat.Format = "0.00";
        worksheet.Columns(10, 11).Style.DateFormat.Format = "yyyy-mm-dd";
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var filename = $"kpi-detail-{DateTime.Now:yyyyMMdd-HHmm}.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
    }

    public class KpiNhanVienDto
    {
        public int MaNhanVien { get; set; }
        public string? HoTen { get; set; }
        public int MaKpi { get; set; }
        public double DiemHienTai { get; set; }
        public string XepLoai { get; set; } = string.Empty;
        public double? XuHuongPhanTram { get; set; }
        public List<KpiDiemKyDto> LichSu12Thang { get; set; } = new();
        public List<KpiDiemKyDto> KpiTheoThang { get; set; } = new();
    }

    public class KpiDiemKyDto
    {
        public int thang { get; set; }
        public int nam { get; set; }
        public double Diem { get; set; }
        public string XepLoai { get; set; } = string.Empty;
    }

    [Authorize(Policy = "KpiView")]
    [HttpGet("scores")]
    public async Task<ActionResult<ApiResponse<object>>> GetScores(
        [FromQuery] string? ids,
        [FromQuery] int? thang,
        [FromQuery] int? nam,
        [FromQuery] int maKpi = 0)
    {
        if (string.IsNullOrWhiteSpace(ids))
        {
            return Ok(ApiResponse<object>.Ok(new { items = new Dictionary<int, double>() }));
        }

        var idList = ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => { int.TryParse(s, out var v); return v; })
            .Where(v => v > 0)
            .Distinct()
            .ToList();

        if (!idList.Any())
        {
            return Ok(ApiResponse<object>.Ok(new { items = new Dictionary<int, double>() }));
        }

        var targetMonth = thang is >= 1 and <= 12 ? thang.Value : DateTime.Now.Month;
        var targetYear = nam is > 2000 and <= 3000 ? nam.Value : DateTime.Now.Year;

        try
        {
            Dictionary<int, double> scoreByEmployee = new();

            if (maKpi > 0)
            {
                var rows = await _dbContext.KetQuaKpis
                    .AsNoTracking()
                    .Where(x => x.MaKpi == maKpi && x.thang == targetMonth && x.nam == targetYear && idList.Contains(x.MaNhanVien))
                    .GroupBy(x => x.MaNhanVien)
                    .Select(g => new { MaNhanVien = g.Key, Score = g.Average(v => (double)(v.DiemSo ?? 0)) })
                    .ToListAsync();

                scoreByEmployee = rows.ToDictionary(x => x.MaNhanVien, x => x.Score);
            }
            else
            {
                // Prefer aggregated KETQUAKPI_TONG
                var tongRows = await _dbContext.KetQuaKpiTongs
                    .AsNoTracking()
                    .Where(x => x.Thang == targetMonth && x.Nam == targetYear && idList.Contains(x.MaNhanVien))
                    .Select(x => new { x.MaNhanVien, Score = (double)x.DiemTong })
                    .ToListAsync();

                scoreByEmployee = tongRows.ToDictionary(x => x.MaNhanVien, x => x.Score);

                var missingIds = idList.Where(id => !scoreByEmployee.ContainsKey(id)).ToList();
                if (missingIds.Any())
                {
                    var fallback = await _dbContext.KetQuaKpis
                        .AsNoTracking()
                        .Where(x => x.thang == targetMonth && x.nam == targetYear && missingIds.Contains(x.MaNhanVien))
                        .GroupBy(x => x.MaNhanVien)
                        .Select(g => new { MaNhanVien = g.Key, Score = g.Average(v => (double)(v.DiemSo ?? 0)) })
                        .ToListAsync();

                    foreach (var f in fallback)
                    {
                        scoreByEmployee[f.MaNhanVien] = f.Score;
                    }
                }
            }

            return Ok(ApiResponse<object>.Ok(new { items = scoreByEmployee }));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail(ex.Message));
        }
    }

    public class KpiPhongBanDto
    {
        public int MaPhongBan { get; set; }
        public string? TenPhongBan { get; set; }
        public int thang { get; set; }
        public int nam { get; set; }
        public int MaKpi { get; set; }
        public double KpiTrungBinh { get; set; }
        public string XepLoai { get; set; } = string.Empty;
        public List<KpiPhongBanNhanVienItemDto> NhanViens { get; set; } = new();
    }

    public class KpiPhongBanNhanVienItemDto
    {
        public int MaNhanVien { get; set; }
        public string? HoTen { get; set; }
        public double Diem { get; set; }
        public string XepLoai { get; set; } = string.Empty;
    }

    public class KpiCatalogListDto
    {
        public List<KpiCatalogItemDto> Items { get; set; } = new();
        public int Page { get; set; }
        public int Size { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public double TongTrongSo { get; set; }
    }

    public class KpiCatalogItemDto
    {
        public int MaKpi { get; set; }
        public string? TenKpi { get; set; }
        public string? MoTa { get; set; }
        public double TrongSo { get; set; }
        public double TrongSoGoc { get; set; }
        public int MaLoaiKpi { get; set; }
        public string? TenLoaiKpi { get; set; }
        public string ApDung { get; set; } = string.Empty;
        public string TrangThai { get; set; } = "Inactive";
        public int SoDoiTuong { get; set; }
        public double DiemTrungBinh { get; set; }
    }

    public class KpiCatalogDetailDto
    {
        public int MaKpi { get; set; }
        public string? TenKpi { get; set; }
        public string? MoTa { get; set; }
        public double TrongSo { get; set; }
        public double TrongSoGoc { get; set; }
        public int MaLoaiKpi { get; set; }
        public string? TenLoaiKpi { get; set; }
        public string TrangThai { get; set; } = "Active";
        public List<KpiAssignmentDto> Assignments { get; set; } = new();
        public List<KpiResultRowDto> Results { get; set; } = new();
        public List<KpiTrendPointDto> Trend { get; set; } = new();
    }

    public class KpiAssignmentDto
    {
        public int ObjectId { get; set; }
        public string? TenDoiTuong { get; set; }
        public string Loai { get; set; } = "Nhân viên";
        public string? LoaiCode { get; set; }
        public double TrongSo { get; set; }
        public string TrangThai { get; set; } = "Active";
        public string? GhiChu { get; set; }
    }

    public class KpiResultRowDto
    {
        public int MaNhanVien { get; set; }
        public string? HoTen { get; set; }
        public double Diem { get; set; }
        public int thang { get; set; }
        public int nam { get; set; }
        public string XepLoai { get; set; } = string.Empty;
    }

    public class KpiTrendPointDto
    {
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    public class KpiTypeDto
    {
        public int MaLoaiKpi { get; set; }
        public string? TenLoaiKpi { get; set; }
    }

    public class KpiDashboardSummaryDto
    {
        public int thang { get; set; }
        public int nam { get; set; }
        public double KpiTrungBinh { get; set; }
        public int SoNhanVienDatKpi { get; set; }
        public int SoNhanVienKpiThap { get; set; }
        public int SoCongViecDungHan { get; set; }
        public int SoCongViecTreHan { get; set; }
        public List<KpiTopEmployeeDto> TopNhanViens { get; set; } = new();
    }

    public class KpiTopEmployeeDto
    {
        public int MaNhanVien { get; set; }
        public string? HoTen { get; set; }
        public double DiemKpi { get; set; }
    }

    public class KpiEmployeeListDto
    {
        public int thang { get; set; }
        public int nam { get; set; }
        public int Page { get; set; }
        public int Size { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public List<KpiEmployeeListItemDto> Items { get; set; } = new();
    }

    public class KpiEmployeeListItemDto
    {
        public int MaNhanVien { get; set; }
        public string? HoTen { get; set; }
        public int? MaPhongBan { get; set; }
        public string? TenPhongBan { get; set; }
        public double DiemKpi { get; set; }
        public int TongCongViec { get; set; }
        public int CongViecHoanThanh { get; set; }
        public int CongViecTreHan { get; set; }
        public string? MucKpi { get; set; }
    }

    public class KpiEmployeeDetailDto
    {
        public int MaNhanVien { get; set; }
        public string? HoTen { get; set; }
        public int? MaPhongBan { get; set; }
        public string? TenPhongBan { get; set; }
        public int thang { get; set; }
        public int nam { get; set; }
        public double DiemKpiHienTai { get; set; }
        public string? MucKpi { get; set; }
        public int TongCongViec { get; set; }
        public int CongViecHoanThanh { get; set; }
        public int CongViecDungHan { get; set; }
        public int CongViecTreHan { get; set; }
        public List<KpiDiemKyDto> KpiTheoThang { get; set; } = new();
        public List<KpiEmployeeAppliedWeightDto> KpiTrongSoApDung { get; set; } = new();
        public List<KpiEmployeeScoreBreakdownDto> KpiDiemChiTiet { get; set; } = new();
    }

    public class KpiEmployeeAppliedWeightDto
    {
        public int MaKpi { get; set; }
        public string? TenKpi { get; set; }
        public string? TenLoaiKpi { get; set; }
        public double TrongSoGoc { get; set; }
        public double TrongSoApDung { get; set; }
        public DateTime? TuNgay { get; set; }
        public DateTime? DenNgay { get; set; }
        public bool IsActive { get; set; }
    }

    public class KpiEmployeeScoreBreakdownDto
    {
        public int MaKpi { get; set; }
        public string? TenKpi { get; set; }
        public double RuleScore { get; set; }
        public double? AiScore { get; set; }
        public double FinalScore { get; set; }
        public double TrongSoApDung { get; set; }
        public double DongGop { get; set; }
        public bool CoSuDungAi { get; set; }
    }

    public class KpiTeamSummaryDto
    {
        public int thang { get; set; }
        public int nam { get; set; }
        public double KpiTrungBinhNhom { get; set; }
        public double KpiTrungBinhPhongBan { get; set; }
        public KpiTeamSummaryItemDto? NhomTotNhat { get; set; }
        public KpiTeamSummaryItemDto? NhomKpiThap { get; set; }
        public List<KpiTeamSummaryItemDto> Teams { get; set; } = new();
        public List<KpiDepartmentSummaryItemDto> Departments { get; set; } = new();
    }

    public class KpiTeamSummaryItemDto
    {
        public int MaNhom { get; set; }
        public string? TenNhom { get; set; }
        public int SoThanhVien { get; set; }
        public double KpiTrungBinh { get; set; }
        public string? MucKpi { get; set; }
    }

    public class KpiDepartmentSummaryItemDto
    {
        public int? MaPhongBan { get; set; }
        public string? TenPhongBan { get; set; }
        public int SoNhanVien { get; set; }
        public double KpiTrungBinh { get; set; }
        public string? MucKpi { get; set; }
    }

    public class KpiScopeDto
    {
        public string RoleKey { get; set; } = "employee";
        public int? MaNhanVien { get; set; }
        public int? MaPhongBan { get; set; }
    }

    public class KpiExportOverviewRowDto
    {
        public int MaKpi { get; set; }
        public string? TenKpi { get; set; }
        public double TrongSoGoc { get; set; }
        public string? LoaiKpi { get; set; }
        public string? TrangThai { get; set; }
        public int SoGanNhanVien { get; set; }
        public int SoGanNhom { get; set; }
        public int SoGanPhongBan { get; set; }
        public int SoGanDuAn { get; set; }
        public int SoNhanVienCoDiem { get; set; }
        public double DiemTrungBinh { get; set; }
        public double DiemCaoNhat { get; set; }
        public double DiemThapNhat { get; set; }
    }

    public class KpiExportDetailRowDto
    {
        public int MaKpi { get; set; }
        public string? TenKpi { get; set; }
        public string? LoaiKpi { get; set; }
        public string LoaiApDung { get; set; } = "NhanVien";
        public int MaDoiTuong { get; set; }
        public string? TenDoiTuong { get; set; }
        public double TrongSoGoc { get; set; }
        public double TrongSoApDung { get; set; }
        public bool IsActive { get; set; }
        public DateTime? TuNgay { get; set; }
        public DateTime? DenNgay { get; set; }
        public double DiemThanhPhan { get; set; }
        public double DongGop { get; set; }
    }

    public class KpiTeamOptionDto
    {
        public int MaNhom { get; set; }
        public string? TenNhom { get; set; }
    }

    private class KpiAssignmentCountRow
    {
        public int MaKpi { get; set; }
        public int Count { get; set; }
    }

    public class SaveKpiCatalogRequest
    {
        public string TenKpi { get; set; } = string.Empty;
        public string? MoTa { get; set; }
        public double? TrongSo { get; set; }
        public double? TrongSoGoc { get; set; }
        public int MaLoaiKpi { get; set; }
        public int? thang { get; set; }
        public int? nam { get; set; }
    }

    public class SaveKpiAssignmentRequest
    {
        public string? LoaiCode { get; set; }
        public int ObjectId { get; set; }
        public DateTime? TuNgay { get; set; }
        public DateTime? DenNgay { get; set; }
        public byte? TrangThai { get; set; }
        public string? GhiChu { get; set; }
    }

    public class SaveKpiProposalRequest
    {
        public int? MaKpi { get; set; }
        public int? MaLoaiKpi { get; set; }
        public string? LoaiDeXuat { get; set; }
        public int? MaNhanVienApDung { get; set; }
        public int? MaNhomApDung { get; set; }
        public int? MaPhongBanApDung { get; set; }
        public int? MaDuAnApDung { get; set; }
        public DateTime TuNgay { get; set; }
        public DateTime? DenNgay { get; set; }
        public decimal TrongSoDeXuat { get; set; }
        public string? TenKpiDeXuat { get; set; }
        public string? MoTaKpiDeXuat { get; set; }
        public string? LyDo { get; set; }
        public string? GhiChu { get; set; }
    }

    public class ReviewKpiProposalRequest
    {
        public string? Action { get; set; }
        public string? PhanHoiAdmin { get; set; }
    }

    public class KpiProposalDto
    {
        public int MaDeXuat { get; set; }
        public int? MaKpi { get; set; }
        public int? MaLoaiKpi { get; set; }
        public int NguoiDeXuat { get; set; }
        public string? TenNguoiDeXuat { get; set; }
        public int? NguoiDuyet { get; set; }
        public string? TenNguoiDuyet { get; set; }
        public string LoaiDeXuat { get; set; } = string.Empty;
        public int? MaNhanVienApDung { get; set; }
        public int? MaNhomApDung { get; set; }
        public int? MaPhongBanApDung { get; set; }
        public int? MaDuAnApDung { get; set; }
        public DateTime TuNgay { get; set; }
        public DateTime? DenNgay { get; set; }
        public decimal TrongSoDeXuat { get; set; }
        public string? TenKpiDeXuat { get; set; }
        public string? MoTaKpiDeXuat { get; set; }
        public string? LyDo { get; set; }
        public string TrangThai { get; set; } = string.Empty;
        public string? PhanHoiAdmin { get; set; }
        public string? GhiChu { get; set; }
        public DateTime NgayTao { get; set; }
        public DateTime? NgayCapNhat { get; set; }
        public DateTime? NgayDuyet { get; set; }
    }

    private static class KpiProposalType
    {
        public const string TaoMoiKPI = "TaoMoiKPI";
        public const string ApDungKPI = "ApDungKPI";
        public const string DieuChinhKPI = "DieuChinhKPI";
        public const string HuyApDungKPI = "HuyApDungKPI";
    }

    private static class KpiProposalStatus
    {
        public const string ChoDuyet = "ChoDuyet";
        public const string DaDuyet = "DaDuyet";
        public const string TuChoi = "TuChoi";
        public const string CanChinhSua = "CanChinhSua";
    }

    private static class KpiProposalAction
    {
        public const string Duyet = "Duyet";
        public const string TuChoi = "TuChoi";
        public const string YeuCauChinhSua = "YeuCauChinhSua";
    }

    private static double ResolveCatalogBaseWeight(SaveKpiCatalogRequest request)
    {
        if (request.TrongSoGoc.HasValue)
        {
            return request.TrongSoGoc.Value;
        }

        if (request.TrongSo.HasValue)
        {
            return request.TrongSo.Value;
        }

        return 0;
    }

    public class CalculateAllKpiRequest
    {
        public int thang { get; set; }
        public int nam { get; set; }
        public int? MaNhanVien { get; set; }
        public int? MaPhongBan { get; set; }
    }

    public class CalculateAllKpiResult
    {
        public int thang { get; set; }
        public int nam { get; set; }
        public int TongKPI { get; set; }
        public int TongNhanVien { get; set; }
        public int TongTaskTrongKy { get; set; }
        public int SoBanGhiTaoMoi { get; set; }
        public int SoBanGhiCapNhat { get; set; }
    }
}

