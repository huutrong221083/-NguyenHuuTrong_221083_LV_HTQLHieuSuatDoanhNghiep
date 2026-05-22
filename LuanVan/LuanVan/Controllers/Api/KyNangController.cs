using LuanVan.Contracts;
using LuanVan.Data;
using LuanVan.Models;
using LuanVan.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LuanVan.Controllers.Api;

[ApiController]
[Route("kynang")]
[Authorize]
public class KyNangController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogService _auditLogService;

    public KyNangController(AppDbContext dbContext, UserManager<ApplicationUser> userManager, IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<KyNangDto>>>> GetKyNangs()
    {
        try
        {
            var items = await QueryKyNangDtosAsync(onlyActive: true, includeDescription: true);
            return Ok(ApiResponse<List<KyNangDto>>.Ok(items));
        }
        catch (Exception ex)
        {
            // Fallback for environments where TRANGTHAI and/or MOTA columns are missing.
            try
            {
                var fallbackItems = await QueryKyNangDtosAsync(onlyActive: false, includeDescription: true);
                return Ok(ApiResponse<List<KyNangDto>>.Ok(
                    fallbackItems,
                    $"Dang dung fallback danh muc ky nang (khong loc TrangThai): {ex.Message}"));
            }
            catch (Exception fallbackEx)
            {
                try
                {
                    // Last-resort fallback for old schemas that only have MAKYNANG + TENKYNANG.
                    var minimalItems = await QueryKyNangDtosAsync(onlyActive: false, includeDescription: false);
                    return Ok(ApiResponse<List<KyNangDto>>.Ok(
                        minimalItems,
                        $"Dang dung fallback danh muc ky nang toi thieu (bo qua cot TrangThai/MoTa): {fallbackEx.Message}"));
                }
                catch
                {
                    // Keep Employees screen usable even if skill catalog cannot be read.
                    return Ok(ApiResponse<List<KyNangDto>>.Ok(new List<KyNangDto>(), $"Ky nang fallback: {ex.Message}"));
                }
            }
        }
    }

    private Task<List<KyNangDto>> QueryKyNangDtosAsync(bool onlyActive, bool includeDescription)
    {
        var query = _dbContext.KyNangs.AsNoTracking();

        if (onlyActive)
        {
            query = query.Where(x => (x.TrangThai ?? 1) == 1);
        }

        return query
            .OrderBy(x => x.TenKyNang)
            .Select(x => new KyNangDto
            {
                MaKyNang = x.MaKyNang,
                TenKyNang = x.TenKyNang,
                MoTa = includeDescription ? x.MoTa : null
            })
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<KyNangDto>>> CreateKyNang([FromBody] SaveKyNangRequest request)
    {
        var tenKyNang = request.TenKyNang?.Trim();
        if (string.IsNullOrWhiteSpace(tenKyNang))
        {
            return BadRequest(ApiResponse<KyNangDto>.Fail("Tên kỹ năng là bắt buộc."));
        }

        var exists = await _dbContext.KyNangs.AnyAsync(x =>
            (x.TrangThai ?? 1) == 1
            && x.TenKyNang != null
            && EF.Functions.Collate(x.TenKyNang, "Latin1_General_CI_AI") == tenKyNang);
        if (exists)
        {
            return Conflict(ApiResponse<KyNangDto>.Fail("Kỹ năng đã tồn tại."));
        }

        var entity = new KyNang
        {
            TenKyNang = tenKyNang,
            MoTa = string.IsNullOrWhiteSpace(request.MoTa) ? null : request.MoTa.Trim(),
            TrangThai = 1
        };

        _dbContext.KyNangs.Add(entity);
        await _dbContext.SaveChangesAsync();
        await WriteAuditAsync($"Tạo kỹ năng {entity.TenKyNang} (Mã: {entity.MaKyNang})");

        var dto = new KyNangDto
        {
            MaKyNang = entity.MaKyNang,
            TenKyNang = entity.TenKyNang,
            MoTa = entity.MoTa
        };

        return CreatedAtAction(nameof(GetKyNangs), ApiResponse<KyNangDto>.Ok(dto, "Thêm kỹ năng thành công"));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<KyNangDto>>> UpdateKyNang(int id, [FromBody] SaveKyNangRequest request)
    {
        var tenKyNang = request.TenKyNang?.Trim();
        if (string.IsNullOrWhiteSpace(tenKyNang))
        {
            return BadRequest(ApiResponse<KyNangDto>.Fail("Tên kỹ năng là bắt buộc."));
        }

        var entity = await _dbContext.KyNangs.FirstOrDefaultAsync(x => x.MaKyNang == id && (x.TrangThai ?? 1) == 1);
        if (entity == null)
        {
            return NotFound(ApiResponse<KyNangDto>.Fail("Không tìm thấy kỹ năng."));
        }

        var exists = await _dbContext.KyNangs.AnyAsync(x =>
            x.MaKyNang != id
            && (x.TrangThai ?? 1) == 1
            && x.TenKyNang != null
            && EF.Functions.Collate(x.TenKyNang, "Latin1_General_CI_AI") == tenKyNang);
        if (exists)
        {
            return Conflict(ApiResponse<KyNangDto>.Fail("Tên kỹ năng đã tồn tại."));
        }

        var oldTenKyNang = entity.TenKyNang;
        entity.TenKyNang = tenKyNang;
        entity.MoTa = string.IsNullOrWhiteSpace(request.MoTa) ? null : request.MoTa.Trim();
        await _dbContext.SaveChangesAsync();
        await WriteAuditAsync($"Cập nhật kỹ năng (Mã: {id}) từ '{oldTenKyNang ?? "(trống)"}' sang '{entity.TenKyNang ?? "(trống)"}'");

        var dto = new KyNangDto
        {
            MaKyNang = entity.MaKyNang,
            TenKyNang = entity.TenKyNang,
            MoTa = entity.MoTa
        };

        return Ok(ApiResponse<KyNangDto>.Ok(dto, "Cập nhật kỹ năng thành công"));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteKyNang(int id)
    {
        var entity = await _dbContext.KyNangs.FirstOrDefaultAsync(x => x.MaKyNang == id && (x.TrangThai ?? 1) == 1);
        if (entity == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy kỹ năng."));
        }

        var inUseByEmployee = await _dbContext.KyNangNhanViens.AnyAsync(x => x.MaKyNang == id);
        var inUseByTask = await _dbContext.CongViecKyNangs.AnyAsync(x => x.MaKyNang == id);
        if (inUseByEmployee || inUseByTask)
        {
            return Conflict(ApiResponse<object>.Fail("Kỹ năng đang được sử dụng nên không thể xóa."));
        }

        entity.TrangThai = 0;
        await _dbContext.SaveChangesAsync();
        await WriteAuditAsync($"Xóa mềm kỹ năng {entity.TenKyNang} (Mã: {id})");

        return Ok(ApiResponse<object>.Ok(null, "Xóa mềm kỹ năng thành công"));
    }

    private async Task WriteAuditAsync(string action)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        await _auditLogService.LogByUserIdAsync(userId, action);
    }

    public class KyNangDto
    {
        public int MaKyNang { get; set; }
        public string? TenKyNang { get; set; }
        public string? MoTa { get; set; }
    }

    public class SaveKyNangRequest
    {
        public string? TenKyNang { get; set; }
        public string? MoTa { get; set; }
    }
}
