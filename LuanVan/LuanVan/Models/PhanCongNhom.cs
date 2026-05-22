namespace LuanVan.Models;

public class PhanCongNhom
{
    public int MaCongViec { get; set; }
    public int MaNhom { get; set; }
    public DateTime? NgayGiao { get; set; }
    public int? TrangThai { get; set; }

    // Navigation properties
    public virtual CongViec CongViec { get; set; }
    public virtual Nhom Nhom { get; set; }
}
