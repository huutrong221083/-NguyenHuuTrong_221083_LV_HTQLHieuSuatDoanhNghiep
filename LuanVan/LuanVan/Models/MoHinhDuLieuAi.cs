namespace LuanVan.Models;

public class MoHinhDuLieuAi
{
    public int MaModel { get; set; }
    public int MaDuLieu { get; set; }
    public string? MucDich { get; set; }
    public DateTime? NgaySuDung { get; set; }
    public decimal? MetricChinh { get; set; }

    public MoHinhAi MoHinhAi { get; set; } = null!;
    public DuLieuAi DuLieuAi { get; set; } = null!;
}
