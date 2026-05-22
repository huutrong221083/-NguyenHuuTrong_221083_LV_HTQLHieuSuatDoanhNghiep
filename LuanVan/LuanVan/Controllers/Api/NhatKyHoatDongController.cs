using LuanVan.Contracts;
using LuanVan.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LuanVan.Controllers.Api;

[ApiController]
[Route("nhatkyhoatdong")]
[Authorize(Policy = Permissions.SettingsView)]
public class NhatKyHoatDongController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public NhatKyHoatDongController(AppDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<NhatKyItemDto>>>> GetNhatKy(
        [FromQuery] int? nhanvien,
        [FromQuery] string? hanhDong,
        [FromQuery] string? doiTuong,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        try
        {
            EnsureConnectionStringInitialized();

            page = Math.Max(1, page);
            size = Math.Clamp(size, 1, 100);

            IQueryable<Models.NhatKyHoatDong> query = _dbContext.NhatKyHoatDongs.AsNoTracking();

            if (nhanvien.HasValue)
            {
                query = query.Where(x => x.MaNhanVien == nhanvien.Value);
            }

            if (!string.IsNullOrWhiteSpace(hanhDong))
            {
                var action = hanhDong.Trim();
                query = query.Where(x => x.HanhDong != null && EF.Functions.Like(x.HanhDong, $"%{action}%"));
            }

            if (!string.IsNullOrWhiteSpace(doiTuong))
            {
                var target = doiTuong.Trim();
                query = query.Where(x => x.DoiTuong != null && EF.Functions.Like(x.DoiTuong, $"%{target}%"));
            }

            if (from.HasValue)
            {
                query = query.Where(x => x.ThoiGian.HasValue && x.ThoiGian.Value >= from.Value);
            }

            if (to.HasValue)
            {
                var toInclusive = to.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(x => x.ThoiGian.HasValue && x.ThoiGian.Value <= toInclusive);
            }

            var totalItems = await query.CountAsync();
            var items = await query
                .OrderByDescending(x => x.ThoiGian)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(x => new NhatKyItemDto
                {
                    MaNhatKyHoatDong = x.MaNhatKyHoatDong,
                    MaNhanVien = x.MaNhanVien,
                    HoTenNhanVien = x.NhanVien.HoTen,
                    HanhDong = x.HanhDong,
                    DoiTuong = x.DoiTuong,
                    DuLieuCu = x.DuLieuCu,
                    DuLieuMoi = x.DuLieuMoi,
                    Ip = x.Ip,
                    TrangThai = x.TrangThai,
                    ThoiGian = x.ThoiGian
                })
                .ToListAsync();

            var result = new PagedResult<NhatKyItemDto>
            {
                Items = items,
                Page = page,
                Size = size,
                TotalItems = totalItems,
                TotalPages = (int)Math.Ceiling(totalItems / (double)size)
            };

            return Ok(ApiResponse<PagedResult<NhatKyItemDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<PagedResult<NhatKyItemDto>>.Fail(
                    $"Không tải được nhật ký hoạt động: {ex.Message}",
                    "AUDIT_LOG_LOAD_FAILED"));
        }
    }

    private void EnsureConnectionStringInitialized()
    {
        var current = _dbContext.Database.GetConnectionString();
        if (!string.IsNullOrWhiteSpace(current))
        {
            return;
        }

        var fallback =
            _configuration.GetConnectionString("DefaultConnection")
            ?? _configuration["ConnectionStrings:DefaultConnection"]
            ?? _configuration["ConnectionStrings__DefaultConnection"];

        if (string.IsNullOrWhiteSpace(fallback))
        {
            throw new InvalidOperationException("Chuỗi kết nối CSDL chưa được cấu hình.");
        }

        _dbContext.Database.SetConnectionString(fallback);
    }

    public class NhatKyItemDto
    {
        public int MaNhatKyHoatDong { get; set; }
        public int MaNhanVien { get; set; }
        public string? HoTenNhanVien { get; set; }
        public string? HanhDong { get; set; }
        public string? DoiTuong { get; set; }
        public string? DuLieuCu { get; set; }
        public string? DuLieuMoi { get; set; }
        public string? Ip { get; set; }
        public string? TrangThai { get; set; }
        public DateTime? ThoiGian { get; set; }
    }
}
