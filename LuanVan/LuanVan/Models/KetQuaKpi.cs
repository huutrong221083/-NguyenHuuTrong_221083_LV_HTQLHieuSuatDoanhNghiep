using System;
using System.Collections.Generic;
using LuanVan.Data;

namespace LuanVan.Models;

public class KetQuaKpi
{
    public int MaKetQua { get; set; }
    public int MaNhanVien { get; set; }
    public int MaKpi { get; set; }
    public decimal? DiemSo { get; set; }
    public int? thang { get; set; }
    public int? nam { get; set; }

    public NhanVien NhanVien { get; set; } = null!;
    public DanhMucKpi DanhMucKpi { get; set; } = null!;
}



