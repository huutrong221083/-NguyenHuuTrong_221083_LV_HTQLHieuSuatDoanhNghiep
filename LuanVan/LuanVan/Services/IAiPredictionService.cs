using LuanVan.Contracts;

namespace LuanVan.Services;

public interface IAiPredictionService
{
    Task<ApiResponse<PredictDelayResultDto>> PredictDelayAsync(PredictDelayCommand command, string? actorUserId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ClassifyPerformanceResultDto>> ClassifyPerformanceAsync(ClassifyPerformanceCommand command, string? actorUserId, CancellationToken cancellationToken = default);
}

public sealed class PredictDelayCommand
{
    public int? MaCongViec { get; set; }
    public int? MaNhanVien { get; set; }
    public double? DoKho { get; set; }
    public double? DoUuTien { get; set; }
    public int? SoNguoiThamGia { get; set; }
    public double? TienDoHienTai { get; set; }
    public int? SoNgayConLai { get; set; }
    public double? EstimatedHours { get; set; }
    public double? SpentHours { get; set; }
    public string? CorrelationId { get; set; }

    // Backward-compatible field name from existing API payload.
    public double? TienDo { get; set; }
}

public sealed class ClassifyPerformanceCommand
{
    public int? MaNhanVien { get; set; }
    public int? SoCongViecHoanThanh { get; set; }
    public int? SoCongViecTreHan { get; set; }
    public double? ThoiGianTrungBinh { get; set; }
    public double? KpiTrungBinh { get; set; }
    public string? CorrelationId { get; set; }
}

public sealed class PredictDelayResultDto
{
    public string? CorrelationId { get; set; }
    public double EstimatedDaysLate { get; set; }
    public string RiskLevel { get; set; } = "LOW";
    public double Percent { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ModelName { get; set; } = "task_delay_linear_regression";
    public string ModelVersion { get; set; } = "v1";
    public string PredictionSource { get; set; } = "python_ai_service";
    public int InferenceTimeMs { get; set; }
}

public sealed class ClassifyPerformanceResultDto
{
    public string? CorrelationId { get; set; }
    public string Label { get; set; } = "NORMAL";
    public string LabelDisplay { get; set; } = "Binh thuong";
    public double Confidence { get; set; }
    public string ModelName { get; set; } = "employee_performance_random_forest";
    public string ModelVersion { get; set; } = "v1";
    public string PredictionSource { get; set; } = "python_ai_service";
    public int InferenceTimeMs { get; set; }
}
