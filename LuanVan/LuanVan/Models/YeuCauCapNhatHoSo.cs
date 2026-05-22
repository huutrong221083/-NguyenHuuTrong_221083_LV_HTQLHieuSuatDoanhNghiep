using System;

namespace LuanVan.Models;

public class YeuCauCapNhatHoSo
{
    public int MaYeuCau { get; set; }
    public int MaNhanVien { get; set; }
    public string TrangThai { get; set; } = "ChoDuyet";
    public string? DanhSachTruong { get; set; }
    public string? DuLieuCuJson { get; set; }
    public string? DuLieuMoiJson { get; set; }
    public string? LyDoGui { get; set; }
    public string? LyDoTuChoi { get; set; }
    public string? GhiChuDuyet { get; set; }
    public int? NguoiTao { get; set; }
    public int? NguoiDuyet { get; set; }
    public int? NguoiCapNhat { get; set; }
    public DateTime? NgayTao { get; set; }
    public DateTime? NgayDuyet { get; set; }
    public DateTime? NgayCapNhat { get; set; }
    public string? IpTao { get; set; }
    public string? IpDuyet { get; set; }
    public bool IsDeleted { get; set; }

    public NhanVien? NhanVien { get; set; }
    public NhanVien? NhanVienDuyet { get; set; }
}
