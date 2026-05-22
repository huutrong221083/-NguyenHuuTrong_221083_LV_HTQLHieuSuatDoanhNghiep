namespace LuanVan.Models;

public class CongViec
{
    public int MaCongViec { get; set; }
    public int MaDuAn { get; set; }
    public int? MaCongViecCha { get; set; }
    public string? TenCongViec { get; set; }
    public string? MoTa { get; set; }
    public DateTime? NgayBatDau { get; set; }
    public DateTime? HanHoanThanh { get; set; }
    public int? MaTrangThai { get; set; }
    public int? MaDoUuTien { get; set; }
    public int? MaDoKho { get; set; }
    public decimal? DiemCongViec { get; set; }
    public decimal? PhanTramHoanThanh { get; set; }
    public DateTime? NgayTao { get; set; }
    public string? NguoiTao { get; set; }
    public DateTime? NgayCapNhat { get; set; }
    public string? NguoiCapNhat { get; set; }
    public bool? DaXoa { get; set; }

    // Navigation properties
    public virtual DuAn DuAn { get; set; }
    public virtual DoUuTien DoUuTien { get; set; }
    public virtual DoKho DoKho { get; set; }
    public virtual CongViec CongViecCha { get; set; }
    public virtual ICollection<CongViec> CongViecCon { get; set; } = new List<CongViec>();
    public virtual ICollection<PhanCongNhanVien> PhanCongNhanViens { get; set; } = new List<PhanCongNhanVien>();
    public virtual ICollection<PhanCongNhom> PhanCongNhoms { get; set; } = new List<PhanCongNhom>();
    public virtual ICollection<PhanCongPhongBan> PhanCongPhongBans { get; set; } = new List<PhanCongPhongBan>();
    public virtual ICollection<NhatKyCongViec> NhatKyCongViecs { get; set; } = new List<NhatKyCongViec>();
    public virtual ICollection<CongViecKyNang> CongViecKyNangs { get; set; } = new List<CongViecKyNang>();
    public virtual ICollection<TienDoCongViec> TienDoCongViecs { get; set; } = new List<TienDoCongViec>();
}
