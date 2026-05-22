using System;
using System.Collections.Generic;
using LuanVan.Data;

namespace LuanVan.Models;

public class DuAn
{
    public int MaDuAn { get; set; }
    public string? TenDuAn { get; set; }
    public string? MoTa { get; set; }
    public DateTime? NgayBatDau { get; set; }
    public DateTime? NgayKetThuc { get; set; }
    public int? TrangThai { get; set; }

    public ICollection<KpiDuAn> KpiDuAns { get; set; } = new List<KpiDuAn>();
    public ICollection<CongViec> CongViecs { get; set; } = new List<CongViec>();
    public ICollection<DuAnNhanVien> DuAnNhanViens { get; set; } = new List<DuAnNhanVien>();
    public ICollection<DuAnNhom> DuAnNhoms { get; set; } = new List<DuAnNhom>();
    public ICollection<DuAnPhongBan> DuAnPhongBans { get; set; } = new List<DuAnPhongBan>();
}

