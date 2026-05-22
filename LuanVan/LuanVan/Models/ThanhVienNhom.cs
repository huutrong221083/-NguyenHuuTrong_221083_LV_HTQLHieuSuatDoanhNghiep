using System;
using System.Collections.Generic;
using LuanVan.Data;

namespace LuanVan.Models;

public class ThanhVienNhom
{
    public int MaNhanVien { get; set; }
    public int MaNhom { get; set; }
    public DateTime? NgayGiaNhap { get; set; }
    public string? VaiTroTrongNhom { get; set; }

    public NhanVien NhanVien { get; set; } = null!;
    public Nhom Nhom { get; set; } = null!;
}




