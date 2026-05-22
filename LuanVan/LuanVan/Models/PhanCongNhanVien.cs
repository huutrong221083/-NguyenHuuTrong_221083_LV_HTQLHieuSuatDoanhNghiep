namespace LuanVan.Models;

public class PhanCongNhanVien
{
    public int MaPhaCong { get; set; }
    public int MaCongViec { get; set; }
    public int MaNhanVien { get; set; }
    public DateTime? NgayBatDauDuKien { get; set; }
    public DateTime? NgayKetThucdukien { get; set; }
    public DateTime? NgayBatDauThucTe { get; set; }
    public DateTime? NgayKetThucThucTe { get; set; }
    public decimal? PhanTramHoanThanh { get; set; }
    public int? TrangThai { get; set; }

    // Navigation properties
    public virtual CongViec CongViec { get; set; }
    public virtual NhanVien NhanVien { get; set; }
}
