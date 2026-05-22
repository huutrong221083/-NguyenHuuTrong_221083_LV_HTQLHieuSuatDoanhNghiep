namespace LuanVan.Models;

public class KpiNhom
{
    public int MaKpi { get; set; }
    public int MaNhom { get; set; }
    public decimal TrongSoApDung { get; set; }
    public DateTime? TuNgay { get; set; }
    public DateTime? DenNgay { get; set; }
    public DateTime? NgayKetThucApDung { get; set; }
    public bool IsActive { get; set; } = true;
    public byte? TrangThai { get; set; }
    public string? GhiChu { get; set; }

    public DanhMucKpi DanhMucKpi { get; set; } = null!;
    public Nhom Nhom { get; set; } = null!;
}
