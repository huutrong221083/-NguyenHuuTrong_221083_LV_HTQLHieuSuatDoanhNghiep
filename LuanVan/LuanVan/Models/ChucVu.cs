namespace LuanVan.Models;

public class ChucVu
{
    public int MaChucVu { get; set; }
    public string? TenChucVu { get; set; }

    public ICollection<NhanVien> NhanViens { get; set; } = new List<NhanVien>();
}
