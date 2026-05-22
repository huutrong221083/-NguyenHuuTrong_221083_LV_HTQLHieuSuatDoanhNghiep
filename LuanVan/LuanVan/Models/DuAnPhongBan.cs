namespace LuanVan.Models;

public class DuAnPhongBan
{
    public int MaDuAn { get; set; }
    public int MaPhongBan { get; set; }
    public DateTime? NgayThamGia { get; set; }
    public byte? TrangThai { get; set; }

    public DuAn DuAn { get; set; } = null!;
    public PhongBan PhongBan { get; set; } = null!;
}
