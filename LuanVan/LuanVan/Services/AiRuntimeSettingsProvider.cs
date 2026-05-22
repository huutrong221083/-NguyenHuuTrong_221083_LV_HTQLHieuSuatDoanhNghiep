using System.Data.Common;
using System.Text.Json;
using LuanVan.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LuanVan.Services;

public interface IAiRuntimeSettingsProvider
{
    Task<AiRuntimeSettings> GetAsync(CancellationToken cancellationToken = default);
}

public sealed class AiRuntimeSettingsProvider : IAiRuntimeSettingsProvider
{
    private const string SettingsParamKey = "ui_ai_settings_json";
    private const string CacheKey = "ai_runtime_settings_v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AppDbContext _dbContext;
    private readonly IMemoryCache _cache;

    public AiRuntimeSettingsProvider(AppDbContext dbContext, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<AiRuntimeSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out AiRuntimeSettings? cached) && cached != null)
        {
            return cached;
        }

        var settings = await LoadInternalAsync(cancellationToken);
        _cache.Set(CacheKey, settings, TimeSpan.FromMinutes(2));
        return settings;
    }

    private async Task<AiRuntimeSettings> LoadInternalAsync(CancellationToken cancellationToken)
    {
        try
        {
            var connection = _dbContext.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            var modelId = await ResolveModelIdAsync(connection, cancellationToken);
            if (!modelId.HasValue)
            {
                return AiRuntimeSettings.Default();
            }

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
SELECT TOP(1) GIATRI
FROM dbo.THAMSOAI
WHERE MAMODEL = @modelId AND TEN_THAMSO = @paramKey
ORDER BY MATAMSO DESC";

            var modelParam = cmd.CreateParameter();
            modelParam.ParameterName = "@modelId";
            modelParam.Value = modelId.Value;
            cmd.Parameters.Add(modelParam);

            var keyParam = cmd.CreateParameter();
            keyParam.ParameterName = "@paramKey";
            keyParam.Value = SettingsParamKey;
            cmd.Parameters.Add(keyParam);

            var raw = await cmd.ExecuteScalarAsync(cancellationToken) as string;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return AiRuntimeSettings.Default();
            }

            var parsed = JsonSerializer.Deserialize<AiRuntimeSettings>(raw, JsonOptions);
            return parsed?.MergeWithDefaults() ?? AiRuntimeSettings.Default();
        }
        catch
        {
            return AiRuntimeSettings.Default();
        }
    }

    private static async Task<int?> ResolveModelIdAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT TOP(1) MAMODEL FROM dbo.MOHINHAI ORDER BY MAMODEL";
            var value = await cmd.ExecuteScalarAsync(cancellationToken);
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            return Convert.ToInt32(value);
        }
        catch
        {
            return null;
        }
    }
}

public sealed class AiRuntimeSettings
{
    public AiRuntimeGeneralSettings General { get; set; } = new();
    public AiRuntimeForecastSettings Forecast { get; set; } = new();
    public AiRuntimePerformanceSettings Performance { get; set; } = new();
    public AiRuntimeAutomationSettings Automation { get; set; } = new();

    public static AiRuntimeSettings Default() => new();

    public AiRuntimeSettings MergeWithDefaults()
    {
        var d = Default();
        return new AiRuntimeSettings
        {
            General = General ?? d.General,
            Forecast = Forecast ?? d.Forecast,
            Performance = Performance ?? d.Performance,
            Automation = Automation ?? d.Automation
        };
    }
}

public sealed class AiRuntimeGeneralSettings
{
    public bool Enabled { get; set; } = true;
    public int DataWindow { get; set; } = 6;
    public int MinRecords { get; set; } = 100;
    public string RunMode { get; set; } = "daily";
    public AiRuntimeWeightSettings Weights { get; set; } = new();
}

public sealed class AiRuntimeForecastSettings
{
    public int DelayThreshold { get; set; } = 70;
}

public sealed class AiRuntimePerformanceSettings
{
    public int Estimators { get; set; } = 100;
    public int MaxDepth { get; set; } = 10;
    public AiRuntimeWeightSettings Weights { get; set; } = new()
    {
        OnTime = 80,
        Volume = 60,
        Difficulty = 70,
        Kpi = 90,
        Feedback = 50
    };
}

public sealed class AiRuntimeWeightSettings
{
    public int Completion { get; set; } = 70;
    public int Overdue { get; set; } = 50;
    public int Kpi { get; set; } = 80;
    public int Difficulty { get; set; } = 60;

    public int OnTime { get; set; } = 80;
    public int Volume { get; set; } = 60;
    public int Feedback { get; set; } = 50;
}

public sealed class AiRuntimeAutomationSettings
{
    public bool SuggestPeople { get; set; } = true;
    public bool SuggestBySkillPerformance { get; set; } = true;
    public bool SuggestImprovePerformance { get; set; } = true;
    public bool SuggestRebalanceTask { get; set; } = true;
    public bool AutoLateAlert { get; set; } = true;
    public bool AutoSendNotification { get; set; } = true;
    public bool AutoSuggestDeadline { get; set; } = true;
    public string SuggestionTemplate { get; set; } = "Nhân viên {Ten} có nguy cơ trễ hạn do {LyDo}. Đề xuất: {GiaiPhap}";
}
