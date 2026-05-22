namespace LuanVan.Models;

public class NhatKyCongViec
{
    public int MaNhatKy { get; set; }
    public int MaCongViec { get; set; }
    public int MaNhanVien { get; set; }
    public decimal? PhanTramHoanThanh { get; set; }
    public DateTime? NgayCapNhat { get; set; }
    public string? GhiChu { get; set; }

    // Navigation properties
    public virtual CongViec CongViec { get; set; }
    public virtual NhanVien NhanVien { get; set; }
}
