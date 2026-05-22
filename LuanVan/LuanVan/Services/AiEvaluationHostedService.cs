using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace LuanVan.Services;

public class AiEvaluationHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AiEvaluationHostedService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(24);

    public AiEvaluationHostedService(IServiceScopeFactory scopeFactory, ILogger<AiEvaluationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AiEvaluationHostedService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var runtimeSettings = scope.ServiceProvider.GetRequiredService<IAiRuntimeSettingsProvider>();
                var runtime = await runtimeSettings.GetAsync(stoppingToken);

                if (!runtime.General.Enabled)
                {
                    _logger.LogInformation("AI runtime is disabled. Skip evaluation cycle.");
                }
                else
                {
                    var runMode = (runtime.General.RunMode ?? "daily").Trim().ToLowerInvariant();
                    if (runMode == "ondemand")
                    {
                        _logger.LogInformation("AI run mode is ondemand. Skip background evaluation cycle.");
                    }
                    else
                    {
                        var to = DateTime.UtcNow.Date;
                        var months = Math.Clamp(runtime.General.DataWindow, 1, 12);
                        var from = to.AddMonths(-months);
                        _logger.LogInformation("Running AI evaluation from {from} to {to} (runMode={runMode}, dataWindow={months} months).", from, to, runMode, months);

                        var evalService = scope.ServiceProvider.GetRequiredService<IAiEvaluationService>();
                        await evalService.RunEvaluationAsync(maModel: 0, loaiMoHinh: "Auto", tuNgay: from, denNgay: to, positiveLabel: "Yeu", cancellationToken: stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running AiEvaluationHostedService.");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("AiEvaluationHostedService stopping.");
    }
}
