namespace LuanVan.Models;

public class BaoCaoChiTiet
{
    public int MaBaoCaoChiTiet { get; set; }
    public int MaBaoCao { get; set; }
    public string? TieuDe { get; set; }
    public string? DuLieu { get; set; }
    public int? ThuTu { get; set; }

    public BaoCao BaoCao { get; set; } = null!;
}

