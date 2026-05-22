namespace LuanVan.Models;

public class DuAnNhanVien
{
    public int MaDuAn { get; set; }
    public int MaNhanVien { get; set; }
    public string? VaiTro { get; set; }
    public DateTime? NgayThamGia { get; set; }
    public DateTime? NgayRoi { get; set; }
    public byte? TrangThai { get; set; }

    public DuAn DuAn { get; set; } = null!;
    public NhanVien NhanVien { get; set; } = null!;
}
