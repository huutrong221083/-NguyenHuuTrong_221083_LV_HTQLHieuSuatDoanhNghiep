using System;

namespace LuanVan.Models;

public class AiFeatureStore
{
    public int MaFeature { get; set; }
    public int? MaModel { get; set; }
    public int? MaNhanVien { get; set; }
    public int? MaCongViec { get; set; }
    public int? MaDuAn { get; set; }
    public string FeatureName { get; set; } = string.Empty;
    public string? FeatureValue { get; set; }
    public string? FeatureType { get; set; }
    public string? SourceTable { get; set; }
    public string? SourceKey { get; set; }
    public string? VersionTag { get; set; }
    public DateTime? DongChot { get; set; }
}