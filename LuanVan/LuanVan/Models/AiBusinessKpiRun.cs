using System;

namespace LuanVan.Models;

public class AiBusinessKpiRun
{
    public int MaBusinessKpi { get; set; }
    public int MaModel { get; set; }
    public int? MaPhienBan { get; set; }
    public string LoaiMoHinh { get; set; } = string.Empty;
    public DateTime? TuNgay { get; set; }
    public DateTime? DenNgay { get; set; }
    public DateTime NgayTao { get; set; }
    public int TongDuDoan { get; set; }
    public int TongTacDong { get; set; }
    public decimal? InterventionRate { get; set; }
    public decimal? UserAcceptanceRate { get; set; }
    public decimal? UtilityScore { get; set; }
    public string? GhiChu { get; set; }
}
