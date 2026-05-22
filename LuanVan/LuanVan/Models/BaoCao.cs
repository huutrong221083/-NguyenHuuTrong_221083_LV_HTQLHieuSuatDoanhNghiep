using System;
using System.Collections.Generic;
using LuanVan.Data;

namespace LuanVan.Models;

public class BaoCao
{
    public int MaBaoCao { get; set; }
    public string? TenBaoCao { get; set; }
    public string? LoaiBaoCao { get; set; }
    public int? MaDuAn { get; set; }
    public int? MaPhongBan { get; set; }
    public string? NguoiTao { get; set; }
    public DateTime? NgayTao { get; set; }
    public DateTime? NgayCapNhat { get; set; }
    public DateTime? NgayBatDau { get; set; }
    public DateTime? NgayKetThuc { get; set; }
    public string? DinhDang { get; set; }
    public string? TrangThai { get; set; }
    public string? NoiDung { get; set; }
    public bool IsDeleted { get; set; }

    public ApplicationUser? NguoiTaoNavigation { get; set; }
    public DuAn? DuAn { get; set; }
    public PhongBan? PhongBan { get; set; }
    public ICollection<BaoCaoChiTiet> BaoCaoChiTiets { get; set; } = new List<BaoCaoChiTiet>();
}

