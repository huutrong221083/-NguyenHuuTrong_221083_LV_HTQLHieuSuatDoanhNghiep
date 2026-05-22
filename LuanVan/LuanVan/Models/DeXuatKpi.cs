namespace LuanVan.Models;

public class DeXuatKpi
{
    public int MaDeXuat { get; set; }
    public int? MaKpi { get; set; }
    public int? MaLoaiKpi { get; set; }
    public int NguoiDeXuat { get; set; }
    public int? NguoiDuyet { get; set; }
    public int? NguoiCapNhat { get; set; }
    public string LoaiDeXuat { get; set; } = "ApDungKPI";
    public int? MaNhanVienApDung { get; set; }
    public int? MaNhomApDung { get; set; }
    public int? MaPhongBanApDung { get; set; }
    public int? MaDuAnApDung { get; set; }
    public DateTime TuNgay { get; set; }
    public DateTime? DenNgay { get; set; }
    public decimal TrongSoDeXuat { get; set; }
    public string? TenKpiDeXuat { get; set; }
    public string? MoTaKpiDeXuat { get; set; }
    public string? LyDo { get; set; }
    public string TrangThai { get; set; } = "ChoDuyet";
    public string? PhanHoiAdmin { get; set; }
    public string? GhiChu { get; set; }
    public DateTime NgayTao { get; set; }
    public DateTime? NgayCapNhat { get; set; }
    public DateTime? NgayDuyet { get; set; }

    public DanhMucKpi? DanhMucKpi { get; set; }
    public LoaiKpi? LoaiKpi { get; set; }
    public NhanVien NhanVienDeXuat { get; set; } = null!;
    public NhanVien? NhanVienDuyet { get; set; }
    public NhanVien? NhanVienCapNhat { get; set; }
    public NhanVien? NhanVienApDung { get; set; }
    public Nhom? NhomApDung { get; set; }
    public PhongBan? PhongBanApDung { get; set; }
    public DuAn? DuAnApDung { get; set; }
}
