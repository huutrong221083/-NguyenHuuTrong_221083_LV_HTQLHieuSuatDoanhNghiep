using System;
using System.Collections.Generic;
using LuanVan.Data;

namespace LuanVan.Models;

public class DanhMucKpi
{
    public int MaKpi { get; set; }
    public int MaLoaiKpi { get; set; }
    public string? TenKpi { get; set; }
    public decimal? TrongSoGoc { get; set; }
    public int? TrangThai { get; set; }

    public LoaiKpi LoaiKpi { get; set; } = null!;
    public ICollection<KetQuaKpi> KetQuaKpis { get; set; } = new List<KetQuaKpi>();
    public ICollection<KpiNhanVien> KpiNhanViens { get; set; } = new List<KpiNhanVien>();
    public ICollection<KpiNhom> KpiNhoms { get; set; } = new List<KpiNhom>();
    public ICollection<KpiDuAn> KpiDuAns { get; set; } = new List<KpiDuAn>();
    public ICollection<KpiPhongBan> KpiPhongBans { get; set; } = new List<KpiPhongBan>();
}

