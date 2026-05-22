using System;
using System.Collections.Generic;
using LuanVan.Data;

namespace LuanVan.Models;

public class LoaiKpi
{
    public int MaLoaiKpi { get; set; }
    public string? TenLoaiKpi { get; set; }
    public decimal HeSo { get; set; } = 1m;
    public bool IsActive { get; set; } = true;

    public ICollection<DanhMucKpi> DanhMucKpis { get; set; } = new List<DanhMucKpi>();
}

