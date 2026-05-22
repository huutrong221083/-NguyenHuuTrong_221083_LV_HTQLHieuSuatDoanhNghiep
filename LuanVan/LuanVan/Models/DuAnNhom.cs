namespace LuanVan.Models;

public class DuAnNhom
{
    public int MaDuAn { get; set; }
    public int MaNhom { get; set; }
    public DateTime? NgayThamGia { get; set; }
    public byte? TrangThai { get; set; }

    public DuAn DuAn { get; set; } = null!;
    public Nhom Nhom { get; set; } = null!;
}
