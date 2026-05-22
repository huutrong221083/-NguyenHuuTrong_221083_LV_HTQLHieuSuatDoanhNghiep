namespace LuanVan.Models;

public class KetQuaKpiTong
{
    public int MaKetQuaTong { get; set; }
    public int MaNhanVien { get; set; }
    public int Thang { get; set; }
    public int Nam { get; set; }
    public decimal DiemTong { get; set; }
    public string? XepLoai { get; set; }
    public int SoKpiThanhPhan { get; set; }
    public DateTime NgayTinh { get; set; }

    public NhanVien NhanVien { get; set; } = null!;
}
