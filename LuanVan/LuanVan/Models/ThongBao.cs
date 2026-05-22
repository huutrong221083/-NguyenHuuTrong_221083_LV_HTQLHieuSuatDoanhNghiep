using System;
using System.Collections.Generic;
using LuanVan.Data;

namespace LuanVan.Models;

public class ThongBao
{
    public int MaThongBao { get; set; }
    public int MaLoai { get; set; }
    public string? NoiDung { get; set; }
    public DateTime? ThoiGian { get; set; }

    public LoaiThongBao LoaiThongBao { get; set; } = null!;
    public ICollection<ThongBaoNhanVien> ThongBaoNhanViens { get; set; } = new List<ThongBaoNhanVien>();
}

