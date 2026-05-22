using LuanVan.Contracts;
using LuanVan.Data;
using LuanVan.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LuanVan.Controllers.Api;

[Authorize]
[ApiController]
[Route("kpi/my")]
[Route("api/kpi/my")]
[Authorize(Policy = Permissions.MyKpiView)]
public class MyKpiController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;

    public MyKpiController(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _configuration = configuration;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<ApiResponse<MyKpiOverviewDto>>> GetOverview([FromQuery] int? thang, [FromQuery] int? nam)
    {
        EnsureDbConnectionStringInitialized();
        var actor = await ResolveCurrentEmployeeAsync();
        if (!actor.Success)
        {
            return StatusCode(actor.StatusCode, ApiResponse<MyKpiOverviewDto>.Fail(actor.ErrorMessage));
        }

        var month = thang is >= 1 and <= 12 ? thang.Value : DateTime.Now.Month;
        var year = nam is > 2000 and <= 3000 ? nam.Value : DateTime.Now.Year;

        var tong = await _dbContext.KetQuaKpiTongs
            .AsNoTracking()
            .Where(x => x.MaNhanVien == actor.MaNhanVien && x.Thang == month && x.Nam == year)
            .OrderByDescending(x => x.NgayTinh)
            .FirstOrDefaultAsync();

        var applied = await BuildAppliedRowsAsync(actor.MaNhanVien, month, year);

        var dto = new MyKpiOverviewDto
        {
            Thang = month,
            Nam = year,
            DiemTong = tong?.DiemTong,
            XepLoai = tong?.XepLoai,
            NgayTinh = tong?.NgayTinh,
            SoKpiDangApDung = applied.Count
        };

        return Ok(ApiResponse<MyKpiOverviewDto>.Ok(dto));
    }

    [HttpGet("applied")]
    public async Task<ActionResult<ApiResponse<List<MyKpiAppliedItemDto>>>> GetApplied([FromQuery] int? thang, [FromQuery] int? nam)
    {
        EnsureDbConnectionStringInitialized();
        var actor = await ResolveCurrentEmployeeAsync();
        if (!actor.Success)
        {
            return StatusCode(actor.StatusCode, ApiResponse<List<MyKpiAppliedItemDto>>.Fail(actor.ErrorMessage));
        }

        var month = thang is >= 1 and <= 12 ? thang.Value : DateTime.Now.Month;
        var year = nam is > 2000 and <= 3000 ? nam.Value : DateTime.Now.Year;
        var rows = await BuildAppliedRowsAsync(actor.MaNhanVien, month, year);
        return Ok(ApiResponse<List<MyKpiAppliedItemDto>>.Ok(rows));
    }

    [HttpGet("history")]
    public async Task<ActionResult<ApiResponse<List<MyKpiHistoryItemDto>>>> GetHistory([FromQuery] int? months = 6)
    {
        EnsureDbConnectionStringInitialized();
        var actor = await ResolveCurrentEmployeeAsync();
        if (!actor.Success)
        {
            return StatusCode(actor.StatusCode, ApiResponse<List<MyKpiHistoryItemDto>>.Fail(actor.ErrorMessage));
        }

        var take = Math.Clamp(months ?? 6, 1, 24);
        var rows = await _dbContext.KetQuaKpiTongs
            .AsNoTracking()
            .Where(x => x.MaNhanVien == actor.MaNhanVien)
            .OrderByDescending(x => x.Nam)
            .ThenByDescending(x => x.Thang)
            .Take(take)
            .Select(x => new MyKpiHistoryItemDto
            {
                Thang = x.Thang,
                Nam = x.Nam,
                DiemTong = x.DiemTong,
                XepLoai = x.XepLoai,
                SoKpiThanhPhan = x.SoKpiThanhPhan
            })
            .ToListAsync();

        rows = rows.OrderBy(x => x.Nam).ThenBy(x => x.Thang).ToList();
        return Ok(ApiResponse<List<MyKpiHistoryItemDto>>.Ok(rows));
    }

    [HttpPost("feedback")]
    public async Task<ActionResult<ApiResponse<object>>> CreateFeedback([FromBody] MyKpiFeedbackRequest? request)
    {
        EnsureDbConnectionStringInitialized();
        var actor = await ResolveCurrentEmployeeAsync();
        if (!actor.Success)
        {
            return StatusCode(actor.StatusCode, ApiResponse<object>.Fail(actor.ErrorMessage));
        }

        request ??= new MyKpiFeedbackRequest();
        request.NoiDung = (request.NoiDung ?? string.Empty).Trim();
        request.MucDo = (request.MucDo ?? string.Empty).Trim();

        if (request.Thang is < 1 or > 12)
        {
            return BadRequest(ApiResponse<object>.Fail("Tháng không hợp lệ."));
        }

        if (request.Nam is < 2000 or > 3000)
        {
            return BadRequest(ApiResponse<object>.Fail("Năm không hợp lệ."));
        }

        if (string.IsNullOrWhiteSpace(request.NoiDung) || request.NoiDung.Length > 500)
        {
            return BadRequest(ApiResponse<object>.Fail("Nội dung phản hồi là bắt buộc và tối đa 500 ký tự."));
        }

        var allowedLevels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "BinhThuong", "CanXemLai", "KhanCap" };
        if (!allowedLevels.Contains(request.MucDo))
        {
            return BadRequest(ApiResponse<object>.Fail("Mức độ phản hồi không hợp lệ."));
        }

        if (request.MaKpi.HasValue)
        {
            var applied = await BuildAppliedRowsAsync(actor.MaNhanVien, request.Thang, request.Nam);
            if (!applied.Any(x => x.MaKpi == request.MaKpi.Value))
            {
                return BadRequest(ApiResponse<object>.Fail("KPI không thuộc phạm vi áp dụng của bạn trong kỳ đã chọn."));
            }
        }

        var recipients = await ResolveFeedbackRecipientsAsync(actor.MaNhanVien);
        if (recipients.Count == 0)
        {
            return BadRequest(ApiResponse<object>.Fail("Không tìm thấy người nhận phản hồi."));
        }

        var loaiThongBao = await GetOrCreateNotificationTypeAsync("KPI Feedback");
        var noiDung = $"[KPI Feedback] NV {actor.MaNhanVien} gửi phản hồi kỳ {request.Thang}/{request.Nam}"
                      + (request.MaKpi.HasValue ? $" cho KPI #{request.MaKpi.Value}" : string.Empty)
                      + $". Mức độ: {request.MucDo}. Nội dung: {request.NoiDung}";

        var tb = new ThongBao
        {
            MaLoai = loaiThongBao.MaLoai,
            NoiDung = noiDung.Length > 300 ? noiDung[..300] : noiDung,
            ThoiGian = DateTime.Now
        };
        _dbContext.ThongBaos.Add(tb);
        await _dbContext.SaveChangesAsync();

        _dbContext.ThongBaoNhanViens.AddRange(recipients.Select(x => new ThongBaoNhanVien
        {
            MaThongBao = tb.MaThongBao,
            MaNhanVien = x,
            DaDoc = false
        }));
        await _dbContext.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { sentTo = recipients.Count }, "Đã gửi phản hồi thành công."));
    }

    private async Task<List<MyKpiAppliedItemDto>> BuildAppliedRowsAsync(int maNhanVien, int thang, int nam)
    {
        var refDate = ResolveReferenceDate(thang, nam);

        var employee = await _dbContext.NhanViens.AsNoTracking()
            .Where(x => x.MaNhanVien == maNhanVien)
            .Select(x => new { x.MaNhanVien, x.MaPhongBan })
            .FirstOrDefaultAsync();
        if (employee == null)
        {
            return new List<MyKpiAppliedItemDto>();
        }

        var teamIds = await _dbContext.ThanhVienNhoms.AsNoTracking()
            .Where(x => x.MaNhanVien == maNhanVien)
            .Select(x => x.MaNhom)
            .Distinct()
            .ToListAsync();

        var projectIds = await _dbContext.DuAnNhanViens.AsNoTracking()
            .Where(x => x.MaNhanVien == maNhanVien && (x.NgayRoi == null || x.NgayRoi >= refDate))
            .Select(x => x.MaDuAn)
            .Distinct()
            .ToListAsync();

        var directRows = await (
            from a in _dbContext.KpiNhanViens.AsNoTracking()
            join k in _dbContext.DanhMucKpis.AsNoTracking() on a.MaKpi equals k.MaKpi
            join t in _dbContext.LoaiKpis.AsNoTracking() on k.MaLoaiKpi equals t.MaLoaiKpi into tJoin
            from t in tJoin.DefaultIfEmpty()
            where a.MaNhanVien == maNhanVien && a.IsActive
            select new MyKpiAppliedSourceRow
            {
                MaKpi = a.MaKpi,
                TenKpi = k.TenKpi,
                TenLoaiKpi = t != null ? t.TenLoaiKpi : null,
                TrangThaiKpi = (k.TrangThai ?? 1) == 1 ? "Active" : "Inactive",
                LoaiNguon = "CaNhan",
                TenNguon = "Cá nhân",
                TrongSoApDung = a.TrongSoApDung,
                TuNgay = a.TuNgay,
                DenNgay = a.DenNgay
            }).ToListAsync();

        var teamRows = teamIds.Count == 0
            ? new List<MyKpiAppliedSourceRow>()
            : await (
                from a in _dbContext.KpiNhoms.AsNoTracking()
                join n in _dbContext.Nhoms.AsNoTracking() on a.MaNhom equals n.MaNhom
                join k in _dbContext.DanhMucKpis.AsNoTracking() on a.MaKpi equals k.MaKpi
                join t in _dbContext.LoaiKpis.AsNoTracking() on k.MaLoaiKpi equals t.MaLoaiKpi into tJoin
                from t in tJoin.DefaultIfEmpty()
                where teamIds.Contains(a.MaNhom) && a.IsActive
                select new MyKpiAppliedSourceRow
                {
                    MaKpi = a.MaKpi,
                    TenKpi = k.TenKpi,
                    TenLoaiKpi = t != null ? t.TenLoaiKpi : null,
                    TrangThaiKpi = (k.TrangThai ?? 1) == 1 ? "Active" : "Inactive",
                    LoaiNguon = "Nhom",
                    TenNguon = n.TenNhom ?? $"Nhóm {n.MaNhom}",
                    TrongSoApDung = a.TrongSoApDung,
                    TuNgay = a.TuNgay,
                    DenNgay = a.DenNgay
                }).ToListAsync();

        var departmentRows = !employee.MaPhongBan.HasValue
            ? new List<MyKpiAppliedSourceRow>()
            : await (
                from a in _dbContext.KpiPhongBans.AsNoTracking()
                join pb in _dbContext.PhongBans.AsNoTracking() on a.MaPhongBan equals pb.MaPhongBan
                join k in _dbContext.DanhMucKpis.AsNoTracking() on a.MaKpi equals k.MaKpi
                join t in _dbContext.LoaiKpis.AsNoTracking() on k.MaLoaiKpi equals t.MaLoaiKpi into tJoin
                from t in tJoin.DefaultIfEmpty()
                where a.MaPhongBan == employee.MaPhongBan.Value && a.IsActive
                select new MyKpiAppliedSourceRow
                {
                    MaKpi = a.MaKpi,
                    TenKpi = k.TenKpi,
                    TenLoaiKpi = t != null ? t.TenLoaiKpi : null,
                    TrangThaiKpi = (k.TrangThai ?? 1) == 1 ? "Active" : "Inactive",
                    LoaiNguon = "PhongBan",
                    TenNguon = pb.TenPhongBan ?? $"Phòng ban {pb.MaPhongBan}",
                    TrongSoApDung = a.TrongSoApDung,
                    TuNgay = a.TuNgay,
                    DenNgay = a.DenNgay
                }).ToListAsync();

        var projectRows = projectIds.Count == 0
            ? new List<MyKpiAppliedSourceRow>()
            : await (
                from a in _dbContext.KpiDuAns.AsNoTracking()
                join da in _dbContext.DuAns.AsNoTracking() on a.MaDuAn equals da.MaDuAn
                join k in _dbContext.DanhMucKpis.AsNoTracking() on a.MaKpi equals k.MaKpi
                join t in _dbContext.LoaiKpis.AsNoTracking() on k.MaLoaiKpi equals t.MaLoaiKpi into tJoin
                from t in tJoin.DefaultIfEmpty()
                where projectIds.Contains(a.MaDuAn) && a.IsActive
                select new MyKpiAppliedSourceRow
                {
                    MaKpi = a.MaKpi,
                    TenKpi = k.TenKpi,
                    TenLoaiKpi = t != null ? t.TenLoaiKpi : null,
                    TrangThaiKpi = (k.TrangThai ?? 1) == 1 ? "Active" : "Inactive",
                    LoaiNguon = "DuAn",
                    TenNguon = da.TenDuAn ?? $"Dự án {da.MaDuAn}",
                    TrongSoApDung = a.TrongSoApDung,
                    TuNgay = a.TuNgay,
                    DenNgay = a.DenNgay
                }).ToListAsync();

        var sourceRows = directRows
            .Concat(teamRows)
            .Concat(departmentRows)
            .Concat(projectRows)
            .ToList();

        var scores = await _dbContext.KetQuaKpis.AsNoTracking()
            .Where(x => x.MaNhanVien == maNhanVien && x.thang == thang && x.nam == nam)
            .Select(x => new { x.MaKpi, x.DiemSo })
            .ToListAsync();

        var scoreMap = scores
            .GroupBy(x => x.MaKpi)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.DiemSo).Select(x => x.DiemSo).FirstOrDefault());

        var result = sourceRows
            .GroupBy(x => x.MaKpi)
            .Select(g =>
            {
                var first = g.First();
                var sources = g
                    .Select(s => new MyKpiAppliedSourceDto
                    {
                        LoaiNguon = s.LoaiNguon,
                        TenNguon = s.TenNguon,
                        TrongSoApDung = s.TrongSoApDung,
                        TuNgay = s.TuNgay,
                        DenNgay = s.DenNgay,
                        TrangThaiHieuLuc = ResolveEffectStatus(s.TuNgay, s.DenNgay, refDate)
                    })
                    .OrderBy(x => x.LoaiNguon)
                    .ThenBy(x => x.TenNguon)
                    .ToList();

                var minTu = sources.Where(x => x.TuNgay.HasValue).Select(x => x.TuNgay!.Value).DefaultIfEmpty().Min();
                var maxDen = sources.Where(x => x.DenNgay.HasValue).Select(x => x.DenNgay!.Value).DefaultIfEmpty().Max();
                DateTime? minTuVal = minTu == default ? null : minTu;
                DateTime? maxDenVal = maxDen == default ? null : maxDen;

                scoreMap.TryGetValue(g.Key, out var diem);
                var sourceLabels = string.Join(", ", sources.Select(x => ToSourceLabel(x.LoaiNguon)).Distinct());

                return new MyKpiAppliedItemDto
                {
                    MaKpi = g.Key,
                    TenKpi = first.TenKpi ?? $"KPI {g.Key}",
                    TenLoaiKpi = first.TenLoaiKpi,
                    NguonApDung = sources,
                    NguonApDungText = sourceLabels,
                    DiemHienTai = diem,
                    TrangThaiKpi = first.TrangThaiKpi,
                    TrangThaiHieuLuc = ResolveEffectStatus(minTuVal, maxDenVal, refDate),
                    TuNgay = minTuVal,
                    DenNgay = maxDenVal
                };
            })
            .OrderBy(x => x.TenKpi)
            .ToList();

        return result;
    }

    private static string ResolveEffectStatus(DateTime? tuNgay, DateTime? denNgay, DateTime refDate)
    {
        if (denNgay.HasValue && denNgay.Value.Date < refDate.Date)
        {
            return "HetHieuLuc";
        }

        if (tuNgay.HasValue && tuNgay.Value.Date > refDate.Date)
        {
            return "ChuaBatDau";
        }

        var isActive = (!tuNgay.HasValue || tuNgay.Value.Date <= refDate.Date)
                       && (!denNgay.HasValue || denNgay.Value.Date >= refDate.Date);

        if (isActive && denNgay.HasValue)
        {
            var daysLeft = (denNgay.Value.Date - refDate.Date).TotalDays;
            if (daysLeft >= 0 && daysLeft <= 7)
            {
                return "SapHetHan";
            }
        }

        return "DangApDung";
    }

    private static string ToSourceLabel(string? key)
    {
        return (key ?? string.Empty) switch
        {
            "CaNhan" => "Cá nhân",
            "Nhom" => "Nhóm",
            "PhongBan" => "Phòng ban",
            "DuAn" => "Dự án",
            _ => "Khác"
        };
    }

    private async Task<List<int>> ResolveFeedbackRecipientsAsync(int maNhanVien)
    {
        var managerIds = await (
            from nv in _dbContext.NhanViens.AsNoTracking()
            join ur in _dbContext.UserRoles.AsNoTracking() on nv.AspNetUserId equals ur.UserId
            join r in _dbContext.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where r.Name == Roles.Manager || r.Name == Roles.Admin
            select nv.MaNhanVien
        ).Distinct().ToListAsync();

        if (managerIds.Count == 0)
        {
            managerIds = await _dbContext.NhanViens.AsNoTracking()
                .Where(x => x.MaNhanVien != maNhanVien && x.MaPhongBan != null)
                .Select(x => x.MaNhanVien)
                .Take(5)
                .ToListAsync();
        }

        return managerIds.Distinct().ToList();
    }

    private async Task<LoaiThongBao> GetOrCreateNotificationTypeAsync(string tenLoai)
    {
        var loai = await _dbContext.LoaiThongBaos.FirstOrDefaultAsync(x => x.TenLoai == tenLoai);
        if (loai != null)
        {
            return loai;
        }

        loai = new LoaiThongBao { TenLoai = tenLoai };
        _dbContext.LoaiThongBaos.Add(loai);
        try
        {
            await _dbContext.SaveChangesAsync();
            return loai;
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IDENTITY_INSERT", StringComparison.OrdinalIgnoreCase) == true)
        {
            _dbContext.Entry(loai).State = EntityState.Detached;
            var nextId = (await _dbContext.LoaiThongBaos.MaxAsync(x => (int?)x.MaLoai) ?? 0) + 1;
            loai = new LoaiThongBao { MaLoai = nextId, TenLoai = tenLoai };
            _dbContext.LoaiThongBaos.Add(loai);
            await _dbContext.SaveChangesAsync();
            return loai;
        }
    }

    private static DateTime ResolveReferenceDate(int thang, int nam)
    {
        var today = DateTime.Today;
        if (today.Month == thang && today.Year == nam)
        {
            return today;
        }

        return new DateTime(nam, thang, DateTime.DaysInMonth(nam, thang));
    }

    private async Task<(bool Success, int StatusCode, string ErrorMessage, int MaNhanVien)> ResolveCurrentEmployeeAsync()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return (false, StatusCodes.Status401Unauthorized, "Không xác định người dùng.", 0);
        }

        var employeeId = await _dbContext.NhanViens.AsNoTracking()
            .Where(x => x.AspNetUserId == userId)
            .Select(x => (int?)x.MaNhanVien)
            .FirstOrDefaultAsync();

        if (!employeeId.HasValue)
        {
            return (false, StatusCodes.Status401Unauthorized, "Tài khoản chưa liên kết nhân viên.", 0);
        }

        return (true, StatusCodes.Status200OK, string.Empty, employeeId.Value);
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

    private sealed class MyKpiAppliedSourceRow
    {
        public int MaKpi { get; set; }
        public string? TenKpi { get; set; }
        public string? TenLoaiKpi { get; set; }
        public string? TrangThaiKpi { get; set; }
        public string LoaiNguon { get; set; } = string.Empty;
        public string TenNguon { get; set; } = string.Empty;
        public decimal? TrongSoApDung { get; set; }
        public DateTime? TuNgay { get; set; }
        public DateTime? DenNgay { get; set; }
    }

    public class MyKpiOverviewDto
    {
        public int Thang { get; set; }
        public int Nam { get; set; }
        public decimal? DiemTong { get; set; }
        public string? XepLoai { get; set; }
        public int SoKpiDangApDung { get; set; }
        public DateTime? NgayTinh { get; set; }
    }

    public class MyKpiAppliedItemDto
    {
        public int MaKpi { get; set; }
        public string TenKpi { get; set; } = string.Empty;
        public string? TenLoaiKpi { get; set; }
        public string? NguonApDungText { get; set; }
        public decimal? DiemHienTai { get; set; }
        public string? TrangThaiKpi { get; set; }
        public string TrangThaiHieuLuc { get; set; } = "DangApDung";
        public DateTime? TuNgay { get; set; }
        public DateTime? DenNgay { get; set; }
        public List<MyKpiAppliedSourceDto> NguonApDung { get; set; } = new();
    }

    public class MyKpiAppliedSourceDto
    {
        public string LoaiNguon { get; set; } = string.Empty;
        public string TenNguon { get; set; } = string.Empty;
        public decimal? TrongSoApDung { get; set; }
        public DateTime? TuNgay { get; set; }
        public DateTime? DenNgay { get; set; }
        public string TrangThaiHieuLuc { get; set; } = "DangApDung";
    }

    public class MyKpiHistoryItemDto
    {
        public int Thang { get; set; }
        public int Nam { get; set; }
        public decimal DiemTong { get; set; }
        public string? XepLoai { get; set; }
        public int SoKpiThanhPhan { get; set; }
    }

    public class MyKpiFeedbackRequest
    {
        public int Thang { get; set; }
        public int Nam { get; set; }
        public int? MaKpi { get; set; }
        public string NoiDung { get; set; } = string.Empty;
        public string MucDo { get; set; } = "BinhThuong";
    }
}
