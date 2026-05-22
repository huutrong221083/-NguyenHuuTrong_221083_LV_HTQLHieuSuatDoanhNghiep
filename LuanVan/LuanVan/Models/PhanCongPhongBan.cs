namespace LuanVan.Models;

public class PhanCongPhongBan
{
    public int MaPhongBan { get; set; }
    public int MaCongViec { get; set; }
    public DateTime? NgayPhanCong { get; set; }
    public int? TrangThai { get; set; }

    // Navigation properties
    public virtual CongViec CongViec { get; set; }
    public virtual PhongBan PhongBan { get; set; }
}
