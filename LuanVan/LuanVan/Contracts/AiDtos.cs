using System;
using System.Collections.Generic;

namespace LuanVan.Contracts;

public sealed class AiMonitoringSnapshotDto
{
    public List<AiPredictionHistoryItemDto> Predictions { get; set; } = new();
    public List<AiEvaluationRunDto> Evaluations { get; set; } = new();
}

public sealed class AiPredictionHistoryItemDto
{
    public int MaDuDoan { get; set; }
    public int MaNhanVien { get; set; }
    public string? HoTenNhanVien { get; set; }
    public string? ModelName { get; set; }
    public double? DiemDuDoan { get; set; }
    public double? XacSuatTreHan { get; set; }
    public string? RiskLevel { get; set; }
    public string? DeXuatCaiThien { get; set; }
    public string? GoiYNguonLuc { get; set; }
    public DateTime? ThoiGianDuDoan { get; set; }
    public int FeedbackCount { get; set; }
    public double? AvgDoChinhXac { get; set; }
    public double? AvgMucHuuIch { get; set; }
    public string? LatestFeedback { get; set; }
    public DateTime? LatestFeedbackAt { get; set; }
}

public sealed class AiEvaluationRunDto
{
    public int MaDanhGia { get; set; }
    public int MaModel { get; set; }
    public string? TenModel { get; set; }
    public string? LoaiMoHinh { get; set; }
    public string? Version { get; set; }
    public DateTime? NgayDanhGia { get; set; }
    public int TongBanGhi { get; set; }
    public int TongDung { get; set; }
    public int TongSai { get; set; }
    public double? Mae { get; set; }
    public double? Rmse { get; set; }
    public double? Accuracy { get; set; }
    public double? PrecisionScore { get; set; }
    public double? RecallScore { get; set; }
    public double? F1Score { get; set; }
    public string? GhiChu { get; set; }
}

public sealed class AiFeedbackRequestDto
{
    public int? MaDuDoan { get; set; }
    public int? MaDanhGia { get; set; }
    public int? MaNhanVien { get; set; }
    public int? TaskId { get; set; }
    public bool? IsCorrect { get; set; }
    public string? WrongReason { get; set; }
    public string? Note { get; set; }
    public string? Context { get; set; }
    public int? DoChinhXac { get; set; }
    public int? MucHuuIch { get; set; }
    public bool? DungSai { get; set; }
    public string? NoiDung { get; set; }
    public string? HanhDongDeXuat { get; set; }
}

public sealed class AiFeedbackDto
{
    public int MaFeedback { get; set; }
    public int? MaDanhGia { get; set; }
    public int? MaDuDoan { get; set; }
    public int? MaNhanVien { get; set; }
    public int? DoChinhXac { get; set; }
    public int? MucHuuIch { get; set; }
    public bool? DungSai { get; set; }
    public string? NoiDung { get; set; }
    public string? HanhDongDeXuat { get; set; }
    public DateTime? NgayPhanHoi { get; set; }
}

public sealed class AiForecastFeedbackItemDto
{
    public int TaskId { get; set; }
    public int MaFeedback { get; set; }
    public bool? IsCorrect { get; set; }
    public string? WrongReason { get; set; }
    public string? Note { get; set; }
    public DateTime? NgayPhanHoi { get; set; }
}

public sealed class AiFeatureStoreItemDto
{
    public int MaFeature { get; set; }
    public int? MaModel { get; set; }
    public string? TenModel { get; set; }
    public int? MaNhanVien { get; set; }
    public string? HoTenNhanVien { get; set; }
    public int? MaDuAn { get; set; }
    public string? TenDuAn { get; set; }
    public int? MaCongViec { get; set; }
    public string? TenCongViec { get; set; }
    public string FeatureName { get; set; } = string.Empty;
    public string? FeatureValue { get; set; }
    public string? FeatureType { get; set; }
    public string? SourceTable { get; set; }
    public string? SourceKey { get; set; }
    public string? VersionTag { get; set; }
    public DateTime? DongChot { get; set; }
}

public sealed class AiInterventionLogRequestDto
{
    public int? MaDanhGia { get; set; }
    public int? MaDuDoan { get; set; }
    public int? MaNhanVien { get; set; }
    public int? NguoiCanThiep { get; set; }
    public string? ActionType { get; set; }
    public string? ActionSource { get; set; }
    public string? Reason { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? NguonCanThiep { get; set; }
}

public sealed class AiInterventionLogDto
{
    public int MaCanThiep { get; set; }
    public int? MaDanhGia { get; set; }
    public int? MaDuDoan { get; set; }
    public int? MaNhanVien { get; set; }
    public int? NguoiCanThiep { get; set; }
    public string? ActionType { get; set; }
    public string? ActionSource { get; set; }
    public string? Reason { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? NguonCanThiep { get; set; }
    public DateTime? NgayCanThiep { get; set; }
    public int? SoLanChinhSua { get; set; }
}

public sealed class AiTrainingDataResponseDto
{
    public DateTime GeneratedAt { get; set; }
    public AiTrainingDatasetDto? TaskDelay { get; set; }
    public AiTrainingDatasetDto? Performance { get; set; }
}

public sealed class AiTrainingDatasetDto
{
    public string DatasetKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string Algorithm { get; set; } = string.Empty;
    public List<string> SourceTables { get; set; } = new();
    public List<string> Features { get; set; } = new();
    public string Target { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public AiTrainingDataQualityDto DataQuality { get; set; } = new();
    public List<AiTrainingRowDto> Rows { get; set; } = new();
}

public sealed class AiTrainingDataQualityDto
{
    public bool IsLowData { get; set; }
    public int MinRequiredRows { get; set; }
    public int ActualRows { get; set; }
    public string WarningMessage { get; set; } = string.Empty;
}

public sealed class AiTrainingRowDto
{
    public string RowType { get; set; } = string.Empty;
    public Dictionary<string, object?> Values { get; set; } = new();
}
