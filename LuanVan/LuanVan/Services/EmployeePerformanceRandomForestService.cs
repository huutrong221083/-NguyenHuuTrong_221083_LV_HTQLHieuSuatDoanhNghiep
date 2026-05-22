using LuanVan.Data;
using Microsoft.EntityFrameworkCore;

namespace LuanVan.Services;

public interface IEmployeePerformanceRandomForestService
{
    Task<PerformanceClassificationPrediction> PredictAsync(AiPerformanceModelInput input, string? correlationId = null, CancellationToken cancellationToken = default);
}

public sealed class PerformanceClassificationPrediction
{
    public string Label { get; set; } = "NORMAL";
    public double Confidence { get; set; }
    public string ModelName { get; set; } = "employee_performance_random_forest";
    public string ModelVersion { get; set; } = "v1";
    public string PredictionSource { get; set; } = "python_ai_service";
    public int InferenceTimeMs { get; set; }
    public string? CorrelationId { get; set; }
}

public sealed class EmployeePerformanceRandomForestService : IEmployeePerformanceRandomForestService
{
    private readonly AppDbContext _dbContext;
    private readonly IAiPythonClient _aiPythonClient;

    public EmployeePerformanceRandomForestService(AppDbContext dbContext, IAiPythonClient aiPythonClient)
    {
        _dbContext = dbContext;
        _aiPythonClient = aiPythonClient;
    }

    public async Task<PerformanceClassificationPrediction> PredictAsync(AiPerformanceModelInput input, string? correlationId = null, CancellationToken cancellationToken = default)
    {
        var trainingRows = await BuildTrainingSetAsync(cancellationToken);
        if (trainingRows.Count < 4)
        {
            throw new AiPythonBusinessException("INSUFFICIENT_TRAINING_DATA", "Khong du du lieu de huan luyen Performance model.");
        }

        if (trainingRows.Select(x => x.Label).Distinct(StringComparer.OrdinalIgnoreCase).Count() < 2)
        {
            throw new AiPythonBusinessException("INSUFFICIENT_CLASS_DIVERSITY", "Du lieu train can it nhat 2 nhan khac nhau.");
        }

        var response = await _aiPythonClient.PredictPerformanceAsync(new PyPerformancePredictRequest
        {
            CorrelationId = correlationId,
            TrainingRows = trainingRows,
            InputFeatures = new PyPerformanceInputFeatures
            {
                KpiScore = input.KpiTrungBinh,
                CompletionRate = input.SoCongViecHoanThanh / (double)Math.Max(1, input.SoCongViecHoanThanh + input.SoCongViecTreHan),
                LateRate = input.SoCongViecTreHan / (double)Math.Max(1, input.SoCongViecHoanThanh + input.SoCongViecTreHan),
                AvgProgress = Math.Max(0d, Math.Min(100d, input.KpiTrungBinh)),
                TaskCount = Math.Max(1d, input.SoCongViecHoanThanh + input.SoCongViecTreHan),
                ProjectCount = 1d
            }
        }, cancellationToken);

        return new PerformanceClassificationPrediction
        {
            Label = response.Label,
            Confidence = response.Confidence,
            ModelName = response.ModelName,
            ModelVersion = response.ModelVersion,
            PredictionSource = response.PredictionSource,
            InferenceTimeMs = response.InferenceTimeMs,
            CorrelationId = response.CorrelationId
        };
    }

    private async Task<List<PyPerformanceTrainingRow>> BuildTrainingSetAsync(CancellationToken cancellationToken)
    {
        var rows = await _dbContext.DuLieuAis
            .AsNoTracking()
            .Where(x => x.SoCongViecHoanThanh.HasValue && x.SoCongViecTreHan.HasValue && x.ThoiGianTrungBinh.HasValue && x.KpiTrungBinh.HasValue)
            .Select(x => new
            {
                Completed = x.SoCongViecHoanThanh!.Value,
                Late = x.SoCongViecTreHan!.Value,
                AvgTime = (double)x.ThoiGianTrungBinh!.Value,
                Kpi = (double)x.KpiTrungBinh!.Value
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x =>
        {
            var taskCount = Math.Max(1, x.Completed + x.Late);
            var completionRate = x.Completed / (double)taskCount;
            var lateRate = x.Late / (double)taskCount;
            var score = x.Kpi - (lateRate * 30d);

            return new PyPerformanceTrainingRow
            {
                KpiScore = x.Kpi,
                CompletionRate = Math.Round(completionRate, 4),
                LateRate = Math.Round(lateRate, 4),
                AvgProgress = Math.Max(0d, Math.Min(100d, x.Kpi)),
                TaskCount = taskCount,
                ProjectCount = 1d,
                Label = ToEnumLabel(score)
            };
        }).ToList();
    }

    private static string ToEnumLabel(double score)
    {
        if (score >= 85)
        {
            return "EXCELLENT";
        }

        if (score >= 70)
        {
            return "GOOD";
        }

        if (score >= 50)
        {
            return "NORMAL";
        }

        return "LOW";
    }
}
