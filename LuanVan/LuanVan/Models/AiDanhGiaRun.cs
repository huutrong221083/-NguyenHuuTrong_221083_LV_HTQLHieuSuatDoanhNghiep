using System;

namespace LuanVan.Models;

public class AiDanhGiaRun
{
    public int MaDanhGia { get; set; }
    public int MaModel { get; set; }
    public int? MaPhienBan { get; set; }
    public string LoaiMoHinh { get; set; } = string.Empty;
    public DateTime? TuNgay { get; set; }
    public DateTime? DenNgay { get; set; }
    public DateTime? NgayDanhGia { get; set; }
    public int TongBanGhi { get; set; }
    public int TongDung { get; set; }
    public int TongSai { get; set; }
    public decimal? Mae { get; set; }
    public decimal? Rmse { get; set; }
    public decimal? Accuracy { get; set; }
    public decimal? PrecisionScore { get; set; }
    public decimal? RecallScore { get; set; }
    public decimal? F1Score { get; set; }
    public string? GhiChu { get; set; }

    public MoHinhAi MoHinhAi { get; set; } = null!;
}