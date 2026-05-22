using System;
using System.Collections.Generic;
using LuanVan.Data;

namespace LuanVan.Models;

public class LoaiThongBao
{
    public int MaLoai { get; set; }
    public string? TenLoai { get; set; }

    public ICollection<ThongBao> ThongBaos { get; set; } = new List<ThongBao>();
}

