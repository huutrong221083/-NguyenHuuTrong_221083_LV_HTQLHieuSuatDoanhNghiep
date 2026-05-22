using System;

namespace LuanVan.Models;

public class AiDanhGiaChiTiet
{
    public int MaDanhGiaChiTiet { get; set; }
    public int MaDanhGia { get; set; }
    public int? MaDuDoan { get; set; }
    public int? MaNhanVien { get; set; }
    public int? MaCongViec { get; set; }
    public int? MaDuAn { get; set; }
    public decimal? GiatriDuDoanSo { get; set; }
    public decimal? GiatriThucTeSo { get; set; }
    public string? NhanDuDoan { get; set; }
    public string? NhanThucTe { get; set; }
    public decimal? SoSaiLech { get; set; }
    public bool? DungNhan { get; set; }
    public bool? DungSo { get; set; }
    public decimal? DoTinCay { get; set; }
    public string? GhiChu { get; set; }
}
