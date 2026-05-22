using System;
using System.Collections.Generic;
using LuanVan.Data;

namespace LuanVan.Models;

public class NhatKyHoatDong
{
    public int MaNhatKyHoatDong { get; set; }
    public int MaNhanVien { get; set; }
    public string? HanhDong { get; set; }
    public string? DoiTuong { get; set; }
    public string? DuLieuCu { get; set; }
    public string? DuLieuMoi { get; set; }
    public DateTime? ThoiGian { get; set; }
    public string? Ip { get; set; }
    public string? TrangThai { get; set; }

    public NhanVien NhanVien { get; set; } = null!;
}

