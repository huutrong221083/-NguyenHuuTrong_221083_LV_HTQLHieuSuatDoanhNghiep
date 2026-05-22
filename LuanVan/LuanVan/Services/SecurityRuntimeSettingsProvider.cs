using System.Text.Json;
using LuanVan.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LuanVan.Services;

public interface ISecurityRuntimeSettingsProvider
{
    Task<SecurityRuntimeSettings> GetAsync(CancellationToken cancellationToken = default);
}

public sealed class SecurityRuntimeSettingsProvider : ISecurityRuntimeSettingsProvider
{
    private const string CacheKey = "security_runtime_settings_v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AppDbContext _dbContext;
    private readonly IMemoryCache _cache;

    public SecurityRuntimeSettingsProvider(AppDbContext dbContext, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<SecurityRuntimeSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out SecurityRuntimeSettings? cached) && cached is not null)
        {
            return cached;
        }

        var settings = await LoadInternalAsync(cancellationToken);
        _cache.Set(CacheKey, settings, TimeSpan.FromMinutes(2));
        return settings;
    }

    private async Task<SecurityRuntimeSettings> LoadInternalAsync(CancellationToken cancellationToken)
    {
        try
        {
            var raw = await _dbContext.Database
                .SqlQueryRaw<string>(
                    """
                    SELECT TOP(1) CAST(GIATRI AS NVARCHAR(MAX)) AS Value
                    FROM dbo.THAMSOAI
                    WHERE TEN_THAMSO LIKE N'ui\_%\_settingsx.security.v1' ESCAPE N'\'
                    ORDER BY MATAMSO DESC
                    """)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(raw))
            {
                return SecurityRuntimeSettings.Default();
            }

            var parsed = JsonSerializer.Deserialize<SecurityRuntimeSettings>(raw, JsonOptions);
            return parsed?.MergeWithDefaults() ?? SecurityRuntimeSettings.Default();
        }
        catch
        {
            return SecurityRuntimeSettings.Default();
        }
    }
}

public sealed class SecurityRuntimeSettings
{
    public int MinLength { get; set; } = 8;
    public int MaxFailed { get; set; } = 5;
    public bool AutoLock { get; set; } = true;

    public static SecurityRuntimeSettings Default() => new();

    public SecurityRuntimeSettings MergeWithDefaults()
    {
        return new SecurityRuntimeSettings
        {
            MinLength = Math.Clamp(MinLength <= 0 ? 8 : MinLength, 6, 32),
            MaxFailed = Math.Clamp(MaxFailed <= 0 ? 5 : MaxFailed, 1, 20),
            AutoLock = AutoLock
        };
    }
}
