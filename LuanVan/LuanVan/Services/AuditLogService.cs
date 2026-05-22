using LuanVan.Data;
using LuanVan.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LuanVan.Services;

public interface IAuditLogService
{
    Task<bool> LogByNhanVienIdAsync(int? maNhanVien, string action, CancellationToken cancellationToken = default);
    Task<bool> LogByUserIdAsync(string? userId, string action, CancellationToken cancellationToken = default);
    Task<bool> LogStructuredByUserIdAsync(
        string? userId,
        string hanhDong,
        string doiTuong,
        object? duLieuCu = null,
        object? duLieuMoi = null,
        string? trangThai = null,
        CancellationToken cancellationToken = default);
    Task<bool> LogTaskActivityAsync(
        int maCongViec,
        int? maNhanVien,
        string hanhDong,
        string noiDung,
        CancellationToken cancellationToken = default);
}

public class AuditLogService : IAuditLogService
{
    private readonly AppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditLogService> _logger;
    private static readonly JsonSerializerOptions AuditJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public AuditLogService(
        AppDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditLogService> logger)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<bool> LogByNhanVienIdAsync(int? maNhanVien, string action, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!maNhanVien.HasValue || string.IsNullOrWhiteSpace(action))
            {
                return false;
            }

            var employeeExists = await _dbContext.NhanViens
                .AsNoTracking()
                .AnyAsync(x => x.MaNhanVien == maNhanVien.Value, cancellationToken);

            if (!employeeExists)
            {
                return false;
            }

            _dbContext.NhatKyHoatDongs.Add(new NhatKyHoatDong
            {
                MaNhanVien = maNhanVien.Value,
                HanhDong = action.Trim(),
                DoiTuong = "SYSTEM",
                ThoiGian = DateTime.Now,
                Ip = ResolveRequestIp(),
                TrangThai = "SUCCESS"
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch
        {
            _logger.LogWarning("Ghi log theo mã nhân viên thất bại. MaNhanVien={MaNhanVien}, Action={Action}", maNhanVien, action);
            return false;
        }
    }

    public async Task<bool> LogByUserIdAsync(string? userId, string action, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            var maNhanVien = await ResolveMaNhanVienFromUserIdAsync(userId, cancellationToken);
            if (!maNhanVien.HasValue)
            {
                _logger.LogWarning("Bỏ qua ghi log vì không tìm thấy nhân viên liên kết với UserId={UserId}. Action={Action}", userId, action);
                return false;
            }

            return await LogByNhanVienIdAsync(maNhanVien.Value, action, cancellationToken);
        }
        catch
        {
            _logger.LogWarning("Ghi log theo user id thất bại. UserId={UserId}, Action={Action}", userId, action);
            return false;
        }
    }

    public async Task<bool> LogStructuredByUserIdAsync(
        string? userId,
        string hanhDong,
        string doiTuong,
        object? duLieuCu = null,
        object? duLieuMoi = null,
        string? trangThai = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId)
                || string.IsNullOrWhiteSpace(hanhDong)
                || string.IsNullOrWhiteSpace(doiTuong))
            {
                return false;
            }

            var maNhanVien = await ResolveMaNhanVienFromUserIdAsync(userId, cancellationToken);
            if (!maNhanVien.HasValue)
            {
                _logger.LogWarning("Bỏ qua ghi structured log vì không tìm thấy nhân viên liên kết với UserId={UserId}. Action={Action}", userId, hanhDong);
                return false;
            }

            _dbContext.NhatKyHoatDongs.Add(new NhatKyHoatDong
            {
                MaNhanVien = maNhanVien.Value,
                HanhDong = hanhDong.Trim(),
                DoiTuong = doiTuong.Trim(),
                DuLieuCu = SerializeAuditData(duLieuCu),
                DuLieuMoi = SerializeAuditData(duLieuMoi),
                ThoiGian = DateTime.Now,
                Ip = ResolveRequestIp(),
                TrangThai = string.IsNullOrWhiteSpace(trangThai) ? "SUCCESS" : trangThai.Trim()
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch
        {
            _logger.LogWarning("Ghi structured log thất bại. UserId={UserId}, Action={Action}, Target={Target}", userId, hanhDong, doiTuong);
            return false;
        }
    }

    public async Task<bool> LogTaskActivityAsync(
        int maCongViec,
        int? maNhanVien,
        string hanhDong,
        string noiDung,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (maCongViec <= 0 || string.IsNullOrWhiteSpace(noiDung))
            {
                return false;
            }

            if (!maNhanVien.HasValue || maNhanVien.Value <= 0)
            {
                _logger.LogWarning("Bỏ qua ghi log task vì thiếu mã nhân viên hợp lệ. MaCongViec={MaCongViec}, Action={Action}", maCongViec, hanhDong);
                return false;
            }

            _dbContext.NhatKyHoatDongs.Add(new NhatKyHoatDong
            {
                MaNhanVien = maNhanVien.Value,
                HanhDong = string.IsNullOrWhiteSpace(hanhDong) ? "UPDATE" : hanhDong.Trim(),
                DoiTuong = $"CONGVIEC:{maCongViec}",
                DuLieuCu = null,
                DuLieuMoi = noiDung.Trim(),
                ThoiGian = DateTime.Now,
                Ip = ResolveRequestIp(),
                TrangThai = "SUCCESS"
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch
        {
            _logger.LogWarning("Ghi log task thất bại. MaCongViec={MaCongViec}, MaNhanVien={MaNhanVien}, Action={Action}", maCongViec, maNhanVien, hanhDong);
            return false;
        }
    }

    private async Task<int?> ResolveMaNhanVienFromUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        var maNhanVien = await _dbContext.NhanViens
                .AsNoTracking()
                .Where(x => x.AspNetUserId == userId)
                .Select(x => (int?)x.MaNhanVien)
                .FirstOrDefaultAsync(cancellationToken);

        if (maNhanVien.HasValue)
        {
            return maNhanVien;
        }

        var userLite = await _dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new { x.Email, x.UserName })
            .FirstOrDefaultAsync(cancellationToken);

        if (userLite == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(userLite.Email))
        {
            maNhanVien = await _dbContext.NhanViens
                .AsNoTracking()
                .Where(x => x.Email == userLite.Email)
                .Select(x => (int?)x.MaNhanVien)
                .FirstOrDefaultAsync(cancellationToken);
            if (maNhanVien.HasValue)
            {
                return maNhanVien;
            }
        }

        if (!string.IsNullOrWhiteSpace(userLite.UserName))
        {
            maNhanVien = await _dbContext.NhanViens
                .AsNoTracking()
                .Where(x => x.Email == userLite.UserName)
                .Select(x => (int?)x.MaNhanVien)
                .FirstOrDefaultAsync(cancellationToken);
            if (maNhanVien.HasValue)
            {
                return maNhanVien;
            }
        }

        return null;
    }

    private static string? SerializeAuditData(object? value)
    {
        if (value == null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Serialize(value, AuditJsonOptions);
        }
        catch
        {
            return value.ToString();
        }
    }

    private string? ResolveRequestIp()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return null;
        }

        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor.Split(',').FirstOrDefault()?.Trim();
        }

        return httpContext.Connection.RemoteIpAddress?.ToString();
    }
}
