using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Text.Json;
using LuanVan.Contracts;
using LuanVan.Data;
using LuanVan.Models;
using LuanVan.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LuanVan.Controllers.Api;

[ApiController]
[Route("nhanvien")]
[Authorize]
public class NhanVienController : ControllerBase
{
    private static readonly Regex CccdRegex = new("^\\d{12}$", RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new("^0\\d{9}$", RegexOptions.Compiled);
    private static readonly string[] SensitiveProfileFields = ["EMAIL", "HOTEN", "NGAYSINH", "CCCD"];

    private readonly AppDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogService _auditLogService;
    private readonly IConfiguration _configuration;

    public NhanVienController(AppDbContext dbContext, UserManager<ApplicationUser> userManager, IAuditLogService auditLogService, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _auditLogService = auditLogService;
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
        public string UserId { get; set; } = string.Empty;
        public int MaNhanVien { get; set; }
        public int? MaPhongBan { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsManager { get; set; }
        public bool IsEmployee { get; set; }
    }

    private async Task<ActorContext?> GetActorContextAsync()
    {
        EnsureDbConnectionStringInitialized();
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var actor = await _dbContext.NhanViens
            .AsNoTracking()
            .Where(x => x.AspNetUserId == userId)
            .Select(x => new ActorContext
            {
                UserId = userId,
                MaNhanVien = x.MaNhanVien,
                MaPhongBan = x.MaPhongBan,
                IsAdmin = User.IsInRole("Admin"),
                IsManager = User.IsInRole("Manager"),
                IsEmployee = User.IsInRole("Employee")
            })
            .FirstOrDefaultAsync();

        return actor;
    }

    private static bool IsManagerScopeAllowed(ActorContext actor, int? targetMaPhongBan)
    {
        if (actor.IsAdmin)
        {
            return true;
        }

        if (!actor.IsManager)
        {
            return false;
        }

        return actor.MaPhongBan.HasValue
            && targetMaPhongBan.HasValue
            && actor.MaPhongBan.Value == targetMaPhongBan.Value;
    }

    private async Task WriteAuditAsync(ActorContext actor, string action)
    {
        await _auditLogService.LogByUserIdAsync(actor.UserId, action);
    }

    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<NhanVienListItemDto>>> GetCurrentNhanVien()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(ApiResponse<NhanVienListItemDto>.Fail("Bạn chưa đăng nhập."));
        }

        var current = await _dbContext.NhanViens
            .AsNoTracking()
            .Where(x => x.AspNetUserId == userId)
            .Select(x => new NhanVienListItemDto
            {
                MaNhanVien = x.MaNhanVien,
                HoTen = x.HoTen,
                Email = x.Email,
                Sdt = x.Sdt,
                TrangThai = x.TrangThai ?? 0,
                MaPhongBan = x.MaPhongBan,
                TenPhongBan = x.PhongBanQuanLy != null ? x.PhongBanQuanLy.TenPhongBan : null,
                NgayVaoLam = x.NgayVaoLam
            })
            .FirstOrDefaultAsync();

        if (current == null)
        {
            return NotFound(ApiResponse<NhanVienListItemDto>.Fail("Không tìm thấy hồ sơ nhân viên cho tài khoản hiện tại."));
        }

        // Fallback for manager/head accounts that are linked as MaTruongPhong but may have null MaPhongBan in NhanVien.
        if (!current.MaPhongBan.HasValue || string.IsNullOrWhiteSpace(current.TenPhongBan))
        {
            var managedDepartment = await _dbContext.PhongBans
                .AsNoTracking()
                .Where(x => x.MaTruongPhong == current.MaNhanVien)
                .OrderBy(x => x.MaPhongBan)
                .Select(x => new { x.MaPhongBan, x.TenPhongBan })
                .FirstOrDefaultAsync();

            if (managedDepartment != null)
            {
                current.MaPhongBan ??= managedDepartment.MaPhongBan;
                if (string.IsNullOrWhiteSpace(current.TenPhongBan))
                {
                    current.TenPhongBan = managedDepartment.TenPhongBan;
                }
            }
        }

        return Ok(ApiResponse<NhanVienListItemDto>.Ok(current));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<NhanVienListItemDto>>>> GetNhanViens(
        [FromQuery] string? keyword,
        [FromQuery] int? phongban,
        [FromQuery] int? chucvu,
        [FromQuery] int? trangthai,
        [FromQuery] int? skillId,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir,
        [FromQuery] int page = 1,
        [FromQuery] int size = 10)
    {
        try
        {
            var actor = await GetActorContextAsync();
            if (actor == null)
            {
                return Unauthorized(ApiResponse<PagedResult<NhanVienListItemDto>>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
            }

        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 100);

        IQueryable<NhanVien> query = _dbContext.NhanViens.AsNoTracking();

        if (actor.IsEmployee && !actor.IsAdmin && !actor.IsManager)
        {
            query = query.Where(x => x.TrangThai == 1);
        }
        else if (actor.IsManager && actor.MaPhongBan.HasValue)
        {
            query = query.Where(x => x.MaPhongBan == actor.MaPhongBan.Value);
        }

        if (phongban.HasValue)
        {
            query = query.Where(x => x.MaPhongBan == phongban.Value);
        }

        if (chucvu.HasValue)
        {
            query = query.Where(x => x.MaChucVu == chucvu.Value);
        }

        if (trangthai.HasValue)
        {
            query = query.Where(x => x.TrangThai == trangthai.Value);
        }

        if (skillId.HasValue)
        {
            query = query.Where(x => x.KyNangNhanViens.Any(k => k.MaKyNang == skillId.Value));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(x =>
                (x.HoTen != null && EF.Functions.Like(EF.Functions.Collate(x.HoTen, "Latin1_General_CI_AI"), $"%{k}%")) ||
                (x.Email != null && EF.Functions.Like(x.Email, $"%{k}%")) ||
                (x.Cccd != null && EF.Functions.Like(x.Cccd, $"%{k}%")) ||
                (x.Sdt != null && EF.Functions.Like(x.Sdt, $"%{k}%")));
        }

        var totalItems = await query.CountAsync();

        var normalizedSortBy = (sortBy ?? "name").Trim().ToLowerInvariant();
        var isDesc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);

        query = (normalizedSortBy, isDesc) switch
        {
            ("department", true) => query
                .OrderByDescending(x => x.PhongBanQuanLy != null ? x.PhongBanQuanLy.TenPhongBan : string.Empty)
                .ThenBy(x => x.HoTen),
            ("department", false) => query
                .OrderBy(x => x.PhongBanQuanLy != null ? x.PhongBanQuanLy.TenPhongBan : string.Empty)
                .ThenBy(x => x.HoTen),
            (_, true) => query.OrderByDescending(x => x.HoTen),
            _ => query.OrderBy(x => x.HoTen)
        };

        var items = await query
            .Skip((page - 1) * size)
            .Take(size)
            .Select(x => new NhanVienListItemDto
            {
                MaNhanVien = x.MaNhanVien,
                HoTen = x.HoTen,
                Email = x.Email,
                Sdt = x.Sdt,
                TrangThai = x.TrangThai ?? 0,
                MaPhongBan = x.MaPhongBan,
                TenPhongBan = x.PhongBanQuanLy != null ? x.PhongBanQuanLy.TenPhongBan : null,
                MaChucVu = x.MaChucVu,
                TenChucVu = x.ChucVu != null ? x.ChucVu.TenChucVu : null,
                NgayVaoLam = x.NgayVaoLam
            })
            .ToListAsync();

        var result = new PagedResult<NhanVienListItemDto>
        {
            Items = items,
            Page = page,
            Size = size,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)size)
        };

            return Ok(ApiResponse<PagedResult<NhanVienListItemDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<PagedResult<NhanVienListItemDto>>.Fail($"Không tải được danh sách nhân viên: {ex.Message}"));
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<NhanVienDetailDto>>> GetNhanVienById(int id)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<NhanVienDetailDto>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        var isSelfProfile = actor.IsEmployee && actor.MaNhanVien == id;

        if (actor.IsEmployee && !actor.IsAdmin && !actor.IsManager && !isSelfProfile)
        {
            return Forbid();
        }

        var nhanVien = await _dbContext.NhanViens
            .AsNoTracking()
            .Where(x => x.MaNhanVien == id)
            .Select(x => new NhanVienDetailDto
            {
                MaNhanVien = x.MaNhanVien,
                HoTen = x.HoTen,
                NgaySinh = x.NgaySinh,
                Cccd = x.Cccd,
                DiaChi = x.DiaChi,
                GioiTinh = x.GioiTinh,
                Email = x.Email,
                Sdt = x.Sdt,
                NgayVaoLam = x.NgayVaoLam,
                TrangThai = x.TrangThai ?? 0,
                MaPhongBan = x.MaPhongBan,
                PhoMaPhongBan = x.PhoMaPhongBan,
                TenPhongBan = x.PhongBanQuanLy != null ? x.PhongBanQuanLy.TenPhongBan : null,
                MaChucVu = x.MaChucVu,
                TenChucVu = x.ChucVu != null ? x.ChucVu.TenChucVu : null,
                AspNetUserId = x.AspNetUserId,
                Skills = x.KyNangNhanViens.Select(k => new NhanVienSkillDto
                {
                    MaKyNang = k.MaKyNang,
                    TenKyNang = k.KyNang.TenKyNang,
                    CapDo = k.CapDo,
                    SoDuAnDaDung = k.SoDuAnDaDung
                }).ToList(),
                KpiTrungBinh = x.KetQuaKpis.Any() ? (double?)x.KetQuaKpis.Average(k => k.DiemSo ?? 0) : null,
                SoCongViecDaPhanCong = x.PhanCongNhanViens.Count,
                LichSuHoatDong = x.NhatKyHoatDongs.OrderByDescending(l => l.ThoiGian).Take(20).Select(l => new NhatKyDto
                {
                    MaNhatKy = l.MaNhatKyHoatDong,
                    HanhDong = l.HanhDong,
                    ThoiGian = l.ThoiGian
                }).ToList()
            })
            .FirstOrDefaultAsync();

        if (nhanVien == null)
        {
            return NotFound(ApiResponse<NhanVienDetailDto>.Fail("Không tìm thấy nhân viên."));
        }

        if (!isSelfProfile && !IsManagerScopeAllowed(actor, nhanVien.MaPhongBan))
        {
            return Forbid();
        }

        return Ok(ApiResponse<NhanVienDetailDto>.Ok(nhanVien));
    }

    [HttpGet("me/profile")]
    public async Task<ActionResult<ApiResponse<NhanVienDetailDto>>> GetMyProfile()
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<NhanVienDetailDto>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        var detail = await BuildNhanVienDetail(actor.MaNhanVien);
        if (detail == null)
        {
            return NotFound(ApiResponse<NhanVienDetailDto>.Fail("Không tìm thấy hồ sơ nhân viên."));
        }

        return Ok(ApiResponse<NhanVienDetailDto>.Ok(detail));
    }

    [HttpPut("me/profile")]
    public async Task<ActionResult<ApiResponse<NhanVienDetailDto>>> UpdateMyProfile([FromBody] UpdateMyProfileRequest request)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<NhanVienDetailDto>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        var employee = await _dbContext.NhanViens.FirstOrDefaultAsync(x => x.MaNhanVien == actor.MaNhanVien);
        if (employee == null)
        {
            return NotFound(ApiResponse<NhanVienDetailDto>.Fail("Không tìm thấy hồ sơ nhân viên."));
        }

        var trimmedPhone = request.Sdt?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedPhone) && !PhoneRegex.IsMatch(trimmedPhone))
        {
            return BadRequest(ApiResponse<NhanVienDetailDto>.Fail("Số điện thoại phải đúng 10 chữ số."));
        }

        if (!string.IsNullOrWhiteSpace(trimmedPhone))
        {
            var phoneExists = await _dbContext.NhanViens.AnyAsync(x => x.MaNhanVien != actor.MaNhanVien && x.Sdt == trimmedPhone);
            if (phoneExists)
            {
                return Conflict(ApiResponse<NhanVienDetailDto>.Fail("Số điện thoại đã tồn tại."));
            }
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync();
        var oldSnapshot = new { employee.MaNhanVien, employee.Sdt, employee.DiaChi };

        employee.Sdt = trimmedPhone;
        employee.DiaChi = request.DiaChi?.Trim();

        await _dbContext.SaveChangesAsync();
        await _auditLogService.LogStructuredByUserIdAsync(
            actor.UserId,
            "UPDATE_PROFILE_DIRECT",
            "NHANVIEN",
            duLieuCu: oldSnapshot,
            duLieuMoi: new { employee.MaNhanVien, employee.Sdt, employee.DiaChi });
        await tx.CommitAsync();

        var updated = await BuildNhanVienDetail(actor.MaNhanVien);
        return Ok(ApiResponse<NhanVienDetailDto>.Ok(updated, "Cập nhật hồ sơ thành công."));
    }

    [HttpPost("me/profile-change-requests")]
    public async Task<ActionResult<ApiResponse<object>>> CreateMyProfileChangeRequest([FromBody] CreateProfileChangeRequest request)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        var employee = await _dbContext.NhanViens.AsNoTracking().FirstOrDefaultAsync(x => x.MaNhanVien == actor.MaNhanVien);
        if (employee == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy hồ sơ nhân viên."));
        }

        var newValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(request.Email)) newValues["EMAIL"] = request.Email.Trim();
        if (!string.IsNullOrWhiteSpace(request.HoTen)) newValues["HOTEN"] = request.HoTen.Trim();
        if (!string.IsNullOrWhiteSpace(request.Cccd)) newValues["CCCD"] = request.Cccd.Trim();
        if (request.NgaySinh.HasValue) newValues["NGAYSINH"] = request.NgaySinh.Value.ToString("yyyy-MM-dd");

        if (newValues.Count == 0)
        {
            return BadRequest(ApiResponse<object>.Fail("Không có dữ liệu thay đổi hợp lệ."));
        }

        var invalidFields = newValues.Keys.Where(x => !SensitiveProfileFields.Contains(x, StringComparer.OrdinalIgnoreCase)).ToList();
        if (invalidFields.Count > 0)
        {
            return BadRequest(ApiResponse<object>.Fail("Yêu cầu có trường không hợp lệ."));
        }

        if (newValues.TryGetValue("EMAIL", out var emailValue))
        {
            if (!IsValidEmail(emailValue ?? string.Empty))
            {
                return BadRequest(ApiResponse<object>.Fail("Email không hợp lệ."));
            }

            var emailExists = await _dbContext.NhanViens.AnyAsync(x => x.Email == emailValue && x.MaNhanVien != actor.MaNhanVien);
            if (emailExists)
            {
                return BadRequest(ApiResponse<object>.Fail("Email đã tồn tại."));
            }
        }

        if (newValues.TryGetValue("CCCD", out var cccdValue))
        {
            if (string.IsNullOrWhiteSpace(cccdValue) || !CccdRegex.IsMatch(cccdValue))
            {
                return BadRequest(ApiResponse<object>.Fail("CCCD phải đúng 12 chữ số."));
            }

            var cccdExists = await _dbContext.NhanViens.AnyAsync(x => x.Cccd == cccdValue && x.MaNhanVien != actor.MaNhanVien);
            if (cccdExists)
            {
                return BadRequest(ApiResponse<object>.Fail("CCCD đã tồn tại."));
            }
        }

        if (newValues.TryGetValue("NGAYSINH", out var nsValue) && DateTime.TryParse(nsValue, out var parsedDate) && parsedDate >= DateTime.Today)
        {
            return BadRequest(ApiResponse<object>.Fail("Ngày sinh phải nhỏ hơn ngày hiện tại."));
        }

        var pendingRequests = await _dbContext.YeuCauCapNhatHoSos
            .AsNoTracking()
            .Where(x => x.MaNhanVien == actor.MaNhanVien && x.TrangThai == "ChoDuyet" && !x.IsDeleted)
            .Select(x => x.DanhSachTruong)
            .ToListAsync();

        foreach (var field in newValues.Keys)
        {
            var existsConflict = pendingRequests.Any(ds => !string.IsNullOrWhiteSpace(ds) && ds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(field, StringComparer.OrdinalIgnoreCase));
            if (existsConflict)
            {
                return Conflict(ApiResponse<object>.Fail($"Đã có yêu cầu chờ duyệt cho trường {field}."));
            }
        }

        var oldValues = new Dictionary<string, string?>();
        foreach (var field in newValues.Keys)
        {
            oldValues[field] = field switch
            {
                "EMAIL" => employee.Email,
                "HOTEN" => employee.HoTen,
                "CCCD" => employee.Cccd,
                "NGAYSINH" => employee.NgaySinh?.ToString("yyyy-MM-dd"),
                _ => null
            };
        }

        var now = DateTime.Now;
        var requestEntity = new YeuCauCapNhatHoSo
        {
            MaNhanVien = actor.MaNhanVien,
            TrangThai = "ChoDuyet",
            DanhSachTruong = string.Join(",", newValues.Keys.OrderBy(x => x)),
            DuLieuCuJson = JsonSerializer.Serialize(oldValues),
            DuLieuMoiJson = JsonSerializer.Serialize(newValues),
            LyDoGui = request.LyDoGui?.Trim(),
            NguoiTao = actor.MaNhanVien,
            NgayTao = now,
            NgayCapNhat = now,
            IpTao = ResolveRequestIp(),
            IsDeleted = false
        };

        _dbContext.YeuCauCapNhatHoSos.Add(requestEntity);
        await _dbContext.SaveChangesAsync();

        await _auditLogService.LogStructuredByUserIdAsync(
            actor.UserId,
            "CREATE_PROFILE_CHANGE_REQUEST",
            "YEUCAU_CAPNHAT_HOSO",
            duLieuMoi: new
            {
                requestEntity.MaYeuCau,
                requestEntity.MaNhanVien,
                requestEntity.DanhSachTruong,
                requestEntity.TrangThai
            });

        return Ok(ApiResponse<object>.Ok(new { requestEntity.MaYeuCau }, "Đã gửi yêu cầu cập nhật hồ sơ."));
    }

    [HttpGet("me/profile-change-requests")]
    public async Task<ActionResult<ApiResponse<List<ProfileChangeRequestDto>>>> GetMyProfileChangeRequests()
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<List<ProfileChangeRequestDto>>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        var items = await _dbContext.YeuCauCapNhatHoSos
            .AsNoTracking()
            .Where(x => x.MaNhanVien == actor.MaNhanVien && !x.IsDeleted)
            .OrderByDescending(x => x.NgayTao)
            .Select(x => new ProfileChangeRequestDto
            {
                MaYeuCau = x.MaYeuCau,
                MaNhanVien = x.MaNhanVien,
                TrangThai = x.TrangThai,
                DanhSachTruong = x.DanhSachTruong,
                DuLieuCuJson = x.DuLieuCuJson,
                DuLieuMoiJson = x.DuLieuMoiJson,
                LyDoGui = x.LyDoGui,
                LyDoTuChoi = x.LyDoTuChoi,
                GhiChuDuyet = x.GhiChuDuyet,
                NguoiDuyet = x.NguoiDuyet,
                NgayTao = x.NgayTao,
                NgayDuyet = x.NgayDuyet
            })
            .ToListAsync();

        return Ok(ApiResponse<List<ProfileChangeRequestDto>>.Ok(items));
    }

    [HttpGet("profile-change-requests")]
    [Authorize(Policy = Permissions.EmployeesEdit)]
    public async Task<ActionResult<ApiResponse<List<ProfileChangeRequestDto>>>> GetProfileChangeRequests([FromQuery] string? trangThai)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<List<ProfileChangeRequestDto>>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        var query = _dbContext.YeuCauCapNhatHoSos
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (!actor.IsAdmin && !actor.IsManager)
        {
            return Forbid();
        }

        if (!string.IsNullOrWhiteSpace(trangThai))
        {
            query = query.Where(x => x.TrangThai == trangThai.Trim());
        }

        var items = await query
            .OrderByDescending(x => x.NgayTao)
            .Select(x => new ProfileChangeRequestDto
            {
                MaYeuCau = x.MaYeuCau,
                MaNhanVien = x.MaNhanVien,
                HoTenNhanVien = x.NhanVien != null ? x.NhanVien.HoTen : null,
                TrangThai = x.TrangThai,
                DanhSachTruong = x.DanhSachTruong,
                DuLieuCuJson = x.DuLieuCuJson,
                DuLieuMoiJson = x.DuLieuMoiJson,
                LyDoGui = x.LyDoGui,
                LyDoTuChoi = x.LyDoTuChoi,
                GhiChuDuyet = x.GhiChuDuyet,
                NguoiDuyet = x.NguoiDuyet,
                NgayTao = x.NgayTao,
                NgayDuyet = x.NgayDuyet
            })
            .ToListAsync();

        return Ok(ApiResponse<List<ProfileChangeRequestDto>>.Ok(items));
    }

    [HttpPost("profile-change-requests/{id:int}/approve")]
    [Authorize(Policy = Permissions.EmployeesEdit)]
    public async Task<ActionResult<ApiResponse<object>>> ApproveProfileChangeRequest(int id, [FromBody] ApproveProfileChangeRequestRequest request)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        if (!actor.IsAdmin && !actor.IsManager)
        {
            return Forbid();
        }

        var item = await _dbContext.YeuCauCapNhatHoSos.FirstOrDefaultAsync(x => x.MaYeuCau == id && !x.IsDeleted);
        if (item == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy yêu cầu."));
        }

        if (item.TrangThai != "ChoDuyet")
        {
            return BadRequest(ApiResponse<object>.Fail("Yêu cầu không ở trạng thái chờ duyệt."));
        }

        var employee = await _dbContext.NhanViens.FirstOrDefaultAsync(x => x.MaNhanVien == item.MaNhanVien);
        if (employee == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy hồ sơ nhân viên."));
        }

        var newData = JsonSerializer.Deserialize<Dictionary<string, string?>>(item.DuLieuMoiJson ?? "{}") ?? new Dictionary<string, string?>();
        var oldSnapshot = BuildNhanVienAuditSnapshot(employee);

        await using var tx = await _dbContext.Database.BeginTransactionAsync();

        if (newData.TryGetValue("HOTEN", out var hoTen) && !string.IsNullOrWhiteSpace(hoTen))
        {
            employee.HoTen = hoTen.Trim();
        }

        if (newData.TryGetValue("NGAYSINH", out var ngaySinhRaw) && DateTime.TryParse(ngaySinhRaw, out var ngaySinh))
        {
            employee.NgaySinh = ngaySinh;
        }

        if (newData.TryGetValue("CCCD", out var cccd) && !string.IsNullOrWhiteSpace(cccd))
        {
            var cccdTrim = cccd.Trim();
            if (!CccdRegex.IsMatch(cccdTrim))
            {
                return BadRequest(ApiResponse<object>.Fail("CCCD phải đúng 12 chữ số."));
            }

            var cccdExists = await _dbContext.NhanViens.AnyAsync(x => x.Cccd == cccdTrim && x.MaNhanVien != employee.MaNhanVien);
            if (cccdExists)
            {
                return Conflict(ApiResponse<object>.Fail("CCCD đã tồn tại."));
            }

            employee.Cccd = cccdTrim;
        }

        var changedEmail = false;
        if (newData.TryGetValue("EMAIL", out var email) && !string.IsNullOrWhiteSpace(email))
        {
            var emailTrim = email.Trim();
            if (!IsValidEmail(emailTrim))
            {
                return BadRequest(ApiResponse<object>.Fail("Email không hợp lệ."));
            }

            var emailExists = await _dbContext.NhanViens.AnyAsync(x => x.Email == emailTrim && x.MaNhanVien != employee.MaNhanVien);
            if (emailExists)
            {
                return Conflict(ApiResponse<object>.Fail("Email đã tồn tại."));
            }

            employee.Email = emailTrim;
            changedEmail = true;
        }

        await _dbContext.SaveChangesAsync();

        if (changedEmail && !string.IsNullOrWhiteSpace(employee.AspNetUserId))
        {
            await SyncIdentityContactInfo(employee.AspNetUserId, employee.Email, employee.Sdt);
        }

        item.TrangThai = "DaDuyet";
        item.GhiChuDuyet = request.GhiChuDuyet?.Trim();
        item.NguoiDuyet = actor.MaNhanVien;
        item.NguoiCapNhat = actor.MaNhanVien;
        item.NgayDuyet = DateTime.Now;
        item.NgayCapNhat = DateTime.Now;
        item.IpDuyet = ResolveRequestIp();

        await _dbContext.SaveChangesAsync();
        await _auditLogService.LogStructuredByUserIdAsync(
            actor.UserId,
            "APPROVE_PROFILE_CHANGE_REQUEST",
            "YEUCAU_CAPNHAT_HOSO",
            duLieuCu: oldSnapshot,
            duLieuMoi: BuildNhanVienAuditSnapshot(employee));

        await tx.CommitAsync();

        return Ok(ApiResponse<object>.Ok(null, "Duyệt yêu cầu cập nhật hồ sơ thành công."));
    }

    [HttpPost("profile-change-requests/{id:int}/reject")]
    [Authorize(Policy = Permissions.EmployeesEdit)]
    public async Task<ActionResult<ApiResponse<object>>> RejectProfileChangeRequest(int id, [FromBody] RejectProfileChangeRequestRequest request)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        if (!actor.IsAdmin && !actor.IsManager)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.LyDoTuChoi))
        {
            return BadRequest(ApiResponse<object>.Fail("Lý do từ chối là bắt buộc."));
        }

        var item = await _dbContext.YeuCauCapNhatHoSos.FirstOrDefaultAsync(x => x.MaYeuCau == id && !x.IsDeleted);
        if (item == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy yêu cầu."));
        }

        if (item.TrangThai != "ChoDuyet")
        {
            return BadRequest(ApiResponse<object>.Fail("Yêu cầu không ở trạng thái chờ duyệt."));
        }

        item.TrangThai = "TuChoi";
        item.LyDoTuChoi = request.LyDoTuChoi.Trim();
        item.GhiChuDuyet = request.GhiChuDuyet?.Trim();
        item.NguoiDuyet = actor.MaNhanVien;
        item.NguoiCapNhat = actor.MaNhanVien;
        item.NgayDuyet = DateTime.Now;
        item.NgayCapNhat = DateTime.Now;
        item.IpDuyet = ResolveRequestIp();

        await _dbContext.SaveChangesAsync();
        await _auditLogService.LogStructuredByUserIdAsync(
            actor.UserId,
            "REJECT_PROFILE_CHANGE_REQUEST",
            "YEUCAU_CAPNHAT_HOSO",
            duLieuMoi: new { item.MaYeuCau, item.TrangThai, item.LyDoTuChoi });

        return Ok(ApiResponse<object>.Ok(null, "Đã từ chối yêu cầu cập nhật hồ sơ."));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.EmployeesCreate)]
    public async Task<ActionResult<ApiResponse<NhanVienDetailDto>>> CreateNhanVien([FromBody] CreateNhanVienRequest request)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<NhanVienDetailDto>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        if (!IsManagerScopeAllowed(actor, request.MaPhongBan))
        {
            return Forbid();
        }

        var validationError = await ValidateCreateNhanVienRequest(request);
        if (validationError != null)
        {
            return BadRequest(ApiResponse<NhanVienDetailDto>.Fail(validationError));
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync();

        var nhanVien = new NhanVien
        {
            MaPhongBan = request.MaPhongBan,
            PhoMaPhongBan = NormalizePhoMaPhongBan(request.PhoMaPhongBan),
            HoTen = request.HoTen.Trim(),
            NgaySinh = request.NgaySinh,
            Cccd = request.Cccd.Trim(),
            DiaChi = request.DiaChi?.Trim(),
            GioiTinh = request.GioiTinh?.Trim(),
            Email = request.Email.Trim(),
            Sdt = request.Sdt?.Trim(),
            NgayVaoLam = request.NgayVaoLam,
            TrangThai = 1,
            MaChucVu = request.MaChucVu,
            AspNetUserId = request.AspNetUserId
        };

        _dbContext.NhanViens.Add(nhanVien);
        await _dbContext.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(nhanVien.AspNetUserId))
        {
            await SyncIdentityLockStatus(nhanVien.AspNetUserId, nhanVien.TrangThai == 1);
        }

        await _auditLogService.LogStructuredByUserIdAsync(
            actor.UserId,
            "CREATE",
            "NHANVIEN",
            duLieuMoi: BuildNhanVienAuditSnapshot(nhanVien));
        await tx.CommitAsync();

        var created = await BuildNhanVienDetail(nhanVien.MaNhanVien);
        return CreatedAtAction(nameof(GetNhanVienById), new { id = nhanVien.MaNhanVien },
            ApiResponse<NhanVienDetailDto>.Ok(created!, "Tạo nhân viên thành công"));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.EmployeesEdit)]
    public async Task<ActionResult<ApiResponse<NhanVienDetailDto>>> UpdateNhanVien(int id, [FromBody] UpdateNhanVienRequest request)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<NhanVienDetailDto>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        var nhanVien = await _dbContext.NhanViens.FirstOrDefaultAsync(x => x.MaNhanVien == id);
        if (nhanVien == null)
        {
            return NotFound(ApiResponse<NhanVienDetailDto>.Fail("Không tìm thấy nhân viên."));
        }

        if (!IsManagerScopeAllowed(actor, nhanVien.MaPhongBan) || !IsManagerScopeAllowed(actor, request.MaPhongBan))
        {
            return Forbid();
        }

        var validationError = await ValidateUpdateNhanVienRequest(request, id, nhanVien.Cccd);
        if (validationError != null)
        {
            return BadRequest(ApiResponse<NhanVienDetailDto>.Fail(validationError));
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync();

        var oldSnapshot = BuildNhanVienAuditSnapshot(nhanVien);

        nhanVien.HoTen = request.HoTen.Trim();
        nhanVien.NgaySinh = request.NgaySinh;
        nhanVien.DiaChi = request.DiaChi?.Trim();
        nhanVien.GioiTinh = request.GioiTinh?.Trim();
        nhanVien.Email = request.Email.Trim();
        nhanVien.Sdt = request.Sdt?.Trim();
        nhanVien.NgayVaoLam = request.NgayVaoLam;
        nhanVien.MaPhongBan = request.MaPhongBan;
        nhanVien.PhoMaPhongBan = NormalizePhoMaPhongBan(request.PhoMaPhongBan);
        nhanVien.MaChucVu = request.MaChucVu;

        if (!string.Equals(nhanVien.AspNetUserId, request.AspNetUserId, StringComparison.Ordinal))
        {
            nhanVien.AspNetUserId = request.AspNetUserId;
        }

        await _dbContext.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(nhanVien.AspNetUserId))
        {
            await SyncIdentityContactInfo(nhanVien.AspNetUserId, nhanVien.Email, nhanVien.Sdt);
        }

        if (!string.IsNullOrWhiteSpace(nhanVien.AspNetUserId))
        {
            await SyncIdentityLockStatus(nhanVien.AspNetUserId, nhanVien.TrangThai == 1);
        }

        await _auditLogService.LogStructuredByUserIdAsync(
            actor.UserId,
            "UPDATE",
            "NHANVIEN",
            duLieuCu: oldSnapshot,
            duLieuMoi: BuildNhanVienAuditSnapshot(nhanVien));
        await tx.CommitAsync();

        var updated = await BuildNhanVienDetail(id);
        return Ok(ApiResponse<NhanVienDetailDto>.Ok(updated, "Cập nhật nhân viên thành công"));
    }

    [HttpPut("{id:int}/status")]
    [Authorize(Policy = Permissions.EmployeesEdit)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        if (request.TrangThai is not (0 or 1))
        {
            return BadRequest(ApiResponse<object>.Fail("Trạng thái chỉ nhận giá trị 0 hoặc 1."));
        }

        var nhanVien = await _dbContext.NhanViens.FirstOrDefaultAsync(x => x.MaNhanVien == id);
        if (nhanVien == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy nhân viên."));
        }

        if (!IsManagerScopeAllowed(actor, nhanVien.MaPhongBan))
        {
            return Forbid();
        }

        if (request.TrangThai == 0)
        {
            var blockReason = await GetDeactivateBlockReasonAsync(id);
            if (!string.IsNullOrWhiteSpace(blockReason))
            {
                return Conflict(ApiResponse<object>.Fail(blockReason));
            }
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync();

        nhanVien.TrangThai = request.TrangThai;
        await _dbContext.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(nhanVien.AspNetUserId))
        {
            await SyncIdentityLockStatus(nhanVien.AspNetUserId, request.TrangThai == 1);
        }

        await _auditLogService.LogStructuredByUserIdAsync(
            actor.UserId,
            "UPDATE_STATUS",
            "NHANVIEN",
            duLieuMoi: new { MaNhanVien = nhanVien.MaNhanVien, TrangThai = request.TrangThai });
        await tx.CommitAsync();

        return Ok(ApiResponse<object>.Ok(null, "Cập nhật trạng thái thành công"));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.EmployeesDelete)]
    public async Task<ActionResult<ApiResponse<object>>> SoftDeleteNhanVien(int id)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        var nhanVien = await _dbContext.NhanViens.FirstOrDefaultAsync(x => x.MaNhanVien == id);
        if (nhanVien == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy nhân viên."));
        }

        var blockReason = await GetDeactivateBlockReasonAsync(id);
        if (!string.IsNullOrWhiteSpace(blockReason))
        {
            return Conflict(ApiResponse<object>.Fail(blockReason));
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync();

        nhanVien.TrangThai = 0;
        await _dbContext.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(nhanVien.AspNetUserId))
        {
            await SyncIdentityLockStatus(nhanVien.AspNetUserId, false);
        }

        await _auditLogService.LogStructuredByUserIdAsync(
            actor.UserId,
            "SOFT_DELETE",
            "NHANVIEN",
            duLieuMoi: new { MaNhanVien = nhanVien.MaNhanVien, TrangThai = nhanVien.TrangThai });
        await tx.CommitAsync();

        return Ok(ApiResponse<object>.Ok(null, "Đã chuyển nhân viên sang trạng thái nghỉ việc."));
    }

    [HttpPost("{id:int}/skills")]
    [Authorize(Policy = Permissions.EmployeesSkills)]
    public async Task<ActionResult<ApiResponse<object>>> AddSkill(int id, [FromBody] AddSkillRequest request)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        var employee = await _dbContext.NhanViens.FirstOrDefaultAsync(x => x.MaNhanVien == id);
        if (employee == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy nhân viên."));
        }

        if (!IsManagerScopeAllowed(actor, employee.MaPhongBan))
        {
            return Forbid();
        }

        if (employee.TrangThai != 1)
        {
            return BadRequest(ApiResponse<object>.Fail("Nhân viên đã nghỉ việc, không thể cập nhật kỹ năng."));
        }

        var skillExists = await SkillExistsAsync(request.MaKyNang);
        if (!skillExists)
        {
            return BadRequest(ApiResponse<object>.Fail("Kỹ năng không tồn tại."));
        }

        if (request.CapDo is < 1 or > 5)
        {
            return BadRequest(ApiResponse<object>.Fail("Cấp độ kỹ năng phải trong khoảng từ 1 đến 5."));
        }

        var existed = await _dbContext.KyNangNhanViens.AnyAsync(x => x.MaNhanVien == id && x.MaKyNang == request.MaKyNang);
        if (existed)
        {
            return Conflict(ApiResponse<object>.Fail("Kỹ năng đã tồn tại trên nhân viên."));
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync();

        _dbContext.KyNangNhanViens.Add(new KyNangNhanVien
        {
            MaNhanVien = id,
            MaKyNang = request.MaKyNang,
            CapDo = request.CapDo,
            SoDuAnDaDung = request.SoDuAnDaDung
        });

        await _dbContext.SaveChangesAsync();
        await WriteAuditAsync(actor, $"Gán kỹ năng {request.MaKyNang} cho nhân viên {employee.HoTen} (Mã: {id})");
        await tx.CommitAsync();

        return Ok(ApiResponse<object>.Ok(null, "Thêm kỹ năng thành công"));
    }

    [HttpPut("{id:int}/skills/{skillId:int}")]
    [Authorize(Policy = Permissions.EmployeesSkills)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateSkill(int id, int skillId, [FromBody] UpdateSkillRequest request)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        var employee = await _dbContext.NhanViens.AsNoTracking().FirstOrDefaultAsync(x => x.MaNhanVien == id);
        if (employee == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy nhân viên."));
        }

        if (!IsManagerScopeAllowed(actor, employee.MaPhongBan))
        {
            return Forbid();
        }

        var item = await _dbContext.KyNangNhanViens.FirstOrDefaultAsync(x => x.MaNhanVien == id && x.MaKyNang == skillId);
        if (item == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy kỹ năng của nhân viên."));
        }

        if (request.CapDo is < 1 or > 5)
        {
            return BadRequest(ApiResponse<object>.Fail("Cấp độ kỹ năng phải trong khoảng từ 1 đến 5."));
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync();

        item.CapDo = request.CapDo;
        item.SoDuAnDaDung = request.SoDuAnDaDung;

        await _dbContext.SaveChangesAsync();
        await WriteAuditAsync(actor, $"Cập nhật kỹ năng {skillId} cho nhân viên {id}");
        await tx.CommitAsync();

        return Ok(ApiResponse<object>.Ok(null, "Cập nhật kỹ năng thành công"));
    }

    [HttpDelete("{id:int}/skills/{skillId:int}")]
    [Authorize(Policy = Permissions.EmployeesSkills)]
    public async Task<ActionResult<ApiResponse<object>>> RemoveSkill(int id, int skillId)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        var employee = await _dbContext.NhanViens.AsNoTracking().FirstOrDefaultAsync(x => x.MaNhanVien == id);
        if (employee == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy nhân viên."));
        }

        if (!IsManagerScopeAllowed(actor, employee.MaPhongBan))
        {
            return Forbid();
        }

        var item = await _dbContext.KyNangNhanViens.FirstOrDefaultAsync(x => x.MaNhanVien == id && x.MaKyNang == skillId);
        if (item == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy kỹ năng của nhân viên."));
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync();

        _dbContext.KyNangNhanViens.Remove(item);
        await _dbContext.SaveChangesAsync();
        await WriteAuditAsync(actor, $"Xóa kỹ năng {skillId} của nhân viên {id}");
        await tx.CommitAsync();

        return Ok(ApiResponse<object>.Ok(null, "Xóa kỹ năng thành công"));
    }

    private async Task<bool> SkillExistsAsync(int maKyNang)
    {
        try
        {
            return await _dbContext.KyNangs.AnyAsync(x => x.MaKyNang == maKyNang && (x.TrangThai ?? 1) == 1);
        }
        catch (SqlException ex) when (ex.Message.Contains("Invalid column name 'TRANGTHAI'", StringComparison.OrdinalIgnoreCase))
        {
            // Compatibility fallback for old schemas where KYNANG does not have TRANGTHAI.
            return await _dbContext.KyNangs.AnyAsync(x => x.MaKyNang == maKyNang);
        }
    }

    private async Task<string?> ValidateCreateNhanVienRequest(CreateNhanVienRequest request)
    {
        return await ValidateNhanVienRequestCore(request, null);
    }

    private async Task<string?> ValidateUpdateNhanVienRequest(UpdateNhanVienRequest request, int existingId, string? oldCccd)
    {
        var requestedCccd = request.Cccd?.Trim();
        if (!string.Equals(oldCccd, requestedCccd, StringComparison.Ordinal))
        {
            return "Không cho phép thay đổi CCCD sau khi tạo.";
        }

        return await ValidateNhanVienRequestCore(request, existingId);
    }

    private async Task<string?> ValidateNhanVienRequestCore(BaseNhanVienRequest request, int? existingId)
    {
        var trimmedEmail = request.Email?.Trim() ?? string.Empty;
        var trimmedCccd = request.Cccd?.Trim() ?? string.Empty;
        var trimmedSdt = request.Sdt?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(request.HoTen))
        {
            return "Họ tên là bắt buộc.";
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return "Email là bắt buộc.";
        }

        if (request.NgaySinh >= DateTime.Today)
        {
            return "Ngày sinh phải nhỏ hơn ngày hiện tại.";
        }

        if (request.NgayVaoLam < request.NgaySinh)
        {
            return "Ngày vào làm phải lớn hơn hoặc bằng ngày sinh.";
        }

        if (string.IsNullOrWhiteSpace(trimmedCccd) || !CccdRegex.IsMatch(trimmedCccd))
        {
            return "CCCD phải đúng 12 chữ số.";
        }

        // Phone number validation: only required if provided, must be exactly 10 digits (0xxxxxxxxx)
        if (!string.IsNullOrWhiteSpace(trimmedSdt) && !PhoneRegex.IsMatch(trimmedSdt))
        {
            return "Số điện thoại phải đúng 10 chữ số.";
        }

        if (!IsValidEmail(trimmedEmail))
        {
            return "Email không hợp lệ.";
        }

        var emailExists = await _dbContext.NhanViens.AnyAsync(x => x.Email == trimmedEmail && x.MaNhanVien != existingId);
        if (emailExists)
        {
            return "Email đã tồn tại.";
        }

        var cccdExists = await _dbContext.NhanViens.AnyAsync(x => x.Cccd == trimmedCccd && x.MaNhanVien != existingId);
        if (cccdExists)
        {
            return "CCCD đã tồn tại.";
        }

        if (!string.IsNullOrWhiteSpace(trimmedSdt))
        {
            var sdtExists = await _dbContext.NhanViens.AnyAsync(x => x.Sdt == trimmedSdt && x.MaNhanVien != existingId);
            if (sdtExists)
            {
                return "Số điện thoại đã tồn tại.";
            }
        }

        var departmentExists = await _dbContext.PhongBans.AnyAsync(x => x.MaPhongBan == request.MaPhongBan);
        if (!departmentExists)
        {
            return "Phòng ban không tồn tại.";
        }

        if (request.PhoMaPhongBan.HasValue && request.PhoMaPhongBan.Value > 0)
        {
            var phoDepartmentExists = await _dbContext.PhongBans.AnyAsync(x => x.MaPhongBan == request.PhoMaPhongBan.Value);
            if (!phoDepartmentExists)
            {
                return "PHO_MAPHONGBAN không tồn tại.";
            }
        }

        if (request.MaChucVu.HasValue)
        {
            var chucVuExists = await _dbContext.ChucVus.AnyAsync(x => x.MaChucVu == request.MaChucVu.Value);
            if (!chucVuExists)
            {
                return "Chức vụ không tồn tại.";
            }
        }
        else
        {
            return "Chức vụ là bắt buộc.";
        }

        if (!string.IsNullOrWhiteSpace(request.AspNetUserId))
        {
            var user = await _userManager.FindByIdAsync(request.AspNetUserId);
            if (user == null)
            {
                return "AspNetUserId không tồn tại.";
            }

            var userUsedByOtherEmployee = await _dbContext.NhanViens.AnyAsync(x => x.AspNetUserId == request.AspNetUserId && x.MaNhanVien != existingId);
            if (userUsedByOtherEmployee)
            {
                return "AspNetUserId đã được gán cho nhân viên khác.";
            }
        }

        return null;
    }

    private async Task<bool> HasActiveTaskAssignmentsAsync(int maNhanVien)
    {
        return await _dbContext.PhanCongNhanViens
            .AsNoTracking()
            .AnyAsync(x => x.MaNhanVien == maNhanVien && (x.TrangThai == null || x.TrangThai != 0));
    }

    private async Task<string?> GetDeactivateBlockReasonAsync(int maNhanVien)
    {
        var hasActiveAssignments = await HasActiveTaskAssignmentsAsync(maNhanVien);
        if (hasActiveAssignments)
        {
            return "Nhân viên đang có phân công công việc hoạt động, không thể chuyển trạng thái.";
        }

        var hasActiveProjects = await _dbContext.DuAnNhanViens
            .AsNoTracking()
            .AnyAsync(x => x.MaNhanVien == maNhanVien && (x.TrangThai == null || x.TrangThai != 0));
        if (hasActiveProjects)
        {
            return "Nhân viên đang tham gia dự án hoạt động, không thể chuyển trạng thái.";
        }

        var hasActiveKpiAssignments = await _dbContext.KpiNhanViens
            .AsNoTracking()
            .AnyAsync(x => x.MaNhanVien == maNhanVien && (x.TrangThai == null || x.TrangThai != 0));
        if (hasActiveKpiAssignments)
        {
            return "Nhân viên đang có KPI hoạt động, không thể chuyển trạng thái.";
        }

        return null;
    }

    private static object BuildNhanVienAuditSnapshot(NhanVien nv)
    {
        return new
        {
            nv.MaNhanVien,
            nv.HoTen,
            nv.Email,
            nv.Sdt,
            nv.Cccd,
            nv.MaPhongBan,
            nv.PhoMaPhongBan,
            nv.MaChucVu,
            nv.TrangThai,
            nv.AspNetUserId,
            nv.NgayVaoLam
        };
    }

    private static int? NormalizePhoMaPhongBan(int? phoMaPhongBan)
    {
        return phoMaPhongBan.HasValue && phoMaPhongBan.Value > 0
            ? phoMaPhongBan.Value
            : null;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var _ = new MailAddress(email);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private async Task SyncIdentityLockStatus(string userId, bool isActive)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return;
        }

        user.LockoutEnabled = true;
        user.LockoutEnd = isActive ? null : DateTimeOffset.MaxValue;
        await _userManager.UpdateAsync(user);
    }

    private async Task SyncIdentityContactInfo(string userId, string? email, string? phone)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return;
        }

        user.Email = email;
        user.NormalizedEmail = string.IsNullOrWhiteSpace(email) ? null : _userManager.NormalizeEmail(email);
        user.PhoneNumber = phone;
        await _userManager.UpdateAsync(user);
    }

    private string? ResolveRequestIp()
    {
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor.Split(',').FirstOrDefault()?.Trim();
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private async Task<NhanVienDetailDto?> BuildNhanVienDetail(int id)
    {
        return await _dbContext.NhanViens
            .AsNoTracking()
            .Where(x => x.MaNhanVien == id)
            .Select(x => new NhanVienDetailDto
            {
                MaNhanVien = x.MaNhanVien,
                HoTen = x.HoTen,
                NgaySinh = x.NgaySinh,
                Cccd = x.Cccd,
                DiaChi = x.DiaChi,
                GioiTinh = x.GioiTinh,
                Email = x.Email,
                Sdt = x.Sdt,
                NgayVaoLam = x.NgayVaoLam,
                TrangThai = x.TrangThai ?? 0,
                MaPhongBan = x.MaPhongBan,
                PhoMaPhongBan = x.PhoMaPhongBan,
                TenPhongBan = x.PhongBanQuanLy != null ? x.PhongBanQuanLy.TenPhongBan : null,
                MaChucVu = x.MaChucVu,
                TenChucVu = x.ChucVu != null ? x.ChucVu.TenChucVu : null,
                AspNetUserId = x.AspNetUserId,
                Skills = x.KyNangNhanViens.Select(k => new NhanVienSkillDto
                {
                    MaKyNang = k.MaKyNang,
                    TenKyNang = k.KyNang.TenKyNang,
                    CapDo = k.CapDo,
                    SoDuAnDaDung = k.SoDuAnDaDung
                }).ToList(),
                KpiTrungBinh = x.KetQuaKpis.Any() ? (double?)x.KetQuaKpis.Average(k => k.DiemSo ?? 0) : null,
                SoCongViecDaPhanCong = x.PhanCongNhanViens.Count,
                LichSuHoatDong = x.NhatKyHoatDongs.OrderByDescending(l => l.ThoiGian).Take(20).Select(l => new NhatKyDto
                {
                    MaNhatKy = l.MaNhatKyHoatDong,
                    HanhDong = l.HanhDong,
                    ThoiGian = l.ThoiGian
                }).ToList()
            })
            .FirstOrDefaultAsync();
    }

    public class BaseNhanVienRequest
    {
        public string HoTen { get; set; } = string.Empty;
        public DateTime NgaySinh { get; set; }
        public string Cccd { get; set; } = string.Empty;
        public string? DiaChi { get; set; }
        public string? GioiTinh { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? Sdt { get; set; }
        public DateTime NgayVaoLam { get; set; }
        public int MaPhongBan { get; set; }
        public int? PhoMaPhongBan { get; set; }
        public int? MaChucVu { get; set; }
        public string? AspNetUserId { get; set; }
    }

    public class CreateNhanVienRequest : BaseNhanVienRequest;

    public class UpdateNhanVienRequest : BaseNhanVienRequest;

    public class UpdateStatusRequest
    {
        public int TrangThai { get; set; }
    }

    public class AddSkillRequest
    {
        public int MaKyNang { get; set; }
        public int CapDo { get; set; }
        public int? SoDuAnDaDung { get; set; }
    }

    public class UpdateSkillRequest
    {
        public int CapDo { get; set; }
        public int? SoDuAnDaDung { get; set; }
    }

    public class NhanVienListItemDto
    {
        public int MaNhanVien { get; set; }
        public string? HoTen { get; set; }
        public string? Email { get; set; }
        public string? Sdt { get; set; }
        public int TrangThai { get; set; }
        public int? MaPhongBan { get; set; }
        public string? TenPhongBan { get; set; }
        public int? MaChucVu { get; set; }
        public string? TenChucVu { get; set; }
        public DateTime? NgayVaoLam { get; set; }
    }

    public class NhanVienDetailDto
    {
        public int MaNhanVien { get; set; }
        public string? HoTen { get; set; }
        public DateTime? NgaySinh { get; set; }
        public string? Cccd { get; set; }
        public string? DiaChi { get; set; }
        public string? GioiTinh { get; set; }
        public string? Email { get; set; }
        public string? Sdt { get; set; }
        public DateTime? NgayVaoLam { get; set; }
        public int TrangThai { get; set; }
        public int? MaPhongBan { get; set; }
        public int? PhoMaPhongBan { get; set; }
        public string? TenPhongBan { get; set; }
        public int? MaChucVu { get; set; }
        public string? TenChucVu { get; set; }
        public string? AspNetUserId { get; set; }
        public List<NhanVienSkillDto> Skills { get; set; } = new();
        public double? KpiTrungBinh { get; set; }
        public int SoCongViecDaPhanCong { get; set; }
        public List<NhatKyDto> LichSuHoatDong { get; set; } = new();
    }

    public class NhanVienSkillDto
    {
        public int MaKyNang { get; set; }
        public string? TenKyNang { get; set; }
        public int? CapDo { get; set; }
        public int? SoDuAnDaDung { get; set; }
    }

    public class NhatKyDto
    {
        public int MaNhatKy { get; set; }
        public string? HanhDong { get; set; }
        public DateTime? ThoiGian { get; set; }
    }

    public class UpdateMyProfileRequest
    {
        public string? Sdt { get; set; }
        public string? DiaChi { get; set; }
    }

    public class CreateProfileChangeRequest
    {
        public string? Email { get; set; }
        public string? HoTen { get; set; }
        public DateTime? NgaySinh { get; set; }
        public string? Cccd { get; set; }
        public string? LyDoGui { get; set; }
    }

    public class ApproveProfileChangeRequestRequest
    {
        public string? GhiChuDuyet { get; set; }
    }

    public class RejectProfileChangeRequestRequest
    {
        public string LyDoTuChoi { get; set; } = string.Empty;
        public string? GhiChuDuyet { get; set; }
    }

    public class ProfileChangeRequestDto
    {
        public int MaYeuCau { get; set; }
        public int MaNhanVien { get; set; }
        public string? HoTenNhanVien { get; set; }
        public string? TrangThai { get; set; }
        public string? DanhSachTruong { get; set; }
        public string? DuLieuCuJson { get; set; }
        public string? DuLieuMoiJson { get; set; }
        public string? LyDoGui { get; set; }
        public string? LyDoTuChoi { get; set; }
        public string? GhiChuDuyet { get; set; }
        public int? NguoiDuyet { get; set; }
        public DateTime? NgayTao { get; set; }
        public DateTime? NgayDuyet { get; set; }
    }
}




