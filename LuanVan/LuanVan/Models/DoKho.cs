using System;
using System.Collections.Generic;
using LuanVan.Data;

namespace LuanVan.Models;

public class DoKho
{
    public int MaDoKho { get; set; }
    public string? TenDoKho { get; set; }
    public decimal HeSo { get; set; } = 1m;
    public bool IsActive { get; set; } = true;

    public ICollection<CongViec> CongViecs { get; set; } = new List<CongViec>();
}

