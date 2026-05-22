using System;

namespace LuanVan.Contracts;

public class BaoCaoDto
{
    public int MaBaoCao { get; set; }
    public string? TenBaoCao { get; set; }
    public string? LoaiBaoCao { get; set; }
    public string? TenDuAn { get; set; }
    public string? TenPhongBan { get; set; }
    public string? NguoiTao { get; set; }
    public string? NguoiNhanBaoCao { get; set; }
    public DateTime? NgayTao { get; set; }
    public DateTime? NgayBatDau { get; set; }
    public DateTime? NgayKetThuc { get; set; }
    public string? DefinDang { get; set; }
    public string? TrangThai { get; set; }
    public string? LoaiBaoCaoLabel { get; set; }
    public string? TrangThaiLabel { get; set; }
    public string? NoiDungPlaceholder { get; set; }
    public string? NoiDung { get; set; }
}

public class CreateBaoCaoRequest
{
    public string? TenBaoCao { get; set; }
    public string? LoaiBaoCao { get; set; }
    public int? MaDuAn { get; set; }
    public int? MaPhongBan { get; set; }
    public DateTime? NgayBatDau { get; set; }
    public DateTime? NgayKetThuc { get; set; }
    public string? DefinDang { get; set; } = "PDF";
}

public class UpdateBaoCaoRequest
{
    public int MaBaoCao { get; set; }
    public string? TenBaoCao { get; set; }
    public string? LoaiBaoCao { get; set; }
    public int? MaDuAn { get; set; }
    public int? MaPhongBan { get; set; }
    public DateTime? NgayBatDau { get; set; }
    public DateTime? NgayKetThuc { get; set; }
    public string? DefinDang { get; set; }
    public string? TrangThai { get; set; }
}

public class BaoCaoFilterRequest
{
    public DateTime? NgayBatDau { get; set; }
    public DateTime? NgayKetThuc { get; set; }
    public string? LoaiBaoCao { get; set; }
    public int? MaPhongBan { get; set; }
    public string? TenNhanVien { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
