using System;
using System.Collections.Generic;
using LuanVan.Data;

namespace LuanVan.Models;

public class KyNangNhanVien
{
    public int MaKyNang { get; set; }
    public int MaNhanVien { get; set; }
    public int? CapDo { get; set; }
    public DateTime? NgayDatDuoc { get; set; }
    public int? SoDuAnDaDung { get; set; }

    public KyNang KyNang { get; set; } = null!;
    public NhanVien NhanVien { get; set; } = null!;
}
