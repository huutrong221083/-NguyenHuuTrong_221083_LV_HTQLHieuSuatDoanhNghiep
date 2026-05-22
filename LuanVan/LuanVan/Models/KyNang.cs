using System;
using System.Collections.Generic;
using LuanVan.Data;

namespace LuanVan.Models;

public class KyNang
{
    public int MaKyNang { get; set; }
    public string? TenKyNang { get; set; }
    public string? MoTa { get; set; }
    public int? TrangThai { get; set; }

    public ICollection<KyNangNhanVien> KyNangNhanViens { get; set; } = new List<KyNangNhanVien>();
    public ICollection<CongViecKyNang> CongViecKyNangs { get; set; } = new List<CongViecKyNang>();
}

