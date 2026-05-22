using System;
using System.Collections.Generic;
using LuanVan.Data;

namespace LuanVan.Models;

public class DuLieuAi
{
    public int MaDuLieu { get; set; }
    public int MaNhanVien { get; set; }
    public int? SoCongViecHoanThanh { get; set; }
    public int? SoCongViecTreHan { get; set; }
    public decimal? ThoiGianTrungBinh { get; set; }
    public decimal? KpiTrungBinh { get; set; }

    public NhanVien NhanVien { get; set; } = null!;
    public ICollection<MoHinhDuLieuAi> MoHinhDuLieuAis { get; set; } = new List<MoHinhDuLieuAi>();
}

