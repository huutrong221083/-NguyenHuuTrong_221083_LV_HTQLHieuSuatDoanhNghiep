using LuanVan.Contracts;
using LuanVan.Data;
using LuanVan.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LuanVan.Controllers.Api;

[ApiController]
[Route("chucvu")]
[Authorize]
public class ChucVuController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public ChucVuController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Authorize(Policy = Permissions.EmployeesView)]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ChucVuDto>>>> GetChucVu()
    {
        var items = await _dbContext.ChucVus
            .AsNoTracking()
            .OrderBy(x => x.TenChucVu)
            .Select(x => new ChucVuDto
            {
                MaChucVu = x.MaChucVu,
                TenChucVu = x.TenChucVu
            })
            .ToListAsync();

        return Ok(ApiResponse<List<ChucVuDto>>.Ok(items));
    }

    [Authorize(Policy = Permissions.EmployeesCreate)]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ChucVuDto>>> CreateChucVu([FromBody] SaveChucVuRequest request)
    {
        var tenChucVu = request.TenChucVu?.Trim();
        if (string.IsNullOrWhiteSpace(tenChucVu))
        {
            return BadRequest(ApiResponse<ChucVuDto>.Fail("Tên chức vụ là bắt buộc."));
        }

        var exists = await _dbContext.ChucVus.AnyAsync(x =>
            x.TenChucVu != null
            && EF.Functions.Collate(x.TenChucVu, "Latin1_General_CI_AI") == tenChucVu);
        if (exists)
        {
            return Conflict(ApiResponse<ChucVuDto>.Fail("Chức vụ đã tồn tại."));
        }

        var entity = new ChucVu
        {
            TenChucVu = tenChucVu
        };

        _dbContext.ChucVus.Add(entity);
        await _dbContext.SaveChangesAsync();

        var dto = new ChucVuDto
        {
            MaChucVu = entity.MaChucVu,
            TenChucVu = entity.TenChucVu
        };

        return CreatedAtAction(nameof(GetChucVu), ApiResponse<ChucVuDto>.Ok(dto, "Thêm chức vụ thành công"));
    }

    [Authorize(Policy = Permissions.EmployeesEdit)]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<ChucVuDto>>> UpdateChucVu(int id, [FromBody] SaveChucVuRequest request)
    {
        var tenChucVu = request.TenChucVu?.Trim();
        if (string.IsNullOrWhiteSpace(tenChucVu))
        {
            return BadRequest(ApiResponse<ChucVuDto>.Fail("Tên chức vụ là bắt buộc."));
        }

        var entity = await _dbContext.ChucVus.FirstOrDefaultAsync(x => x.MaChucVu == id);
        if (entity == null)
        {
            return NotFound(ApiResponse<ChucVuDto>.Fail("Không tìm thấy chức vụ."));
        }

        var exists = await _dbContext.ChucVus.AnyAsync(x =>
            x.MaChucVu != id
            && x.TenChucVu != null
            && EF.Functions.Collate(x.TenChucVu, "Latin1_General_CI_AI") == tenChucVu);
        if (exists)
        {
            return Conflict(ApiResponse<ChucVuDto>.Fail("Tên chức vụ đã tồn tại."));
        }

        entity.TenChucVu = tenChucVu;
        await _dbContext.SaveChangesAsync();

        var dto = new ChucVuDto
        {
            MaChucVu = entity.MaChucVu,
            TenChucVu = entity.TenChucVu
        };

        return Ok(ApiResponse<ChucVuDto>.Ok(dto, "Cập nhật chức vụ thành công"));
    }

    [Authorize(Policy = Permissions.EmployeesDelete)]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteChucVu(int id)
    {
        var entity = await _dbContext.ChucVus.FirstOrDefaultAsync(x => x.MaChucVu == id);
        if (entity == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy chức vụ."));
        }

        var inUse = await _dbContext.NhanViens.AnyAsync(x => x.MaChucVu == id && (x.TrangThai ?? 1) == 1);
        if (inUse)
        {
            return Conflict(ApiResponse<object>.Fail("Chức vụ đang được gán cho nhân viên hoạt động, không thể xóa."));
        }

        _dbContext.ChucVus.Remove(entity);
        await _dbContext.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(null, "Xóa chức vụ thành công"));
    }

    public class ChucVuDto
    {
        public int MaChucVu { get; set; }
        public string? TenChucVu { get; set; }
    }

    public class SaveChucVuRequest
    {
        public string? TenChucVu { get; set; }
    }
}
