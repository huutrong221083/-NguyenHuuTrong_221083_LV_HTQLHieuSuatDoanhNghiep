using System;
using System.Collections.Generic;
using LuanVan.Data;

namespace LuanVan.Models;

public class DoUuTien
{
    public int MaDoUuTien { get; set; }
    public string? TenDoUuTien { get; set; }
    public decimal HeSo { get; set; } = 1m;
    public bool IsActive { get; set; } = true;

    public ICollection<CongViec> CongViecs { get; set; } = new List<CongViec>();
}

