using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace LuanVan.Services;

public sealed class AiPythonOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8000";
    public int TimeoutSeconds { get; set; } = 10;
    public bool EnableFallback { get; set; } = true;
    public string ApiKey { get; set; } = "dev-ai-service-key";
}

public sealed class AiPythonBusinessException : Exception
{
    public string ErrorCode { get; }

    public AiPythonBusinessException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }
}

public interface IAiPythonClient
{
    Task<PyTaskDelayPredictResponse> PredictTaskDelayAsync(PyTaskDelayPredictRequest request, CancellationToken cancellationToken = default);
    Task<PyPerformancePredictResponse> PredictPerformanceAsync(PyPerformancePredictRequest request, CancellationToken cancellationToken = default);
}

public sealed class AiPythonClient : IAiPythonClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };
    private readonly HttpClient _httpClient;
    private readonly AiPythonOptions _options;

    public AiPythonClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _options = configuration.GetSection("AiPython").Get<AiPythonOptions>() ?? new AiPythonOptions();

        _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 3, 120));
    }

    public Task<PyTaskDelayPredictResponse> PredictTaskDelayAsync(PyTaskDelayPredictRequest request, CancellationToken cancellationToken = default)
        => SendAsync<PyTaskDelayPredictRequest, PyTaskDelayPredictResponse>("predict/task-delay", request, cancellationToken);

    public Task<PyPerformancePredictResponse> PredictPerformanceAsync(PyPerformancePredictRequest request, CancellationToken cancellationToken = default)
        => SendAsync<PyPerformancePredictRequest, PyPerformancePredictResponse>("predict/performance", request, cancellationToken);

    private async Task<TResponse> SendAsync<TRequest, TResponse>(string path, TRequest payload, CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            httpRequest.Headers.Add("X-AI-Service-Key", _options.ApiKey);
        }

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
            if (body == null)
            {
                throw new InvalidOperationException("Python AI response body is empty.");
            }

            return body;
        }

        var error = await TryReadErrorAsync(response, cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest && error != null && !string.IsNullOrWhiteSpace(error.ErrorCode))
        {
            throw new AiPythonBusinessException(error.ErrorCode, error.Message ?? "Python AI business error.");
        }

        throw new InvalidOperationException($"Python AI request failed: {(int)response.StatusCode} {response.ReasonPhrase}. {error?.ErrorCode} {error?.Message}".Trim());
    }

    private static async Task<PyErrorResponse?> TryReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = doc.RootElement;
            if (root.TryGetProperty("error_code", out var errorCodeProp))
            {
                return new PyErrorResponse
                {
                    ErrorCode = errorCodeProp.GetString() ?? string.Empty,
                    Message = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? string.Empty : string.Empty
                };
            }

            if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.Object)
            {
                return new PyErrorResponse
                {
                    ErrorCode = detail.TryGetProperty("error_code", out var dCode) ? dCode.GetString() ?? string.Empty : string.Empty,
                    Message = detail.TryGetProperty("message", out var dMsg) ? dMsg.GetString() ?? string.Empty : string.Empty
                };
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}

public sealed class PyTaskDelayPredictRequest
{
    public string? CorrelationId { get; set; }
    public List<PyTaskDelayTrainingRow> TrainingRows { get; set; } = new();
    public required PyTaskDelayInputFeatures InputFeatures { get; set; }
}

public sealed class PyTaskDelayTrainingRow
{
    public double EstimatedHours { get; set; }
    public double SpentHours { get; set; }
    public double ProgressPercent { get; set; }
    public double PriorityScore { get; set; }
    public double DifficultyScore { get; set; }
    public double DaysUntilDeadline { get; set; }
    public double LateDays { get; set; }
}

public sealed class PyTaskDelayInputFeatures
{
    public double EstimatedHours { get; set; }
    public double SpentHours { get; set; }
    public double ProgressPercent { get; set; }
    public double PriorityScore { get; set; }
    public double DifficultyScore { get; set; }
    public double DaysUntilDeadline { get; set; }
}

public sealed class PyTaskDelayPredictResponse
{
    public string? CorrelationId { get; set; }
    public double EstimatedDaysLate { get; set; }
    public string RiskLevel { get; set; } = "LOW";
    public string ModelName { get; set; } = "task_delay_linear_regression";
    public string ModelVersion { get; set; } = "v1";
    public string PredictionSource { get; set; } = "python_ai_service";
    public int InferenceTimeMs { get; set; }
}

public sealed class PyPerformancePredictRequest
{
    public string? CorrelationId { get; set; }
    public List<PyPerformanceTrainingRow> TrainingRows { get; set; } = new();
    public required PyPerformanceInputFeatures InputFeatures { get; set; }
}

public sealed class PyPerformanceTrainingRow
{
    public double KpiScore { get; set; }
    public double CompletionRate { get; set; }
    public double LateRate { get; set; }
    public double AvgProgress { get; set; }
    public double TaskCount { get; set; }
    public double ProjectCount { get; set; }
    public string Label { get; set; } = "NORMAL";
}

public sealed class PyPerformanceInputFeatures
{
    public double KpiScore { get; set; }
    public double CompletionRate { get; set; }
    public double LateRate { get; set; }
    public double AvgProgress { get; set; }
    public double TaskCount { get; set; }
    public double ProjectCount { get; set; }
}

public sealed class PyPerformancePredictResponse
{
    public string? CorrelationId { get; set; }
    public string Label { get; set; } = "NORMAL";
    public double Confidence { get; set; }
    public string ModelName { get; set; } = "employee_performance_random_forest";
    public string ModelVersion { get; set; } = "v1";
    public string PredictionSource { get; set; } = "python_ai_service";
    public int InferenceTimeMs { get; set; }
}

public sealed class PyErrorResponse
{
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
