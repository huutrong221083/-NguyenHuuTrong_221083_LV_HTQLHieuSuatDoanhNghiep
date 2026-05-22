using System.Text.Json;
using LuanVan.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LuanVan.Services;

public interface INotificationRuntimeSettingsProvider
{
    Task<NotificationRuntimeSettings> GetAsync(CancellationToken cancellationToken = default);
}

public sealed class NotificationRuntimeSettingsProvider : INotificationRuntimeSettingsProvider
{
    private const string CacheKey = "notify_runtime_settings_v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AppDbContext _dbContext;
    private readonly IMemoryCache _cache;

    public NotificationRuntimeSettingsProvider(AppDbContext dbContext, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<NotificationRuntimeSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out NotificationRuntimeSettings? cached) && cached is not null)
        {
            return cached;
        }

        var settings = await LoadInternalAsync(cancellationToken);
        _cache.Set(CacheKey, settings, TimeSpan.FromMinutes(2));
        return settings;
    }

    private async Task<NotificationRuntimeSettings> LoadInternalAsync(CancellationToken cancellationToken)
    {
        try
        {
            var raw = await _dbContext.Database
                .SqlQueryRaw<string>(
                    """
                    SELECT TOP(1) CAST(GIATRI AS NVARCHAR(MAX)) AS Value
                    FROM dbo.THAMSOAI
                    WHERE TEN_THAMSO LIKE N'ui\_%\_settingsx.notify.v1' ESCAPE N'\'
                    ORDER BY MATAMSO DESC
                    """)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(raw))
            {
                return NotificationRuntimeSettings.Default();
            }

            var parsed = JsonSerializer.Deserialize<NotificationRuntimeSettings>(raw, JsonOptions);
            return parsed?.MergeWithDefaults() ?? NotificationRuntimeSettings.Default();
        }
        catch
        {
            return NotificationRuntimeSettings.Default();
        }
    }
}

public sealed class NotificationRuntimeSettings
{
    public string Email { get; set; } = string.Empty;
    public bool OverdueTask { get; set; } = true;
    public bool System { get; set; } = true;

    public static NotificationRuntimeSettings Default() => new();

    public NotificationRuntimeSettings MergeWithDefaults()
    {
        var d = Default();
        return new NotificationRuntimeSettings
        {
            Email = string.IsNullOrWhiteSpace(Email) ? d.Email : Email.Trim(),
            OverdueTask = OverdueTask,
            System = System
        };
    }
}
