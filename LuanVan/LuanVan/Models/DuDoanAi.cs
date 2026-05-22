using System;
using System.Collections.Generic;
using LuanVan.Data;

namespace LuanVan.Models;

public class DuDoanAi
{
    public int MaDuDoan { get; set; }
    public int MaNhanVien { get; set; }
    public int MaModel { get; set; }
    public int? thang { get; set; }
    public int? nam { get; set; }
    public string? ModelName { get; set; }
    public decimal? DiemDuDoan { get; set; }
    public decimal? XacSuatTreHan { get; set; }
    public string? InputData { get; set; }
    public string? OutputData { get; set; }
    public string? Actor { get; set; }
    public string? DeXuatCaiThien { get; set; }
    public string? GoiYNguonLuc { get; set; }
    public DateTime? ThoiGianDuDoan { get; set; }

    public NhanVien NhanVien { get; set; } = null!;
    public MoHinhAi MoHinhAi { get; set; } = null!;
}




