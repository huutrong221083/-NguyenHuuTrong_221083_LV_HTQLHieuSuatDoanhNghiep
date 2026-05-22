using System;
using System.Collections.Generic;
using LuanVan.Data;

namespace LuanVan.Models;

public class ThongBaoNhanVien
{
    public int MaNhanVien { get; set; }
    public int MaThongBao { get; set; }
    public bool? DaDoc { get; set; }

    public NhanVien NhanVien { get; set; } = null!;
    public ThongBao ThongBao { get; set; } = null!;
}

