using LuanVan.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

var appBasePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LuanVan"));
var config = new ConfigurationBuilder()
    .SetBasePath(appBasePath)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole());
services.AddMemoryCache();
services.AddSingleton<IConfiguration>(config);
services.AddHttpClient<IAiPythonClient, AiPythonClient>();
services.AddScoped<IAiRuntimeSettingsProvider, AiRuntimeSettingsProvider>();
services.AddScoped<IAiFeatureBuilderService, AiFeatureBuilderService>();
services.AddScoped<IAiDataValidationService, AiDataValidationService>();
services.AddScoped<ITaskDelayLinearRegressionService, TaskDelayLinearRegressionService>();
services.AddScoped<IEmployeePerformanceRandomForestService, EmployeePerformanceRandomForestService>();
services.AddScoped<IAiPredictionService, AiPredictionService>();
services.AddDbContextPool<LuanVan.Data.AppDbContext>((sp, options) =>
{
    var rawConnection = config.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string missing.");
    var sqlBuilder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(rawConnection)
    {
        InitialCatalog = "LV2026"
    };
    options.UseSqlServer(sqlBuilder.ConnectionString);
});

await using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var predictionService = scope.ServiceProvider.GetRequiredService<IAiPredictionService>();

var correlationPrefix = $"smoke-{DateTime.UtcNow:yyyyMMddHHmmss}";

var delayResult = await predictionService.PredictDelayAsync(new PredictDelayCommand
{
    MaNhanVien = null,
    DoKho = 4,
    DoUuTien = 3,
    SoNguoiThamGia = 2,
    TienDoHienTai = 55,
    SoNgayConLai = 2,
    EstimatedHours = 40,
    SpentHours = 28,
    CorrelationId = $"{correlationPrefix}-delay"
}, actorUserId: "smoke-runner");

Console.WriteLine("=== /ai/predict-delay (service-level) ===");
Console.WriteLine($"Success: {delayResult.Success}");
Console.WriteLine($"Message: {delayResult.Message}");
if (delayResult.Data != null)
{
    Console.WriteLine($"EstimatedDaysLate: {delayResult.Data.EstimatedDaysLate}");
    Console.WriteLine($"RiskLevel: {delayResult.Data.RiskLevel}");
    Console.WriteLine($"PredictionSource: {delayResult.Data.PredictionSource}");
    Console.WriteLine($"Model: {delayResult.Data.ModelName}:{delayResult.Data.ModelVersion}");
    Console.WriteLine($"CorrelationId: {delayResult.Data.CorrelationId}");
}

var perfResult = await predictionService.ClassifyPerformanceAsync(new ClassifyPerformanceCommand
{
    MaNhanVien = null,
    SoCongViecHoanThanh = 12,
    SoCongViecTreHan = 2,
    ThoiGianTrungBinh = 6.4,
    KpiTrungBinh = 81,
    CorrelationId = $"{correlationPrefix}-perf"
}, actorUserId: "smoke-runner");

Console.WriteLine("=== /ai/classify-performance (service-level) ===");
Console.WriteLine($"Success: {perfResult.Success}");
Console.WriteLine($"Message: {perfResult.Message}");
if (perfResult.Data != null)
{
    Console.WriteLine($"Label: {perfResult.Data.Label} ({perfResult.Data.LabelDisplay})");
    Console.WriteLine($"Confidence: {perfResult.Data.Confidence}");
    Console.WriteLine($"PredictionSource: {perfResult.Data.PredictionSource}");
    Console.WriteLine($"Model: {perfResult.Data.ModelName}:{perfResult.Data.ModelVersion}");
    Console.WriteLine($"CorrelationId: {perfResult.Data.CorrelationId}");
}
