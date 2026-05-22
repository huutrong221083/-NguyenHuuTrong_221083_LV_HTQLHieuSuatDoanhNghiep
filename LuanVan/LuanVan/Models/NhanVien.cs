using System;
using System.Collections.Generic;
using LuanVan.Data;

namespace LuanVan.Models;

public class NhanVien
{
    public int MaNhanVien { get; set; }
    public int? MaPhongBan { get; set; }
    public int? PhoMaPhongBan { get; set; }
    public string? HoTen { get; set; }
    public DateTime? NgaySinh { get; set; }
    public string? Cccd { get; set; }
    public string? DiaChi { get; set; }
    public string? GioiTinh { get; set; }
    public string? Email { get; set; }
    public string? Sdt { get; set; }
    public DateTime? NgayVaoLam { get; set; }
    public int? TrangThai { get; set; }
    public int? MaChucVu { get; set; }

    public string? AspNetUserId { get; set; }
    public ApplicationUser? AspNetUser { get; set; }
    public ChucVu? ChucVu { get; set; }

    public PhongBan? PhongBanQuanLy { get; set; }
    public PhongBan? PhongBanPhoTrach { get; set; }

    public ICollection<DuDoanAi> DuDoanAis { get; set; } = new List<DuDoanAi>();
    public ICollection<DuLieuAi> DuLieuAis { get; set; } = new List<DuLieuAi>();
    public ICollection<KetQuaKpi> KetQuaKpis { get; set; } = new List<KetQuaKpi>();
    public ICollection<KetQuaKpiTong> KetQuaKpiTongs { get; set; } = new List<KetQuaKpiTong>();
    public ICollection<KpiNhanVien> KpiNhanViens { get; set; } = new List<KpiNhanVien>();
    public ICollection<KyNangNhanVien> KyNangNhanViens { get; set; } = new List<KyNangNhanVien>();
    public ICollection<NhatKyHoatDong> NhatKyHoatDongs { get; set; } = new List<NhatKyHoatDong>();
    public ICollection<NhatKyCongViec> NhatKyCongViecs { get; set; } = new List<NhatKyCongViec>();
    public ICollection<PhanCongNhanVien> PhanCongNhanViens { get; set; } = new List<PhanCongNhanVien>();
    public ICollection<ThanhVienNhom> ThanhVienNhoms { get; set; } = new List<ThanhVienNhom>();
    public ICollection<ThongBaoNhanVien> ThongBaoNhanViens { get; set; } = new List<ThongBaoNhanVien>();
    public ICollection<PhongBan> PhongBans { get; set; } = new List<PhongBan>();
    public ICollection<DuAnNhanVien> DuAnNhanViens { get; set; } = new List<DuAnNhanVien>();
    public ICollection<Nhom> NhomTruongNhoms { get; set; } = new List<Nhom>();
}



