using System;
using LuanVan.Data;

namespace LuanVan.Models;

public class YeuCauBaoCao
{
    public int MaYeuCau { get; set; }
    public string? NguoiYeuCau { get; set; }
    public string? NguoiNhanYeuCau { get; set; }
    public string? TieuDe { get; set; }
    public string? MoTa { get; set; }
    public string? Priority { get; set; }
    public DateTime? HanChot { get; set; }
    public string? TrangThai { get; set; }
    public DateTime? NgayTao { get; set; }
    public DateTime? NgayCapNhat { get; set; }
    public bool IsDeleted { get; set; }

    public ApplicationUser? NguoiYeuCauNavigation { get; set; }
    public ApplicationUser? NguoiNhanYeuCauNavigation { get; set; }
}

