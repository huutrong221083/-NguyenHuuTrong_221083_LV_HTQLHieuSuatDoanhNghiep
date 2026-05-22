using System;
using System.Collections.Generic;
using LuanVan.Data;

namespace LuanVan.Models;

public class PhongBan
{
    public int MaPhongBan { get; set; }
    public string? TenPhongBan { get; set; }
    public string? MoTa { get; set; }
    public int? MaTruongPhong { get; set; }

    public NhanVien? TruongPhong { get; set; }
    public ICollection<NhanVien> NhanVienQuanLys { get; set; } = new List<NhanVien>();
    public ICollection<NhanVien> NhanVienPhoTrachs { get; set; } = new List<NhanVien>();
    public ICollection<KpiPhongBan> KpiPhongBans { get; set; } = new List<KpiPhongBan>();
    public ICollection<DuAnPhongBan> DuAnPhongBans { get; set; } = new List<DuAnPhongBan>();
    public ICollection<PhanCongPhongBan> PhanCongPhongBans { get; set; } = new List<PhanCongPhongBan>();
}

