namespace LuanVan.Contracts;

public class DuAnListItemDto
{
    public int MaDuAn { get; set; }
    public string? TenDuAn { get; set; }
    public string? MoTa { get; set; }
    public DateTime? NgayBatDau { get; set; }
    public DateTime? NgayKetThuc { get; set; }
    public int? TrangThai { get; set; }
    public int TongCongViec { get; set; }
    public int CongViecHoanThanh { get; set; }
    public int CongViecTreHan { get; set; }
    public double PhanTramHoanThanh { get; set; }
}

public class DuAnDetailDto
{
    public int MaDuAn { get; set; }
    public string? TenDuAn { get; set; }
    public string? MoTa { get; set; }
    public DateTime? NgayBatDau { get; set; }
    public DateTime? NgayKetThuc { get; set; }
    public int? TrangThai { get; set; }
    public int TongCongViec { get; set; }
    public int CongViecHoanThanh { get; set; }
    public int CongViecTreHan { get; set; }
    public int SoNhanSu { get; set; }
    public int SoNhom { get; set; }
    public int SoPhongBan { get; set; }
    public int SoKpiLienKet { get; set; }
    public double PhanTramHoanThanh { get; set; }
}

public class DuAnNhanVienDto
{
    public int MaNhanVien { get; set; }
    public string? HoTen { get; set; }
    public string? VaiTro { get; set; }
    public DateTime? NgayThamGia { get; set; }
}

public class DuAnNhomDto
{
    public int MaNhom { get; set; }
    public string? TenNhom { get; set; }
    public DateTime? NgayThamGia { get; set; }
}

public class DuAnPhongBanDto
{
    public int MaPhongBan { get; set; }
    public string? TenPhongBan { get; set; }
    public DateTime? NgayThamGia { get; set; }
}
