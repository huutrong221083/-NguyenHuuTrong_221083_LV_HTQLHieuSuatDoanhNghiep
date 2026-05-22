namespace LuanVan.Models;

public class KpiDuAn
{
    public int MaKpi { get; set; }
    public int MaDuAn { get; set; }
    public decimal TrongSoApDung { get; set; }
    public DateTime? TuNgay { get; set; }
    public DateTime? DenNgay { get; set; }
    public DateTime? NgayKetThucApDung { get; set; }
    public bool IsActive { get; set; } = true;
    public byte? TrangThai { get; set; }
    public string? GhiChu { get; set; }

    public DanhMucKpi DanhMucKpi { get; set; } = null!;
    public DuAn DuAn { get; set; } = null!;
}
