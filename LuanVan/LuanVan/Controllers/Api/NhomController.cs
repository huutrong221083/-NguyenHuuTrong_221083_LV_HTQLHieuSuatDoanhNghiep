using LuanVan.Contracts;
using LuanVan.Data;
using LuanVan.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace LuanVan.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/nhom")]
public class NhomController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public NhomController(AppDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
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

    private sealed class ManagerTeamScope
    {
        public int MaNhanVien { get; set; }
        public HashSet<int> VisibleTeamIds { get; set; } = new();
    }

    private async Task<ManagerTeamScope?> GetManagerTeamScopeAsync()
    {
        if (User.IsInRole(Roles.Admin) || !User.IsInRole(Roles.Manager))
        {
            return null;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new ManagerTeamScope { MaNhanVien = 0 };
        }

        var manager = await _dbContext.NhanViens
            .AsNoTracking()
            .Where(x => x.AspNetUserId == userId)
            .Select(x => new { x.MaNhanVien })
            .FirstOrDefaultAsync();

        if (manager == null)
        {
            return new ManagerTeamScope { MaNhanVien = 0 };
        }

        var ledTeamIds = await _dbContext.Nhoms
            .AsNoTracking()
            .Where(x => x.TruongNhom == manager.MaNhanVien)
            .Select(x => x.MaNhom)
            .ToListAsync();

        var memberTeamIds = await _dbContext.ThanhVienNhoms
            .AsNoTracking()
            .Where(x => x.MaNhanVien == manager.MaNhanVien)
            .Select(x => x.MaNhom)
            .ToListAsync();

        return new ManagerTeamScope
        {
            MaNhanVien = manager.MaNhanVien,
            VisibleTeamIds = ledTeamIds.Union(memberTeamIds).Distinct().ToHashSet()
        };
    }

    private async Task<bool> IsTeamInScopeAsync(int maNhom)
    {
        var scope = await GetManagerTeamScopeAsync();
        return scope == null || (scope.MaNhanVien != 0 && scope.VisibleTeamIds.Contains(maNhom));
    }

    private async Task<bool> CanManagerMutateTeamAsync(int maNhom)
    {
        var scope = await GetManagerTeamScopeAsync();
        if (scope == null)
        {
            return true;
        }

        if (scope.MaNhanVien == 0)
        {
            return false;
        }

        var team = await _dbContext.Nhoms
            .AsNoTracking()
            .Where(x => x.MaNhom == maNhom)
            .Select(x => new { x.TruongNhom })
            .FirstOrDefaultAsync();

        return team != null && team.TruongNhom == scope.MaNhanVien;
    }

    [Authorize(Policy = Permissions.EmployeesCreate)]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> CreateNhom([FromBody] CreateNhomRequest request)
    {
        EnsureDbConnectionStringInitialized();

        if (User.IsInRole(Roles.Manager) && !User.IsInRole(Roles.Admin))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<object>.Fail("Manager không được tạo nhóm trong phiên bản V1."));
        }

        if (string.IsNullOrWhiteSpace(request.TenNhom))
        {
            return BadRequest(ApiResponse<object>.Fail("Tên nhóm là bắt buộc."));
        }

        var tenNhom = request.TenNhom.Trim();
        var duplicateName = await _dbContext.Nhoms.AnyAsync(x => x.TenNhom != null
            && EF.Functions.Collate(x.TenNhom, "Latin1_General_CI_AI") == tenNhom);
        if (duplicateName)
        {
            return Conflict(ApiResponse<object>.Fail("Tên nhóm đã tồn tại."));
        }

        var leader = await _dbContext.NhanViens.FirstOrDefaultAsync(x => x.MaNhanVien == request.TruongNhom);
        if (leader == null || leader.TrangThai != 1)
        {
            return BadRequest(ApiResponse<object>.Fail("Trưởng nhóm không tồn tại hoặc đã nghỉ việc."));
        }

        var Nhom = new Nhom
        {
            TenNhom = tenNhom,
            NgayTao = DateTime.Now,
            TruongNhom = request.TruongNhom
        };

        _dbContext.Nhoms.Add(Nhom);
        await _dbContext.SaveChangesAsync();

        _dbContext.ThanhVienNhoms.Add(new ThanhVienNhom
        {
            MaNhom = Nhom.MaNhom,
            MaNhanVien = request.TruongNhom,
            NgayGiaNhap = DateTime.Now,
            VaiTroTrongNhom = "Trưởng nhóm"
        });

        await _dbContext.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { Nhom.MaNhom }, "Tạo nhóm thành công"));
    }

    [Authorize(Policy = Permissions.EmployeesEdit)]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateNhom(int id, [FromBody] UpdateNhomRequest request)
    {
        EnsureDbConnectionStringInitialized();

        if (string.IsNullOrWhiteSpace(request.TenNhom))
        {
            return BadRequest(ApiResponse<object>.Fail("Tên nhóm là bắt buộc."));
        }

        var nhom = await _dbContext.Nhoms.FirstOrDefaultAsync(x => x.MaNhom == id);
        if (nhom == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy nhóm."));
        }

        if (!await CanManagerMutateTeamAsync(id))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<object>.Fail("Bạn không có quyền cập nhật nhóm này."));
        }

        var tenNhom = request.TenNhom.Trim();
        var duplicateName = await _dbContext.Nhoms.AnyAsync(x => x.MaNhom != id
            && x.TenNhom != null
            && EF.Functions.Collate(x.TenNhom, "Latin1_General_CI_AI") == tenNhom);
        if (duplicateName)
        {
            return Conflict(ApiResponse<object>.Fail("Tên nhóm đã tồn tại."));
        }

        var leader = await _dbContext.NhanViens.FirstOrDefaultAsync(x => x.MaNhanVien == request.TruongNhom);
        if (leader == null || leader.TrangThai != 1)
        {
            return BadRequest(ApiResponse<object>.Fail("Trưởng nhóm không tồn tại hoặc đã nghỉ việc."));
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync();

        var oldLeaderId = nhom.TruongNhom;
        nhom.TenNhom = tenNhom;
        nhom.TruongNhom = request.TruongNhom;

        var oldLeaderMember = await _dbContext.ThanhVienNhoms
            .FirstOrDefaultAsync(x => x.MaNhom == id && x.MaNhanVien == oldLeaderId);
        if (oldLeaderMember != null)
        {
            oldLeaderMember.VaiTroTrongNhom = "Thành viên";
        }

        var newLeaderMember = await _dbContext.ThanhVienNhoms
            .FirstOrDefaultAsync(x => x.MaNhom == id && x.MaNhanVien == request.TruongNhom);
        if (newLeaderMember == null)
        {
            _dbContext.ThanhVienNhoms.Add(new ThanhVienNhom
            {
                MaNhom = id,
                MaNhanVien = request.TruongNhom,
                NgayGiaNhap = DateTime.Now,
                VaiTroTrongNhom = "Trưởng nhóm"
            });
        }
        else
        {
            newLeaderMember.VaiTroTrongNhom = "Trưởng nhóm";
        }

        await _dbContext.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(ApiResponse<object>.Ok(new { nhom.MaNhom }, "Cập nhật nhóm thành công"));
    }

    [Authorize(Policy = Permissions.EmployeesDelete)]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteNhom(int id)
    {
        EnsureDbConnectionStringInitialized();

        var nhom = await _dbContext.Nhoms.FirstOrDefaultAsync(x => x.MaNhom == id);
        if (nhom == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy nhóm."));
        }

        if (!await CanManagerMutateTeamAsync(id))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<object>.Fail("Bạn không có quyền xóa nhóm này."));
        }

        var hasProject = await _dbContext.DuAnNhoms.AnyAsync(x => x.MaNhom == id && (x.TrangThai ?? 1) != 0);
        var hasTask = await _dbContext.PhanCongNhoms.AnyAsync(x => x.MaNhom == id && (x.TrangThai ?? 1) != 0);
        var hasKpi = await _dbContext.KpiNhoms.AnyAsync(x => x.MaNhom == id && (x.TrangThai ?? 1) != 0);
        if (hasProject || hasTask || hasKpi)
        {
            return Conflict(ApiResponse<object>.Fail("Nhóm đang có dữ liệu hoạt động (dự án/công việc/KPI), không thể xóa."));
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync();

        var members = await _dbContext.ThanhVienNhoms.Where(x => x.MaNhom == id).ToListAsync();
        _dbContext.ThanhVienNhoms.RemoveRange(members);
        _dbContext.Nhoms.Remove(nhom);
        await _dbContext.SaveChangesAsync();

        await tx.CommitAsync();
        return Ok(ApiResponse<object>.Ok(null, "Xóa nhóm thành công"));
    }

    [Authorize(Policy = Permissions.EmployeesEdit)]
    [HttpPost("add-member")]
    public async Task<ActionResult<ApiResponse<object>>> AddMember([FromBody] AddMemberRequest request)
    {
        EnsureDbConnectionStringInitialized();

        var nhomExists = await _dbContext.Nhoms.AnyAsync(x => x.MaNhom == request.MaNhom);
        if (!nhomExists)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy nhóm."));
        }

        if (!await CanManagerMutateTeamAsync(request.MaNhom))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<object>.Fail("Bạn không có quyền thêm thành viên vào nhóm này."));
        }

        var employee = await _dbContext.NhanViens.FirstOrDefaultAsync(x => x.MaNhanVien == request.MaNhanVien);
        if (employee == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy nhân viên."));
        }

        if (employee.TrangThai != 1)
        {
            return BadRequest(ApiResponse<object>.Fail("Nhân viên nghỉ việc không thể thêm vào nhóm."));
        }

        var existed = await _dbContext.ThanhVienNhoms.AnyAsync(x => x.MaNhom == request.MaNhom && x.MaNhanVien == request.MaNhanVien);
        if (existed)
        {
            return Conflict(ApiResponse<object>.Fail("Nhân viên đã ở trong nhóm."));
        }

        _dbContext.ThanhVienNhoms.Add(new ThanhVienNhom
        {
            MaNhom = request.MaNhom,
            MaNhanVien = request.MaNhanVien,
            NgayGiaNhap = DateTime.Now,
            VaiTroTrongNhom = string.IsNullOrWhiteSpace(request.VaiTroTrongNhom) ? "Thành viên" : request.VaiTroTrongNhom.Trim()
        });

        await _dbContext.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(null, "Thêm thành viên vào nhóm thành công"));
    }

    [Authorize(Policy = Permissions.EmployeesEdit)]
    [HttpDelete("{maNhom:int}/members/{maNhanVien:int}")]
    public async Task<ActionResult<ApiResponse<object>>> RemoveMember(int maNhom, int maNhanVien)
    {
        EnsureDbConnectionStringInitialized();

        var nhom = await _dbContext.Nhoms.AsNoTracking().FirstOrDefaultAsync(x => x.MaNhom == maNhom);
        if (nhom == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy nhóm."));
        }

        if (!await CanManagerMutateTeamAsync(maNhom))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<object>.Fail("Bạn không có quyền xóa thành viên khỏi nhóm này."));
        }

        if (nhom.TruongNhom == maNhanVien)
        {
            return Conflict(ApiResponse<object>.Fail("Không thể xóa trưởng nhóm khỏi nhóm. Hãy gán trưởng nhóm mới trước."));
        }

        var item = await _dbContext.ThanhVienNhoms.FirstOrDefaultAsync(x => x.MaNhom == maNhom && x.MaNhanVien == maNhanVien);
        if (item == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy thành viên trong nhóm."));
        }

        _dbContext.ThanhVienNhoms.Remove(item);
        await _dbContext.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(null, "Xóa thành viên khỏi nhóm thành công"));
    }

    [Authorize(Policy = Permissions.EmployeesEdit)]
    [HttpPut("{maNhom:int}/members/{maNhanVien:int}/role")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateMemberRole(int maNhom, int maNhanVien, [FromBody] UpdateMemberRoleRequest request)
    {
        EnsureDbConnectionStringInitialized();

        var vaiTro = request.VaiTroTrongNhom?.Trim();
        if (string.IsNullOrWhiteSpace(vaiTro))
        {
            return BadRequest(ApiResponse<object>.Fail("Vai trò trong nhóm là bắt buộc."));
        }

        var item = await _dbContext.ThanhVienNhoms
            .Include(x => x.Nhom)
            .FirstOrDefaultAsync(x => x.MaNhom == maNhom && x.MaNhanVien == maNhanVien);
        if (item == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy thành viên trong nhóm."));
        }

        if (!await CanManagerMutateTeamAsync(maNhom))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<object>.Fail("Bạn không có quyền cập nhật vai trò thành viên nhóm này."));
        }

        item.VaiTroTrongNhom = vaiTro;
        if (item.Nhom?.TruongNhom == maNhanVien)
        {
            item.VaiTroTrongNhom = "Trưởng nhóm";
        }

        await _dbContext.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(null, "Cập nhật vai trò thành công"));
    }

    [Authorize(Policy = Permissions.EmployeesView)]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<NhomDto>>>> GetNhoms()
    {
        EnsureDbConnectionStringInitialized();

        var query = _dbContext.Nhoms.AsNoTracking().AsQueryable();
        var scope = await GetManagerTeamScopeAsync();
        if (scope is { MaNhanVien: 0 })
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<List<NhomDto>>.Fail("Tài khoản quản lý chưa được liên kết nhân viên."));
        }

        if (scope != null)
        {
            query = query.Where(x => scope.VisibleTeamIds.Contains(x.MaNhom));
        }

        var items = await query
            .OrderByDescending(x => x.NgayTao)
            .Select(x => new NhomDto
            {
                MaNhom = x.MaNhom,
                TenNhom = x.TenNhom,
                NgayTao = x.NgayTao,
                TruongNhom = x.TruongNhom,
                TenTruongNhom = x.NhanVienTruongNhom != null ? x.NhanVienTruongNhom.HoTen : null,
                SoThanhVien = x.ThanhVienNhoms.Count
            })
            .ToListAsync();

        return Ok(ApiResponse<List<NhomDto>>.Ok(items));
    }

    [Authorize(Policy = Permissions.EmployeesView)]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<NhomDetailDto>>> GetNhomById(int id)
    {
        EnsureDbConnectionStringInitialized();

        if (!await IsTeamInScopeAsync(id))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<NhomDetailDto>.Fail("Bạn không có quyền truy cập nhóm này."));
        }

        var item = await _dbContext.Nhoms
            .AsNoTracking()
            .Where(x => x.MaNhom == id)
            .Select(x => new NhomDetailDto
            {
                MaNhom = x.MaNhom,
                TenNhom = x.TenNhom,
                NgayTao = x.NgayTao,
                TruongNhom = x.TruongNhom,
                TenTruongNhom = x.NhanVienTruongNhom != null ? x.NhanVienTruongNhom.HoTen : null,
                ThanhViens = x.ThanhVienNhoms.Select(tv => new NhomMemberDto
                {
                    MaNhanVien = tv.MaNhanVien,
                    HoTen = tv.NhanVien.HoTen,
                    VaiTroTrongNhom = tv.VaiTroTrongNhom,
                    NgayGiaNhap = tv.NgayGiaNhap,
                    TrangThaiNhanVien = tv.NhanVien.TrangThai ?? 0
                }).ToList()
            })
            .FirstOrDefaultAsync();

        if (item == null)
        {
            return NotFound(ApiResponse<NhomDetailDto>.Fail("Không tìm thấy nhóm."));
        }

        return Ok(ApiResponse<NhomDetailDto>.Ok(item));
    }

    [Authorize(Policy = Permissions.EmployeesView)]
    [HttpGet("{id:int}/duan")]
    public async Task<ActionResult<ApiResponse<List<NhomDuAnDto>>>> GetProjectsByNhom(int id)
    {
        EnsureDbConnectionStringInitialized();

        if (!await IsTeamInScopeAsync(id))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<List<NhomDuAnDto>>.Fail("Bạn không có quyền xem dự án của nhóm này."));
        }

        var projects = await _dbContext.DuAnNhoms
            .AsNoTracking()
            .Where(x => x.MaNhom == id && (x.TrangThai ?? 1) == 1)
            .OrderBy(x => x.DuAn.TenDuAn)
            .Select(x => new NhomDuAnDto
            {
                MaDuAn = x.MaDuAn,
                TenDuAn = x.DuAn != null ? x.DuAn.TenDuAn : null,
                NgayThamGia = x.NgayThamGia,
                TrangThai = x.TrangThai
            })
            .ToListAsync();

        return Ok(ApiResponse<List<NhomDuAnDto>>.Ok(projects));
    }

    [Authorize(Policy = Permissions.EmployeesView)]
    [HttpGet("{id:int}/kpi")]
    public async Task<ActionResult<ApiResponse<List<NhomKpiDto>>>> GetKpiByNhom(int id)
    {
        EnsureDbConnectionStringInitialized();

        if (!await IsTeamInScopeAsync(id))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<List<NhomKpiDto>>.Fail("Bạn không có quyền xem KPI của nhóm này."));
        }

        var memberIds = await _dbContext.ThanhVienNhoms
            .AsNoTracking()
            .Where(x => x.MaNhom == id)
            .Select(x => x.MaNhanVien)
            .Distinct()
            .ToListAsync();

        var scoreByKpi = memberIds.Count == 0
            ? new Dictionary<int, double?>()
            : await _dbContext.KetQuaKpis
                .AsNoTracking()
                .Where(x => memberIds.Contains(x.MaNhanVien) && x.DiemSo.HasValue)
                .GroupBy(x => x.MaKpi)
                .Select(g => new
                {
                    MaKpi = g.Key,
                    DiemTrungBinh = g.Average(x => x.DiemSo ?? 0m)
                })
                .ToDictionaryAsync(x => x.MaKpi, x => (double?)x.DiemTrungBinh);

        var assignments = await _dbContext.KpiNhoms
            .AsNoTracking()
            .Where(x => x.MaNhom == id)
            .OrderBy(x => x.MaKpi)
            .Select(x => new
            {
                MaKpi = x.MaKpi,
                TenKpi = x.DanhMucKpi != null ? x.DanhMucKpi.TenKpi : null,
                TuNgay = x.TuNgay,
                DenNgay = x.DenNgay,
                TrongSoApDung = (double)x.TrongSoApDung,
                IsActive = x.IsActive
            })
            .ToListAsync();

        var kpis = assignments
            .Select(x => new NhomKpiDto
            {
                MaKpi = x.MaKpi,
                TenKpi = x.TenKpi,
                TuNgay = x.TuNgay,
                DenNgay = x.DenNgay,
                TrongSoApDung = x.TrongSoApDung,
                DiemDanhGia = scoreByKpi.TryGetValue(x.MaKpi, out var score) ? score : null,
                IsActive = x.IsActive
            })
            .ToList();

        return Ok(ApiResponse<List<NhomKpiDto>>.Ok(kpis));
    }

    public class CreateNhomRequest
    {
        public string TenNhom { get; set; } = string.Empty;
        public int TruongNhom { get; set; }
    }

    public class UpdateNhomRequest : CreateNhomRequest;

    public class AddMemberRequest
    {
        public int MaNhom { get; set; }
        public int MaNhanVien { get; set; }
        public string? VaiTroTrongNhom { get; set; }
    }

    public class NhomDto
    {
        public int MaNhom { get; set; }
        public string? TenNhom { get; set; }
        public DateTime? NgayTao { get; set; }
        public int? TruongNhom { get; set; }
        public string? TenTruongNhom { get; set; }
        public int SoThanhVien { get; set; }
    }

    public class NhomDetailDto
    {
        public int MaNhom { get; set; }
        public string? TenNhom { get; set; }
        public DateTime? NgayTao { get; set; }
        public int? TruongNhom { get; set; }
        public string? TenTruongNhom { get; set; }
        public List<NhomMemberDto> ThanhViens { get; set; } = new();
    }

    public class NhomMemberDto
    {
        public int MaNhanVien { get; set; }
        public string? HoTen { get; set; }
        public string? VaiTroTrongNhom { get; set; }
        public DateTime? NgayGiaNhap { get; set; }
        public int TrangThaiNhanVien { get; set; }
    }

    public class NhomDuAnDto
    {
        public int MaDuAn { get; set; }
        public string? TenDuAn { get; set; }
        public DateTime? NgayThamGia { get; set; }
        public int? TrangThai { get; set; }
    }

    public class NhomKpiDto
    {
        public int MaKpi { get; set; }
        public string? TenKpi { get; set; }
        public DateTime? TuNgay { get; set; }
        public DateTime? DenNgay { get; set; }
        public double? TrongSoApDung { get; set; }
        public double? DiemDanhGia { get; set; }
        public bool IsActive { get; set; }
    }

    public class UpdateMemberRoleRequest
    {
        public string? VaiTroTrongNhom { get; set; }
    }
}




