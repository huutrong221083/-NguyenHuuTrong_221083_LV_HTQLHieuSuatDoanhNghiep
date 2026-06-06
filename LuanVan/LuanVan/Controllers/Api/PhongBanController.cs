using LuanVan.Contracts;
using LuanVan.Data;
using LuanVan.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace LuanVan.Controllers.Api;

[ApiController]
[Route("phongban")]
[Authorize]
public class PhongBanController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public PhongBanController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Authorize(Policy = Permissions.EmployeesView)]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<PhongBanDto>>>> GetPhongBans()
    {
        try
        {
            var query = _dbContext.PhongBans
                .AsNoTracking()
                .AsQueryable();

            if (User.IsInRole(Roles.Manager) && !User.IsInRole(Roles.Admin))
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return StatusCode(StatusCodes.Status403Forbidden,
                        ApiResponse<List<PhongBanDto>>.Fail("Tài khoản quản lý chưa được liên kết nhân viên."));
                }

                var managerProfile = await _dbContext.NhanViens
                    .AsNoTracking()
                    .Where(x => x.AspNetUserId == userId)
                    .Select(x => new { x.MaNhanVien })
                    .FirstOrDefaultAsync();

                if (managerProfile == null)
                {
                    return StatusCode(StatusCodes.Status403Forbidden,
                        ApiResponse<List<PhongBanDto>>.Fail("Tài khoản quản lý chưa được liên kết nhân viên."));
                }

                var managedDepartmentIds = await _dbContext.PhongBans
                    .AsNoTracking()
                    .Where(x => x.MaTruongPhong == managerProfile.MaNhanVien)
                    .Select(x => x.MaPhongBan)
                    .ToListAsync();

                query = query.Where(x => managedDepartmentIds.Contains(x.MaPhongBan));
            }

            var items = await query
                .OrderBy(x => x.TenPhongBan)
                .Select(x => new PhongBanDto
                {
                    MaPhongBan = x.MaPhongBan,
                    TenPhongBan = x.TenPhongBan,
                    MoTa = x.MoTa,
                    MaTruongPhong = x.MaTruongPhong,
                    SoNhanVien = x.NhanVienQuanLys.Count(nv => nv.TrangThai == 1)
                })
                .ToListAsync();

            return Ok(ApiResponse<List<PhongBanDto>>.Ok(items));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<List<PhongBanDto>>.Fail($"Không tải được danh sách phòng ban: {ex.Message}"));
        }
    }

    [Authorize(Policy = Permissions.EmployeesCreate)]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<PhongBanDto>>> CreatePhongBan([FromBody] CreatePhongBanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TenPhongBan))
        {
            return BadRequest(ApiResponse<PhongBanDto>.Fail("Tên phòng ban là bắt buộc."));
        }

        var tenPhongBan = request.TenPhongBan.Trim();
        var isDuplicateName = await _dbContext.PhongBans.AnyAsync(x =>
            x.TenPhongBan != null
            && EF.Functions.Collate(x.TenPhongBan, "Latin1_General_CI_AI") == tenPhongBan);
        if (isDuplicateName)
        {
            return Conflict(ApiResponse<PhongBanDto>.Fail("Tên phòng ban đã tồn tại."));
        }

        var managerExists = !request.MaTruongPhong.HasValue
            || await _dbContext.NhanViens.AnyAsync(x => x.MaNhanVien == request.MaTruongPhong.Value && x.TrangThai == 1);
        if (!managerExists)
        {
            return BadRequest(ApiResponse<PhongBanDto>.Fail("Trưởng phòng không tồn tại hoặc không còn hoạt động."));
        }

        var phongBan = new PhongBan
        {
            TenPhongBan = tenPhongBan,
            MoTa = request.MoTa?.Trim(),
            MaTruongPhong = request.MaTruongPhong
        };

        _dbContext.PhongBans.Add(phongBan);
        await _dbContext.SaveChangesAsync();

        var dto = new PhongBanDto
        {
            MaPhongBan = phongBan.MaPhongBan,
            TenPhongBan = phongBan.TenPhongBan,
            MoTa = phongBan.MoTa,
            MaTruongPhong = phongBan.MaTruongPhong,
            SoNhanVien = 0
        };

        return CreatedAtAction(nameof(GetNhanVienByPhongBan), new { id = phongBan.MaPhongBan },
            ApiResponse<PhongBanDto>.Ok(dto, "Tạo phòng ban thành công"));
    }

    [Authorize(Policy = Permissions.EmployeesEdit)]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdatePhongBan(int id, [FromBody] UpdatePhongBanRequest request)
    {
        var phongBan = await _dbContext.PhongBans.FirstOrDefaultAsync(x => x.MaPhongBan == id);
        if (phongBan == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy phòng ban."));
        }

        if (string.IsNullOrWhiteSpace(request.TenPhongBan))
        {
            return BadRequest(ApiResponse<object>.Fail("Tên phòng ban là bắt buộc."));
        }

        var tenPhongBan = request.TenPhongBan.Trim();
        var isDuplicateName = await _dbContext.PhongBans.AnyAsync(x =>
            x.MaPhongBan != id
            && x.TenPhongBan != null
            && EF.Functions.Collate(x.TenPhongBan, "Latin1_General_CI_AI") == tenPhongBan);
        if (isDuplicateName)
        {
            return Conflict(ApiResponse<object>.Fail("Tên phòng ban đã tồn tại."));
        }

        var managerExists = !request.MaTruongPhong.HasValue
            || await _dbContext.NhanViens.AnyAsync(x => x.MaNhanVien == request.MaTruongPhong.Value && x.TrangThai == 1);
        if (!managerExists)
        {
            return BadRequest(ApiResponse<object>.Fail("Trưởng phòng không tồn tại hoặc không còn hoạt động."));
        }

        phongBan.TenPhongBan = tenPhongBan;
        phongBan.MoTa = request.MoTa?.Trim();
        phongBan.MaTruongPhong = request.MaTruongPhong;

        await _dbContext.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(null, "Cập nhật phòng ban thành công"));
    }

    [Authorize(Policy = Permissions.EmployeesDelete)]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeletePhongBan(int id)
    {
        var phongBan = await _dbContext.PhongBans.FirstOrDefaultAsync(x => x.MaPhongBan == id);
        if (phongBan == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy phòng ban."));
        }

        // Chỉ chặn nếu còn nhân viên đang làm việc thuộc phòng ban này.
        var hasActiveEmployee = await _dbContext.NhanViens
            .AnyAsync(x => (x.MaPhongBan == id || x.PhoMaPhongBan == id) && x.TrangThai == 1);
        if (hasActiveEmployee)
        {
            return BadRequest(ApiResponse<object>.Fail("Phòng ban đang có nhân viên hoạt động. Hãy chuyển phòng hoặc cho nghỉ việc trước khi xóa."));
        }

        var inactiveEmployees = await _dbContext.NhanViens
            .Where(x => x.MaPhongBan == id || x.PhoMaPhongBan == id)
            .ToListAsync();

        // Check dự án
        var hasProject = await _dbContext.DuAnPhongBans
            .AnyAsync(x => x.MaPhongBan == id);
        if (hasProject)
        {
            return BadRequest(ApiResponse<object>.Fail("Phòng ban đang được gán trong dự án."));
        }

        // Check phân công
        var hasAssignment = await _dbContext.PhanCongPhongBans
            .AnyAsync(x => x.MaPhongBan == id);
        if (hasAssignment)
        {
            return BadRequest(ApiResponse<object>.Fail("Phòng ban đang có phân công công việc."));
        }

        // Check KPI
        var hasKpi = await _dbContext.KpiPhongBans
            .AnyAsync(x => x.MaPhongBan == id);
        if (hasKpi)
        {
            return BadRequest(ApiResponse<object>.Fail("Phòng ban đang có KPI."));
        }

        try
        {
            foreach (var employee in inactiveEmployees)
            {
                if (employee.MaPhongBan == id)
                {
                    employee.MaPhongBan = null;
                }

                if (employee.PhoMaPhongBan == id)
                {
                    employee.PhoMaPhongBan = null;
                }
            }

            _dbContext.PhongBans.Remove(phongBan);
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Lỗi khi xóa phòng ban.",
                error = ex.Message,
                inner = ex.InnerException?.Message
            });
        }

        return Ok(ApiResponse<object>.Ok(null, "Xóa phòng ban thành công"));
    }

    [Authorize(Policy = Permissions.EmployeesView)]
    [HttpGet("{id:int}/nhanvien")]
    public async Task<ActionResult<ApiResponse<List<NhanVienInPhongBanDto>>>> GetNhanVienByPhongBan(int id)
    {
        var phongBanExists = await _dbContext.PhongBans.AnyAsync(x => x.MaPhongBan == id);
        if (!phongBanExists)
        {
            return NotFound(ApiResponse<List<NhanVienInPhongBanDto>>.Fail("Không tìm thấy phòng ban."));
        }

        var items = await _dbContext.NhanViens
            .AsNoTracking()
            .Where(x => x.MaPhongBan == id)
            .OrderBy(x => x.HoTen)
            .Select(x => new NhanVienInPhongBanDto
            {
                MaNhanVien = x.MaNhanVien,
                HoTen = x.HoTen,
                Email = x.Email,
                Sdt = x.Sdt,
                PhoMaPhongBan = x.PhoMaPhongBan,
                TrangThai = x.TrangThai ?? 0
            })
            .ToListAsync();

        return Ok(ApiResponse<List<NhanVienInPhongBanDto>>.Ok(items));
    }

    [Authorize(Policy = Permissions.EmployeesEdit)]
    [HttpDelete("{deptId:int}/nhanvien/{employeeId:int}")]
    public async Task<ActionResult<ApiResponse<object>>> RemoveNhanVienFromPhongBan(int deptId, int employeeId)
    {
        var phongBan = await _dbContext.PhongBans.FirstOrDefaultAsync(x => x.MaPhongBan == deptId);
        if (phongBan == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy phòng ban."));
        }

        var employee = await _dbContext.NhanViens.FirstOrDefaultAsync(x => x.MaNhanVien == employeeId);
        if (employee == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy nhân viên."));
        }

        if (employee.MaPhongBan != deptId && employee.PhoMaPhongBan != deptId)
        {
            return BadRequest(ApiResponse<object>.Fail("Nhân viên không thuộc phòng ban này."));
        }

        if (phongBan.MaTruongPhong == employeeId && employee.MaPhongBan == deptId)
        {
            return Conflict(ApiResponse<object>.Fail("Nhân viên đang là trưởng phòng. Hãy chuyển trưởng phòng trước khi rời phòng ban."));
        }

        if (employee.MaPhongBan == deptId)
        {
            employee.MaPhongBan = null;
        }

        if (employee.PhoMaPhongBan == deptId)
        {
            employee.PhoMaPhongBan = null;
        }

        await _dbContext.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(null, "Đã rời phòng ban thành công"));
    }

    [Authorize(Policy = Permissions.EmployeesView)]
    [HttpGet("{id:int}/duan")]
    public async Task<ActionResult<ApiResponse<List<DuAnPhongBanDto>>>> GetDuAnByPhongBan(int id)
    {
        var phongBanExists = await _dbContext.PhongBans.AnyAsync(x => x.MaPhongBan == id);
        if (!phongBanExists)
        {
            return NotFound(ApiResponse<List<DuAnPhongBanDto>>.Fail("Không tìm thấy phòng ban."));
        }

        var items = await _dbContext.DuAnPhongBans
            .AsNoTracking()
            .Where(x => x.MaPhongBan == id && (x.TrangThai ?? 1) == 1)
            .OrderBy(x => x.DuAn.TenDuAn)
            .Select(x => new DuAnPhongBanDto
            {
                MaDuAn = x.MaDuAn,
                TenDuAn = x.DuAn.TenDuAn,
                NgayThamGia = x.NgayThamGia,
                TrangThai = x.DuAn.TrangThai
            })
            .ToListAsync();

        return Ok(ApiResponse<List<DuAnPhongBanDto>>.Ok(items));
    }

    public class CreatePhongBanRequest
    {
        public string TenPhongBan { get; set; } = string.Empty;
        public string? MoTa { get; set; }
        public int? MaTruongPhong { get; set; }
    }

    public class UpdatePhongBanRequest : CreatePhongBanRequest;

    public class PhongBanDto
    {
        public int MaPhongBan { get; set; }
        public string? TenPhongBan { get; set; }
        public string? MoTa { get; set; }
        public int? MaTruongPhong { get; set; }
        public int SoNhanVien { get; set; }
    }

    public class NhanVienInPhongBanDto
    {
        public int MaNhanVien { get; set; }
        public string? HoTen { get; set; }
        public string? Email { get; set; }
        public string? Sdt { get; set; }
        public int? PhoMaPhongBan { get; set; }
        public int TrangThai { get; set; }
    }

    public class DuAnPhongBanDto
    {
        public int MaDuAn { get; set; }
        public string? TenDuAn { get; set; }
        public DateTime? NgayThamGia { get; set; }
        public int? TrangThai { get; set; }
    }
}




