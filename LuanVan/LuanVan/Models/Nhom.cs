using System;
using System.Collections.Generic;
using LuanVan.Data;

namespace LuanVan.Models;

public class Nhom
{
    public int MaNhom { get; set; }
    public string? TenNhom { get; set; }
    public DateTime? NgayTao { get; set; }
    public int? TruongNhom { get; set; }

    public NhanVien? NhanVienTruongNhom { get; set; }

    public ICollection<ThanhVienNhom> ThanhVienNhoms { get; set; } = new List<ThanhVienNhom>();
    public ICollection<KpiNhom> KpiNhoms { get; set; } = new List<KpiNhom>();
    public ICollection<DuAnNhom> DuAnNhoms { get; set; } = new List<DuAnNhom>();
    public ICollection<PhanCongNhom> PhanCongNhoms { get; set; } = new List<PhanCongNhom>();
}




