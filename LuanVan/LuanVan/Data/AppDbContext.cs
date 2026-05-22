using LuanVan.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LuanVan.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ChucVu> ChucVus => Set<ChucVu>();
    public DbSet<DanhMucKpi> DanhMucKpis => Set<DanhMucKpi>();
    public DbSet<DoKho> DoKhos => Set<DoKho>();
    public DbSet<DoUuTien> DoUuTiens => Set<DoUuTien>();
    public DbSet<DuAn> DuAns => Set<DuAn>();
    public DbSet<DuAnNhanVien> DuAnNhanViens => Set<DuAnNhanVien>();
    public DbSet<DuAnNhom> DuAnNhoms => Set<DuAnNhom>();
    public DbSet<DuAnPhongBan> DuAnPhongBans => Set<DuAnPhongBan>();
    public DbSet<AiDanhGiaRun> AiDanhGiaRuns => Set<AiDanhGiaRun>();
    public DbSet<AiFeedback> AiFeedbacks => Set<AiFeedback>();
    public DbSet<AiDanhGiaChiTiet> AiDanhGiaChiTiets => Set<AiDanhGiaChiTiet>();
    public DbSet<AiBusinessKpiRun> AiBusinessKpiRuns => Set<AiBusinessKpiRun>();
    public DbSet<DuDoanAi> DuDoanAis => Set<DuDoanAi>();
    public DbSet<DuLieuAi> DuLieuAis => Set<DuLieuAi>();
    public DbSet<AiFeatureStore> AiFeatureStores => Set<AiFeatureStore>();
    public DbSet<AiNhatKyCanThiep> AiNhatKyCanThieps => Set<AiNhatKyCanThiep>();
    public DbSet<KetQuaKpi> KetQuaKpis => Set<KetQuaKpi>();
    public DbSet<KetQuaKpiTong> KetQuaKpiTongs => Set<KetQuaKpiTong>();
    public DbSet<KpiDuAn> KpiDuAns => Set<KpiDuAn>();
    public DbSet<DeXuatKpi> DeXuatKpis => Set<DeXuatKpi>();
    public DbSet<KpiNhanVien> KpiNhanViens => Set<KpiNhanVien>();
    public DbSet<KpiNhom> KpiNhoms => Set<KpiNhom>();
    public DbSet<KpiPhongBan> KpiPhongBans => Set<KpiPhongBan>();
    public DbSet<KyNang> KyNangs => Set<KyNang>();
    public DbSet<KyNangNhanVien> KyNangNhanViens => Set<KyNangNhanVien>();
    public DbSet<KpiXepLoai> KpiXepLoais => Set<KpiXepLoai>();
    public DbSet<LoaiKpi> LoaiKpis => Set<LoaiKpi>();
    public DbSet<LoaiThongBao> LoaiThongBaos => Set<LoaiThongBao>();
    public DbSet<MoHinhAi> MoHinhAis => Set<MoHinhAi>();
    public DbSet<MoHinhDuLieuAi> MoHinhDuLieuAis => Set<MoHinhDuLieuAi>();
    public DbSet<NhanVien> NhanViens => Set<NhanVien>();
    public DbSet<NhatKyHoatDong> NhatKyHoatDongs => Set<NhatKyHoatDong>();
    public DbSet<Nhom> Nhoms => Set<Nhom>();
    public DbSet<PhongBan> PhongBans => Set<PhongBan>();
    public DbSet<ThanhVienNhom> ThanhVienNhoms => Set<ThanhVienNhom>();
    public DbSet<ThongBao> ThongBaos => Set<ThongBao>();
    public DbSet<ThongBaoNhanVien> ThongBaoNhanViens => Set<ThongBaoNhanVien>();
    public DbSet<CongViec> CongViecs => Set<CongViec>();
    public DbSet<PhanCongNhanVien> PhanCongNhanViens => Set<PhanCongNhanVien>();
    public DbSet<PhanCongNhom> PhanCongNhoms => Set<PhanCongNhom>();
    public DbSet<PhanCongPhongBan> PhanCongPhongBans => Set<PhanCongPhongBan>();
    public DbSet<NhatKyCongViec> NhatKyCongViecs => Set<NhatKyCongViec>();
    public DbSet<CongViecKyNang> CongViecKyNangs => Set<CongViecKyNang>();
    public DbSet<TienDoCongViec> TienDoCongViecs => Set<TienDoCongViec>();
    public DbSet<BaoCao> BaoCaos => Set<BaoCao>();
    public DbSet<BaoCaoChiTiet> BaoCaoChiTiets => Set<BaoCaoChiTiet>();
    public DbSet<YeuCauBaoCao> YeuCauBaoCaos => Set<YeuCauBaoCao>();
    public DbSet<YeuCauCapNhatHoSo> YeuCauCapNhatHoSos => Set<YeuCauCapNhatHoSo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureIdentity(modelBuilder);

        modelBuilder.Entity<BaoCao>(entity =>
        {
            entity.ToTable("BAOCAO_PORTAL");
            entity.HasKey(e => e.MaBaoCao).HasName("PK_BAOCAO_PORTAL");
            entity.Property(e => e.MaBaoCao).HasColumnName("MABAOCAO");
            entity.Property(e => e.TenBaoCao).HasColumnName("TENBAOCAO").HasMaxLength(200).IsUnicode();
            entity.Property(e => e.LoaiBaoCao).HasColumnName("LOAIBAOCAO").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.MaDuAn).HasColumnName("MADUAN");
            entity.Property(e => e.MaPhongBan).HasColumnName("MAPHONGBAN");
            entity.Property(e => e.NguoiTao).HasColumnName("NGUOITAO").HasMaxLength(128);
            entity.Property(e => e.NgayTao).HasColumnName("NGAYTAO").HasColumnType("datetime2(0)");
            entity.Property(e => e.NgayCapNhat).HasColumnName("NGAYCAPNHAT").HasColumnType("datetime2(0)");
            entity.Property(e => e.NgayBatDau).HasColumnName("NGAYBATDAU").HasColumnType("datetime2(0)");
            entity.Property(e => e.NgayKetThuc).HasColumnName("NGAYKETTHUC").HasColumnType("datetime2(0)");
            entity.Property(e => e.DinhDang).HasColumnName("DINH_DANG").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.TrangThai).HasColumnName("TRANGTHAI").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.NoiDung).HasColumnName("NOIDUNG").IsUnicode();
            entity.Property(e => e.IsDeleted).HasColumnName("ISDELETED");

            entity.HasOne(e => e.NguoiTaoNavigation).WithMany().HasForeignKey(e => e.NguoiTao).HasConstraintName("FK_BAOCAO_NGUOITAO");
            entity.HasOne(e => e.DuAn).WithMany().HasForeignKey(e => e.MaDuAn).HasConstraintName("FK_BAOCAO_DUAN");
            entity.HasOne(e => e.PhongBan).WithMany().HasForeignKey(e => e.MaPhongBan).HasConstraintName("FK_BAOCAO_PHONGBAN");
        });

        modelBuilder.Entity<BaoCaoChiTiet>(entity =>
        {
            entity.ToTable("BAOCAOCHITIET_PORTAL");
            entity.HasKey(e => e.MaBaoCaoChiTiet).HasName("PK_BAOCAOCHITIET_PORTAL");
            entity.Property(e => e.MaBaoCaoChiTiet).HasColumnName("MABAOCAOCHITIET");
            entity.Property(e => e.MaBaoCao).HasColumnName("MABAOCAO");
            entity.Property(e => e.TieuDe).HasColumnName("TIEUDE").HasMaxLength(200).IsUnicode();
            entity.Property(e => e.DuLieu).HasColumnName("DULIEU").IsUnicode();
            entity.Property(e => e.ThuTu).HasColumnName("THUTUU");

            entity.HasOne(e => e.BaoCao).WithMany(e => e.BaoCaoChiTiets).HasForeignKey(e => e.MaBaoCao).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_BAOCAOCHITIET_PORTAL_BAOCAO_PORTAL");
        });

        modelBuilder.Entity<YeuCauBaoCao>(entity =>
        {
            entity.ToTable("YEUCAUBAOCAO");
            entity.HasKey(e => e.MaYeuCau).HasName("PK_YEUCAUBAOCAO");
            entity.Property(e => e.MaYeuCau).HasColumnName("MAYEUCAU");
            entity.Property(e => e.NguoiYeuCau).HasColumnName("NGUOIYEUCAU").HasMaxLength(128);
            entity.Property(e => e.NguoiNhanYeuCau).HasColumnName("NGUOINHAN").HasMaxLength(128);
            entity.Property(e => e.TieuDe).HasColumnName("TIEUDE").HasMaxLength(200).IsUnicode();
            entity.Property(e => e.MoTa).HasColumnName("MOTA").IsUnicode();
            entity.Property(e => e.Priority).HasColumnName("PRIORITY").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.HanChot).HasColumnName("HANCHOT");
            entity.Property(e => e.TrangThai).HasColumnName("TRANGTHAI").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.NgayTao).HasColumnName("NGAYTAO");
            entity.Property(e => e.NgayCapNhat).HasColumnName("NGAYCAPNHAT");
            entity.Property(e => e.IsDeleted).HasColumnName("ISDELETED");

            entity.HasOne(e => e.NguoiYeuCauNavigation).WithMany().HasForeignKey(e => e.NguoiYeuCau).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_YEUCAU_MANAGER");
            entity.HasOne(e => e.NguoiNhanYeuCauNavigation).WithMany().HasForeignKey(e => e.NguoiNhanYeuCau).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_YEUCAU_EMPLOYEE");
        });

        modelBuilder.Entity<YeuCauCapNhatHoSo>(entity =>
        {
            entity.ToTable("YEUCAU_CAPNHAT_HOSO");
            entity.HasKey(e => e.MaYeuCau).HasName("PK_YEUCAU_CAPNHAT_HOSO");
            entity.Property(e => e.MaYeuCau).HasColumnName("MAYEUCAU");
            entity.Property(e => e.MaNhanVien).HasColumnName("MANHANVIEN");
            entity.Property(e => e.TrangThai).HasColumnName("TRANGTHAI").HasMaxLength(30).IsUnicode(false).HasDefaultValue("ChoDuyet");
            entity.Property(e => e.DanhSachTruong).HasColumnName("DANHSACH_TRUONG").HasMaxLength(200).IsUnicode(false);
            entity.Property(e => e.DuLieuCuJson).HasColumnName("DULIEU_CU_JSON").HasColumnType("nvarchar(max)").IsUnicode();
            entity.Property(e => e.DuLieuMoiJson).HasColumnName("DULIEU_MOI_JSON").HasColumnType("nvarchar(max)").IsUnicode();
            entity.Property(e => e.LyDoGui).HasColumnName("LYDO_GUI").HasMaxLength(500).IsUnicode();
            entity.Property(e => e.LyDoTuChoi).HasColumnName("LYDO_TUCHOI").HasMaxLength(500).IsUnicode();
            entity.Property(e => e.GhiChuDuyet).HasColumnName("GHICHU_DUYET").HasMaxLength(500).IsUnicode();
            entity.Property(e => e.NguoiTao).HasColumnName("NGUOITAO");
            entity.Property(e => e.NguoiDuyet).HasColumnName("NGUOIDUYET");
            entity.Property(e => e.NguoiCapNhat).HasColumnName("NGUOICAPNHAT");
            entity.Property(e => e.NgayTao).HasColumnName("NGAYTAO").HasColumnType("datetime2(0)");
            entity.Property(e => e.NgayDuyet).HasColumnName("NGAYDUYET").HasColumnType("datetime2(0)");
            entity.Property(e => e.NgayCapNhat).HasColumnName("NGAYCAPNHAT").HasColumnType("datetime2(0)");
            entity.Property(e => e.IpTao).HasColumnName("IP_TAO").HasMaxLength(64).IsUnicode(false);
            entity.Property(e => e.IpDuyet).HasColumnName("IP_DUYET").HasMaxLength(64).IsUnicode(false);
            entity.Property(e => e.IsDeleted).HasColumnName("ISDELETED").HasDefaultValue(false);

            entity.HasIndex(e => e.MaNhanVien).HasDatabaseName("IX_YEUCAU_CAPNHAT_HOSO_MANHANVIEN");
            entity.HasIndex(e => e.TrangThai).HasDatabaseName("IX_YEUCAU_CAPNHAT_HOSO_TRANGTHAI");
            entity.HasIndex(e => e.NgayTao).HasDatabaseName("IX_YEUCAU_CAPNHAT_HOSO_NGAYTAO");

            entity.HasOne(e => e.NhanVien).WithMany().HasForeignKey(e => e.MaNhanVien).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_YEUCAU_CAPNHAT_HOSO_NHANVIEN");
            entity.HasOne(e => e.NhanVienDuyet).WithMany().HasForeignKey(e => e.NguoiDuyet).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_YEUCAU_CAPNHAT_HOSO_NGUOIDUYET");
        });

        modelBuilder.Entity<ChucVu>(entity =>
        {
            entity.ToTable("CHUCVU");
            entity.HasKey(e => e.MaChucVu).HasName("PK_CHUCVU");
            entity.Property(e => e.MaChucVu).HasColumnName("MACHUCVU");
            entity.Property(e => e.TenChucVu).HasColumnName("TENCHUCVU").HasMaxLength(50).IsUnicode();
        });

        modelBuilder.Entity<DanhMucKpi>(entity =>
        {
            entity.ToTable("DANHMUCKPI");
            entity.HasKey(e => e.MaKpi).HasName("PK_DANHMUCKPI");
            entity.Property(e => e.MaKpi).HasColumnName("MAKPI");
            entity.Property(e => e.MaLoaiKpi).HasColumnName("MALOAIKPI");
            entity.Property(e => e.TenKpi).HasColumnName("TENKPI").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.TrongSoGoc).HasColumnName("TRONGSOGOC").HasColumnType("decimal(5,2)");
            entity.Property(e => e.TrangThai).HasColumnName("TRANGTHAI");
            entity.HasOne(e => e.LoaiKpi).WithMany(e => e.DanhMucKpis).HasForeignKey(e => e.MaLoaiKpi).HasConstraintName("FK_DANHMUCKPI_LOAIKPI");
        });

        modelBuilder.Entity<DoKho>(entity =>
        {
            entity.ToTable("DOKHO");
            entity.HasKey(e => e.MaDoKho).HasName("PK_DOKHO");
            entity.Property(e => e.MaDoKho).HasColumnName("MADOKHO");
            entity.Property(e => e.TenDoKho).HasColumnName("TENDOKHO").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.HeSo).HasColumnName("HESO").HasColumnType("decimal(5,2)").HasDefaultValue(1m);
            entity.Property(e => e.IsActive).HasColumnName("ISACTIVE").HasDefaultValue(true);
        });

        modelBuilder.Entity<DoUuTien>(entity =>
        {
            entity.ToTable("DOUUTIEN");
            entity.HasKey(e => e.MaDoUuTien).HasName("PK_DOUUTIEN");
            entity.Property(e => e.MaDoUuTien).HasColumnName("MADOUUTIEN");
            entity.Property(e => e.TenDoUuTien).HasColumnName("TENDOUUTIEN").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.HeSo).HasColumnName("HESO").HasColumnType("decimal(5,2)").HasDefaultValue(1m);
            entity.Property(e => e.IsActive).HasColumnName("ISACTIVE").HasDefaultValue(true);
        });

        modelBuilder.Entity<DuAn>(entity =>
        {
            entity.ToTable("DUAN");
            entity.HasKey(e => e.MaDuAn).HasName("PK_DUAN");
            entity.Property(e => e.MaDuAn).HasColumnName("MADUAN");
            entity.Property(e => e.TenDuAn).HasColumnName("TENDUAN").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.MoTa).HasColumnName("MOTA").HasMaxLength(300).IsUnicode();
            entity.Property(e => e.NgayBatDau).HasColumnName("NGAYBATDAU").HasColumnType("datetime2(0)");
            entity.Property(e => e.NgayKetThuc).HasColumnName("NGAYKETTHUC").HasColumnType("datetime2(0)");
            entity.Property(e => e.TrangThai).HasColumnName("TRANGTHAI");
        });

        modelBuilder.Entity<DuAnNhanVien>(entity =>
        {
            entity.ToTable("DUAN_NHANVIEN");
            entity.HasKey(e => new { e.MaDuAn, e.MaNhanVien }).HasName("PK_DUAN_NHANVIEN");
            entity.Property(e => e.MaDuAn).HasColumnName("MADUAN");
            entity.Property(e => e.MaNhanVien).HasColumnName("MANHANVIEN");
            entity.Property(e => e.VaiTro).HasColumnName("VAITRO").HasMaxLength(100).IsUnicode();
            entity.Property(e => e.NgayThamGia).HasColumnName("NGAYTHAMGIA").HasColumnType("datetime2(0)");
            entity.Property(e => e.NgayRoi).HasColumnName("NGAYROI").HasColumnType("datetime2(0)");
            entity.Property(e => e.TrangThai).HasColumnName("TRANGTHAI");
            entity.HasOne(e => e.DuAn).WithMany(e => e.DuAnNhanViens).HasForeignKey(e => e.MaDuAn).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_DUAN_NHANVIEN_DUAN");
            entity.HasOne(e => e.NhanVien).WithMany(e => e.DuAnNhanViens).HasForeignKey(e => e.MaNhanVien).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_DUAN_NHANVIEN_NHANVIEN");
        });

        modelBuilder.Entity<DuAnNhom>(entity =>
        {
            entity.ToTable("DUAN_NHOM");
            entity.HasKey(e => new { e.MaDuAn, e.MaNhom }).HasName("PK_DUAN_NHOM");
            entity.Property(e => e.MaDuAn).HasColumnName("MADUAN");
            entity.Property(e => e.MaNhom).HasColumnName("MANHOM");
            entity.Property(e => e.NgayThamGia).HasColumnName("NGAYTHAMGIA").HasColumnType("datetime2(0)");
            entity.Property(e => e.TrangThai).HasColumnName("TRANGTHAI");
            entity.HasOne(e => e.DuAn).WithMany(e => e.DuAnNhoms).HasForeignKey(e => e.MaDuAn).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_DUAN_NHOM_DUAN");
            entity.HasOne(e => e.Nhom).WithMany(e => e.DuAnNhoms).HasForeignKey(e => e.MaNhom).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_DUAN_NHOM_NHOM");
        });

        modelBuilder.Entity<DuAnPhongBan>(entity =>
        {
            entity.ToTable("DUAN_PHONGBAN");
            entity.HasKey(e => new { e.MaDuAn, e.MaPhongBan }).HasName("PK_DUAN_PHONGBAN");
            entity.Property(e => e.MaDuAn).HasColumnName("MADUAN");
            entity.Property(e => e.MaPhongBan).HasColumnName("MAPHONGBAN");
            entity.Property(e => e.NgayThamGia).HasColumnName("NGAYTHAMGIA").HasColumnType("datetime2(0)");
            entity.Property(e => e.TrangThai).HasColumnName("TRANGTHAI");
            entity.HasOne(e => e.DuAn).WithMany(e => e.DuAnPhongBans).HasForeignKey(e => e.MaDuAn).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_DUAN_PHONGBAN_DUAN");
            entity.HasOne(e => e.PhongBan).WithMany(e => e.DuAnPhongBans).HasForeignKey(e => e.MaPhongBan).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_DUAN_PHONGBAN_PHONGBAN");
        });

        modelBuilder.Entity<AiDanhGiaRun>(entity =>
        {
            entity.ToTable("AI_DANHGIA_RUN");
            entity.HasKey(e => e.MaDanhGia).HasName("PK_AI_DANHGIA_RUN");
            entity.Property(e => e.MaDanhGia).HasColumnName("MADANHGIA");
            entity.Property(e => e.MaModel).HasColumnName("MAMODEL");
            entity.Property(e => e.MaPhienBan).HasColumnName("MAPHIENBAN");
            entity.Property(e => e.LoaiMoHinh).HasColumnName("LOAI_MO_HINH").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.TuNgay).HasColumnName("TU_NGAY").HasColumnType("date");
            entity.Property(e => e.DenNgay).HasColumnName("DEN_NGAY").HasColumnType("date");
            entity.Property(e => e.NgayDanhGia).HasColumnName("NGAY_DANHGIA").HasColumnType("datetime2(0)");
            entity.Property(e => e.TongBanGhi).HasColumnName("TONG_BAN_GHI");
            entity.Property(e => e.TongDung).HasColumnName("TONG_DUNG");
            entity.Property(e => e.TongSai).HasColumnName("TONG_SAI");
            entity.Property(e => e.Mae).HasColumnName("MAE").HasColumnType("decimal(10,4)");
            entity.Property(e => e.Rmse).HasColumnName("RMSE").HasColumnType("decimal(10,4)");
            entity.Property(e => e.Accuracy).HasColumnName("ACCURACY").HasColumnType("decimal(10,4)");
            entity.Property(e => e.PrecisionScore).HasColumnName("PRECISION_SCORE").HasColumnType("decimal(10,4)");
            entity.Property(e => e.RecallScore).HasColumnName("RECALL_SCORE").HasColumnType("decimal(10,4)");
            entity.Property(e => e.F1Score).HasColumnName("F1_SCORE").HasColumnType("decimal(10,4)");
            entity.Property(e => e.GhiChu).HasColumnName("GHI_CHU").HasMaxLength(300).IsUnicode();
            entity.HasOne(e => e.MoHinhAi).WithMany().HasForeignKey(e => e.MaModel).HasConstraintName("FK_AI_DANHGIA_RUN_MOHINHAI");
        });

        modelBuilder.Entity<AiDanhGiaChiTiet>(entity =>
        {
            entity.ToTable("AI_DANHGIA_CHITIET");
            entity.HasKey(e => e.MaDanhGiaChiTiet).HasName("PK_AI_DANHGIA_CHITIET");
            entity.Property(e => e.MaDanhGiaChiTiet).HasColumnName("MADANHGIA_CHITIET");
            entity.Property(e => e.MaDanhGia).HasColumnName("MADANHGIA");
            entity.Property(e => e.MaDuDoan).HasColumnName("MADUDOAN");
            entity.Property(e => e.MaNhanVien).HasColumnName("MANHANVIEN");
            entity.Property(e => e.MaCongViec).HasColumnName("MACONGVIEC");
            entity.Property(e => e.MaDuAn).HasColumnName("MADUAN");
            entity.Property(e => e.GiatriDuDoanSo).HasColumnName("GIATRI_DU_DOAN_SO").HasColumnType("decimal(10,4)");
            entity.Property(e => e.GiatriThucTeSo).HasColumnName("GIATRI_THUC_TE_SO").HasColumnType("decimal(10,4)");
            entity.Property(e => e.NhanDuDoan).HasColumnName("NHAN_DU_DOAN").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.NhanThucTe).HasColumnName("NHAN_THUC_TE").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.SoSaiLech).HasColumnName("SO_SAI_LECH").HasColumnType("decimal(10,4)");
            entity.Property(e => e.DungNhan).HasColumnName("DUNG_NHAN");
            entity.Property(e => e.DungSo).HasColumnName("DUNG_SO");
            entity.Property(e => e.DoTinCay).HasColumnName("DO_TIN_CAY").HasColumnType("decimal(10,4)");
            entity.Property(e => e.GhiChu).HasColumnName("GHI_CHU").HasMaxLength(300).IsUnicode();
        });

        modelBuilder.Entity<AiBusinessKpiRun>(entity =>
        {
            entity.ToTable("AI_BUSINESS_KPI_RUN");
            entity.HasKey(e => e.MaBusinessKpi).HasName("PK_AI_BUSINESS_KPI_RUN");
            entity.Property(e => e.MaBusinessKpi).HasColumnName("MABUSINESS_KPI");
            entity.Property(e => e.MaModel).HasColumnName("MAMODEL");
            entity.Property(e => e.MaPhienBan).HasColumnName("MAPHIENBAN");
            entity.Property(e => e.LoaiMoHinh).HasColumnName("LOAI_MO_HINH").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.TuNgay).HasColumnName("TU_NGAY").HasColumnType("date");
            entity.Property(e => e.DenNgay).HasColumnName("DEN_NGAY").HasColumnType("date");
            entity.Property(e => e.NgayTao).HasColumnName("NGAY_TAO").HasColumnType("datetime2(0)");
            entity.Property(e => e.TongDuDoan).HasColumnName("TONG_DU_DOAN");
            entity.Property(e => e.TongTacDong).HasColumnName("TONG_TAC_DONG");
            entity.Property(e => e.InterventionRate).HasColumnName("INTERVENTION_RATE").HasColumnType("decimal(8,4)");
            entity.Property(e => e.UserAcceptanceRate).HasColumnName("USER_ACCEPTANCE_RATE").HasColumnType("decimal(8,4)");
            entity.Property(e => e.UtilityScore).HasColumnName("UTILITY_SCORE").HasColumnType("decimal(8,4)");
            entity.Property(e => e.GhiChu).HasColumnName("GHI_CHU").HasMaxLength(300).IsUnicode();
        });

        modelBuilder.Entity<AiFeedback>(entity =>
        {
            entity.ToTable("AI_FEEDBACK");
            entity.HasKey(e => e.MaFeedback).HasName("PK_AI_FEEDBACK");
            entity.Property(e => e.MaFeedback).HasColumnName("MAFEEDBACK");
            entity.Property(e => e.MaDanhGia).HasColumnName("MADANHGIA");
            entity.Property(e => e.MaDuDoan).HasColumnName("MADUDOAN");
            entity.Property(e => e.MaNhanVien).HasColumnName("MANHANVIEN");
            entity.Property(e => e.DoChinhXac).HasColumnName("DO_CHINH_XAC");
            entity.Property(e => e.MucHuuIch).HasColumnName("MUC_HUU_ICH");
            entity.Property(e => e.DungSai).HasColumnName("DUNG_SAI");
            entity.Property(e => e.NoiDung).HasColumnName("NOI_DUNG").HasMaxLength(500).IsUnicode();
            entity.Property(e => e.HanhDongDeXuat).HasColumnName("HANH_DONG_DE_XUAT").HasMaxLength(300).IsUnicode();
            entity.Property(e => e.NgayPhanHoi).HasColumnName("NGAY_PHAN_HOI").HasColumnType("datetime2(0)");
            entity.HasIndex(e => new { e.MaDanhGia, e.NgayPhanHoi }).HasDatabaseName("IX_AI_FEEDBACK_MADANHGIA_NGAY_PHAN_HOI");
            entity.HasOne<AiDanhGiaRun>().WithMany().HasForeignKey(e => e.MaDanhGia).HasConstraintName("FK_AI_FEEDBACK_AI_DANHGIA_RUN");
            entity.HasOne<DuDoanAi>().WithMany().HasForeignKey(e => e.MaDuDoan).HasConstraintName("FK_AI_FEEDBACK_DUDOANAI");
            entity.HasOne<NhanVien>().WithMany().HasForeignKey(e => e.MaNhanVien).HasConstraintName("FK_AI_FEEDBACK_NHANVIEN");
        });

        modelBuilder.Entity<AiFeatureStore>(entity =>
        {
            entity.ToTable("AI_FEATURE_STORE");
            entity.HasKey(e => e.MaFeature).HasName("PK_AI_FEATURE_STORE");
            entity.Property(e => e.MaFeature).HasColumnName("MAFEATURE");
            entity.Property(e => e.MaModel).HasColumnName("MAMODEL");
            entity.Property(e => e.MaNhanVien).HasColumnName("MANHANVIEN");
            entity.Property(e => e.MaCongViec).HasColumnName("MACONGVIEC");
            entity.Property(e => e.MaDuAn).HasColumnName("MADUAN");
            entity.Property(e => e.FeatureName).HasColumnName("FEATURE_NAME").HasMaxLength(100).IsUnicode();
            entity.Property(e => e.FeatureValue).HasColumnName("FEATURE_VALUE").HasMaxLength(200).IsUnicode();
            entity.Property(e => e.FeatureType).HasColumnName("FEATURE_TYPE").HasMaxLength(30).IsUnicode();
            entity.Property(e => e.SourceTable).HasColumnName("SOURCE_TABLE").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.SourceKey).HasColumnName("SOURCE_KEY").HasMaxLength(100).IsUnicode();
            entity.Property(e => e.VersionTag).HasColumnName("VERSION_TAG").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.DongChot).HasColumnName("DONG_CHOT").HasColumnType("datetime2(0)");
            entity.HasIndex(e => new { e.MaModel, e.DongChot }).HasDatabaseName("IX_AI_FEATURE_STORE_MAMODEL_DONG_CHOT");
            entity.HasOne<MoHinhAi>().WithMany().HasForeignKey(e => e.MaModel).HasConstraintName("FK_AI_FEATURE_STORE_MOHINHAI");
            entity.HasOne<NhanVien>().WithMany().HasForeignKey(e => e.MaNhanVien).HasConstraintName("FK_AI_FEATURE_STORE_NHANVIEN");
        });

        modelBuilder.Entity<AiNhatKyCanThiep>(entity =>
        {
            entity.ToTable("AI_NHATKY_CAN_THIEP");
            entity.HasKey(e => e.MaCanThiep).HasName("PK_AI_NHATKY_CAN_THIEP");
            entity.Property(e => e.MaCanThiep).HasColumnName("MACANTHIEP");
            entity.Property(e => e.MaDanhGia).HasColumnName("MADANHGIA");
            entity.Property(e => e.MaDuDoan).HasColumnName("MADUDOAN");
            entity.Property(e => e.MaNhanVien).HasColumnName("MANHANVIEN");
            entity.Property(e => e.NguoiCanThiep).HasColumnName("NGUOI_CANTHIEP");
            entity.Property(e => e.ActionType).HasColumnName("ACTION_TYPE").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.ActionSource).HasColumnName("ACTION_SOURCE").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.Reason).HasColumnName("LY_DO").HasMaxLength(500).IsUnicode();
            entity.Property(e => e.OldValue).HasColumnName("GIA_TRI_CU").HasColumnType("nvarchar(max)").IsUnicode();
            entity.Property(e => e.NewValue).HasColumnName("GIA_TRI_MOI").HasColumnType("nvarchar(max)").IsUnicode();
            entity.Property(e => e.NguonCanThiep).HasColumnName("NGUON_CANTHIEP").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.NgayCanThiep).HasColumnName("NGAY_CAN_THIEP").HasColumnType("datetime2(0)");
            entity.Property(e => e.SoLanChinhSua).HasColumnName("SO_LAN_CHINH_SUA");

            entity.HasIndex(e => e.MaDanhGia).HasDatabaseName("IX_AI_NHATKY_CAN_THIEP_MADANHGIA");
            entity.HasIndex(e => e.MaDuDoan).HasDatabaseName("IX_AI_NHATKY_CAN_THIEP_MADUDOAN");
            entity.HasOne<AiDanhGiaRun>().WithMany().HasForeignKey(e => e.MaDanhGia).HasConstraintName("FK_AI_NHATKY_CAN_THIEP_AI_DANHGIA_RUN");
            entity.HasOne<DuDoanAi>().WithMany().HasForeignKey(e => e.MaDuDoan).HasConstraintName("FK_AI_NHATKY_CAN_THIEP_DUDOANAI");
            entity.HasOne<NhanVien>().WithMany().HasForeignKey(e => e.MaNhanVien).HasConstraintName("FK_AI_NHATKY_CAN_THIEP_NHANVIEN");
            entity.HasOne<NhanVien>().WithMany().HasForeignKey(e => e.NguoiCanThiep).HasConstraintName("FK_AI_NHATKY_CAN_THIEP_NGUOI_CANTHIEP");
        });

        modelBuilder.Entity<DuDoanAi>(entity =>
        {
            entity.ToTable("DUDOANAI");
            entity.HasKey(e => e.MaDuDoan).HasName("PK_DUDOANAI");
            entity.Property(e => e.MaDuDoan).HasColumnName("MADUDOAN");
            entity.Property(e => e.MaNhanVien).HasColumnName("MANHANVIEN");
            entity.Property(e => e.MaModel).HasColumnName("MAMODEL");
            entity.Property(e => e.thang).HasColumnName("THANG");
            entity.Property(e => e.nam).HasColumnName("NAM");
            entity.Property(e => e.ModelName).HasColumnName("MODELNAME").HasMaxLength(100).IsUnicode();
            entity.Property(e => e.DiemDuDoan).HasColumnName("DIEMDUDOAN").HasColumnType("decimal(5,2)");
            entity.Property(e => e.XacSuatTreHan).HasColumnName("XACSUATTREHAN").HasColumnType("decimal(5,4)");
            entity.Property(e => e.InputData).HasColumnName("INPUTDATA").HasColumnType("nvarchar(max)").IsUnicode();
            entity.Property(e => e.OutputData).HasColumnName("OUTPUTDATA").HasColumnType("nvarchar(max)").IsUnicode();
            entity.Property(e => e.Actor).HasColumnName("ACTOR").HasMaxLength(128);
            entity.Property(e => e.DeXuatCaiThien).HasColumnName("DEXUATCAITHIEN").HasMaxLength(300).IsUnicode();
            entity.Property(e => e.GoiYNguonLuc).HasColumnName("GOIYNGUONLUC").HasMaxLength(300).IsUnicode();
            entity.Property(e => e.ThoiGianDuDoan).HasColumnName("THOIGIANDUDOAN").HasColumnType("datetime2(0)");
            entity.HasIndex(e => new { e.MaNhanVien, e.ModelName, e.thang, e.nam })
                .HasDatabaseName("UQ_DUDOANAI_NV_MODEL_THANG_NAM")
                .IsUnique()
                .HasFilter("[MODELNAME] IS NOT NULL");
            entity.HasOne(e => e.NhanVien).WithMany(e => e.DuDoanAis).HasForeignKey(e => e.MaNhanVien).HasConstraintName("FK_DUDOANAI_NHANVIEN");
            entity.HasOne(e => e.MoHinhAi).WithMany(e => e.DuDoanAis).HasForeignKey(e => e.MaModel).HasConstraintName("FK_DUDOANAI_MOHINHAI");
        });

        modelBuilder.Entity<DuLieuAi>(entity =>
        {
            entity.ToTable("DULIEUAI");
            entity.HasKey(e => e.MaDuLieu).HasName("PK_DULIEUAI");
            entity.Property(e => e.MaDuLieu).HasColumnName("MADULIEU");
            entity.Property(e => e.MaNhanVien).HasColumnName("MANHANVIEN");
            entity.Property(e => e.SoCongViecHoanThanh).HasColumnName("SOCONGVIECHOANTHANH");
            entity.Property(e => e.SoCongViecTreHan).HasColumnName("SOCONGVIECTREHAN");
            entity.Property(e => e.ThoiGianTrungBinh).HasColumnName("THOIGIANTRUNGBINH").HasColumnType("decimal(6,2)");
            entity.Property(e => e.KpiTrungBinh).HasColumnName("KPITRUNGBINH").HasColumnType("decimal(5,2)");
            entity.HasOne(e => e.NhanVien).WithMany(e => e.DuLieuAis).HasForeignKey(e => e.MaNhanVien).HasConstraintName("FK_DULIEUAI_NHANVIEN");
        });

        modelBuilder.Entity<KetQuaKpi>(entity =>
        {
            entity.ToTable("KETQUAKPI");
            entity.HasKey(e => e.MaKetQua).HasName("PK_KETQUAKPI");
            entity.Property(e => e.MaKetQua).HasColumnName("MAKETQUA");
            entity.Property(e => e.MaNhanVien).HasColumnName("MANHANVIEN");
            entity.Property(e => e.MaKpi).HasColumnName("MAKPI");
            entity.Property(e => e.DiemSo).HasColumnName("DIEMSO").HasColumnType("decimal(5,2)");
            entity.Property(e => e.thang).HasColumnName("THANG");
            entity.Property(e => e.nam).HasColumnName("NAM");
            entity.HasIndex(e => new { e.MaNhanVien, e.MaKpi, e.thang, e.nam })
                .HasDatabaseName("UQ_KETQUAKPI_NV_KPI_THANG_NAM")
                .IsUnique();
            entity.HasIndex(e => new { e.MaNhanVien, e.thang, e.nam })
                .HasDatabaseName("IX_KETQUAKPI_MONTH");
            entity.HasOne(e => e.NhanVien).WithMany(e => e.KetQuaKpis).HasForeignKey(e => e.MaNhanVien).HasConstraintName("FK_KETQUAKPI_NHANVIEN");
            entity.HasOne(e => e.DanhMucKpi).WithMany(e => e.KetQuaKpis).HasForeignKey(e => e.MaKpi).HasConstraintName("FK_KETQUAKPI_DANHMUCKPI");
        });

        modelBuilder.Entity<KetQuaKpiTong>(entity =>
        {
            entity.ToTable("KETQUAKPI_TONG");
            entity.HasKey(e => e.MaKetQuaTong).HasName("PK_KETQUAKPI_TONG");
            entity.Property(e => e.MaKetQuaTong).HasColumnName("MAKETQUATONG");
            entity.Property(e => e.MaNhanVien).HasColumnName("MANHANVIEN");
            entity.Property(e => e.Thang).HasColumnName("THANG");
            entity.Property(e => e.Nam).HasColumnName("NAM");
            entity.Property(e => e.DiemTong).HasColumnName("DIEMTONG").HasColumnType("decimal(5,2)");
            entity.Property(e => e.XepLoai).HasColumnName("XEPLOAI").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.SoKpiThanhPhan).HasColumnName("SOKPI_THANHPHAN");
            entity.Property(e => e.NgayTinh).HasColumnName("NGAYTINH").HasColumnType("datetime2(0)").HasDefaultValueSql("getdate()");
            entity.HasIndex(e => new { e.MaNhanVien, e.Thang, e.Nam })
                .HasDatabaseName("UQ_KETQUAKPI_TONG_NV_THANG_NAM")
                .IsUnique();
            entity.HasOne(e => e.NhanVien).WithMany(e => e.KetQuaKpiTongs).HasForeignKey(e => e.MaNhanVien).HasConstraintName("FK_KETQUAKPI_TONG_NHANVIEN");
        });

        modelBuilder.Entity<KpiNhanVien>(entity =>
        {
            entity.ToTable("KPI_NHANVIEN");
            entity.HasKey(e => new { e.MaKpi, e.MaNhanVien }).HasName("PK_KPI_NHANVIEN");
            entity.Property(e => e.MaKpi).HasColumnName("MAKPI");
            entity.Property(e => e.MaNhanVien).HasColumnName("MANHANVIEN");
            entity.Property(e => e.TrongSoApDung).HasColumnName("TRONGSO_APDUNG").HasColumnType("decimal(5,2)");
            entity.Property(e => e.TuNgay).HasColumnName("TU_NGAY").HasColumnType("date");
            entity.Property(e => e.DenNgay).HasColumnName("DEN_NGAY").HasColumnType("date");
            entity.Property(e => e.NgayKetThucApDung).HasColumnName("NGAYKETTHUC_APDUNG").HasColumnType("datetime2(0)");
            entity.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasDefaultValue(true);
            entity.Property(e => e.TrangThai).HasColumnName("TRANGTHAI");
            entity.Property(e => e.GhiChu).HasColumnName("GHI_CHU").HasMaxLength(300).IsUnicode();
            entity.HasCheckConstraint("CK_KPI_NHANVIEN_TRONGSO_APDUNG", "[TRONGSO_APDUNG] >= 0 AND [TRONGSO_APDUNG] <= 100");
            entity.HasOne(e => e.DanhMucKpi).WithMany(e => e.KpiNhanViens).HasForeignKey(e => e.MaKpi).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_KPI_NHANVIEN_DANHMUCKPI");
            entity.HasOne(e => e.NhanVien).WithMany(e => e.KpiNhanViens).HasForeignKey(e => e.MaNhanVien).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_KPI_NHANVIEN_NHANVIEN");
        });

        modelBuilder.Entity<KpiNhom>(entity =>
        {
            entity.ToTable("KPI_NHOM");
            entity.HasKey(e => new { e.MaKpi, e.MaNhom }).HasName("PK_KPI_NHOM");
            entity.Property(e => e.MaKpi).HasColumnName("MAKPI");
            entity.Property(e => e.MaNhom).HasColumnName("MANHOM");
            entity.Property(e => e.TrongSoApDung).HasColumnName("TRONGSO_APDUNG").HasColumnType("decimal(5,2)");
            entity.Property(e => e.TuNgay).HasColumnName("TU_NGAY").HasColumnType("date");
            entity.Property(e => e.DenNgay).HasColumnName("DEN_NGAY").HasColumnType("date");
            entity.Property(e => e.NgayKetThucApDung).HasColumnName("NGAYKETTHUC_APDUNG").HasColumnType("datetime2(0)");
            entity.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasDefaultValue(true);
            entity.Property(e => e.TrangThai).HasColumnName("TRANGTHAI");
            entity.Property(e => e.GhiChu).HasColumnName("GHI_CHU").HasMaxLength(300).IsUnicode();
            entity.HasCheckConstraint("CK_KPI_NHOM_TRONGSO_APDUNG", "[TRONGSO_APDUNG] >= 0 AND [TRONGSO_APDUNG] <= 100");
            entity.HasOne(e => e.DanhMucKpi).WithMany(e => e.KpiNhoms).HasForeignKey(e => e.MaKpi).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_KPI_NHOM_DANHMUCKPI");
            entity.HasOne(e => e.Nhom).WithMany(e => e.KpiNhoms).HasForeignKey(e => e.MaNhom).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_KPI_NHOM_NHOM");
        });

        modelBuilder.Entity<KpiDuAn>(entity =>
        {
            entity.ToTable("KPI_DUAN");
            entity.HasKey(e => new { e.MaKpi, e.MaDuAn }).HasName("PK_KPI_DUAN");
            entity.Property(e => e.MaKpi).HasColumnName("MAKPI");
            entity.Property(e => e.MaDuAn).HasColumnName("MADUAN");
            entity.Property(e => e.TrongSoApDung).HasColumnName("TRONGSO_APDUNG").HasColumnType("decimal(5,2)");
            entity.Property(e => e.TuNgay).HasColumnName("TU_NGAY").HasColumnType("date");
            entity.Property(e => e.DenNgay).HasColumnName("DEN_NGAY").HasColumnType("date");
            entity.Property(e => e.NgayKetThucApDung).HasColumnName("NGAYKETTHUC_APDUNG").HasColumnType("datetime2(0)");
            entity.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasDefaultValue(true);
            entity.Property(e => e.TrangThai).HasColumnName("TRANGTHAI");
            entity.Property(e => e.GhiChu).HasColumnName("GHI_CHU").HasMaxLength(300).IsUnicode();
            entity.HasCheckConstraint("CK_KPI_DUAN_TRONGSO_APDUNG", "[TRONGSO_APDUNG] >= 0 AND [TRONGSO_APDUNG] <= 100");
            entity.HasOne(e => e.DanhMucKpi).WithMany(e => e.KpiDuAns).HasForeignKey(e => e.MaKpi).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_KPI_DUAN_DANHMUCKPI");
            entity.HasOne(e => e.DuAn).WithMany(e => e.KpiDuAns).HasForeignKey(e => e.MaDuAn).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_KPI_DUAN_DUAN");
        });

        modelBuilder.Entity<KpiPhongBan>(entity =>
        {
            entity.ToTable("KPI_PHONGBAN");
            entity.HasKey(e => new { e.MaKpi, e.MaPhongBan }).HasName("PK_KPI_PHONGBAN");
            entity.Property(e => e.MaKpi).HasColumnName("MAKPI");
            entity.Property(e => e.MaPhongBan).HasColumnName("MAPHONGBAN");
            entity.Property(e => e.TrongSoApDung).HasColumnName("TRONGSO_APDUNG").HasColumnType("decimal(5,2)");
            entity.Property(e => e.TuNgay).HasColumnName("TU_NGAY").HasColumnType("date");
            entity.Property(e => e.DenNgay).HasColumnName("DEN_NGAY").HasColumnType("date");
            entity.Property(e => e.NgayKetThucApDung).HasColumnName("NGAYKETTHUC_APDUNG").HasColumnType("datetime2(0)");
            entity.Property(e => e.IsActive).HasColumnName("IS_ACTIVE").HasDefaultValue(true);
            entity.Property(e => e.TrangThai).HasColumnName("TRANGTHAI");
            entity.Property(e => e.GhiChu).HasColumnName("GHI_CHU").HasMaxLength(300).IsUnicode();
            entity.HasCheckConstraint("CK_KPI_PHONGBAN_TRONGSO_APDUNG", "[TRONGSO_APDUNG] >= 0 AND [TRONGSO_APDUNG] <= 100");
            entity.HasOne(e => e.DanhMucKpi).WithMany(e => e.KpiPhongBans).HasForeignKey(e => e.MaKpi).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_KPI_PHONGBAN_DANHMUCKPI");
            entity.HasOne(e => e.PhongBan).WithMany(e => e.KpiPhongBans).HasForeignKey(e => e.MaPhongBan).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_KPI_PHONGBAN_PHONGBAN");
        });

        modelBuilder.Entity<DeXuatKpi>(entity =>
        {
            entity.ToTable("DE_XUAT_KPI");
            entity.HasKey(e => e.MaDeXuat).HasName("PK_DE_XUAT_KPI");
            entity.Property(e => e.MaDeXuat).HasColumnName("MADEXUAT");
            entity.Property(e => e.MaKpi).HasColumnName("MAKPI");
            entity.Property(e => e.MaLoaiKpi).HasColumnName("MALOAIKPI");
            entity.Property(e => e.NguoiDeXuat).HasColumnName("NGUOIDE_XUAT");
            entity.Property(e => e.NguoiDuyet).HasColumnName("NGUOIDUYET");
            entity.Property(e => e.NguoiCapNhat).HasColumnName("NGUOICAPNHAT");
            entity.Property(e => e.LoaiDeXuat).HasColumnName("LOAI_DEXUAT").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.MaNhanVienApDung).HasColumnName("MANHANVIEN_APDUNG");
            entity.Property(e => e.MaNhomApDung).HasColumnName("MANHOM_APDUNG");
            entity.Property(e => e.MaPhongBanApDung).HasColumnName("MAPHONGBAN_APDUNG");
            entity.Property(e => e.MaDuAnApDung).HasColumnName("MADUAN_APDUNG");
            entity.Property(e => e.TuNgay).HasColumnName("TU_NGAY").HasColumnType("date");
            entity.Property(e => e.DenNgay).HasColumnName("DEN_NGAY").HasColumnType("date");
            entity.Property(e => e.TrongSoDeXuat).HasColumnName("TRONGSO_DEXUAT").HasColumnType("decimal(5,2)");
            entity.Property(e => e.TenKpiDeXuat).HasColumnName("TENKPI_DEXUAT").HasMaxLength(150).IsUnicode();
            entity.Property(e => e.MoTaKpiDeXuat).HasColumnName("MOTA_KPI_DEXUAT").HasMaxLength(500).IsUnicode();
            entity.Property(e => e.LyDo).HasColumnName("LYDO").HasMaxLength(500).IsUnicode();
            entity.Property(e => e.TrangThai).HasColumnName("TRANGTHAI").HasMaxLength(50).IsUnicode().HasDefaultValue("ChoDuyet");
            entity.Property(e => e.PhanHoiAdmin).HasColumnName("PHANHOI_ADMIN").HasMaxLength(500).IsUnicode();
            entity.Property(e => e.GhiChu).HasColumnName("GHI_CHU").HasMaxLength(300).IsUnicode();
            entity.Property(e => e.NgayTao).HasColumnName("NGAYTAO").HasColumnType("datetime2(0)").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.NgayCapNhat).HasColumnName("NGAYCAPNHAT").HasColumnType("datetime2(0)");
            entity.Property(e => e.NgayDuyet).HasColumnName("NGAYDUYET").HasColumnType("datetime2(0)");

            entity.HasCheckConstraint("CK_DE_XUAT_KPI_TRANGTHAI", "[TRANGTHAI] IN (N'ChoDuyet',N'DaDuyet',N'TuChoi',N'CanChinhSua')");
            entity.HasCheckConstraint("CK_DE_XUAT_KPI_LOAI", "[LOAI_DEXUAT] IN (N'TaoMoiKPI',N'ApDungKPI',N'DieuChinhKPI',N'HuyApDungKPI')");
            entity.HasCheckConstraint("CK_DE_XUAT_KPI_TRONGSO", "[TRONGSO_DEXUAT] >= 0 AND [TRONGSO_DEXUAT] <= 100");
            entity.HasCheckConstraint("CK_DE_XUAT_KPI_THOIGIAN", "[DEN_NGAY] IS NULL OR [DEN_NGAY] >= [TU_NGAY]");
            entity.HasCheckConstraint("CK_DE_XUAT_KPI_DOITUONG",
                "(CASE WHEN [MANHANVIEN_APDUNG] IS NULL THEN 0 ELSE 1 END + CASE WHEN [MANHOM_APDUNG] IS NULL THEN 0 ELSE 1 END + CASE WHEN [MAPHONGBAN_APDUNG] IS NULL THEN 0 ELSE 1 END + CASE WHEN [MADUAN_APDUNG] IS NULL THEN 0 ELSE 1 END) = 1");

            entity.HasOne(e => e.DanhMucKpi).WithMany().HasForeignKey(e => e.MaKpi).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_DE_XUAT_KPI_DANHMUCKPI");
            entity.HasOne(e => e.LoaiKpi).WithMany().HasForeignKey(e => e.MaLoaiKpi).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_DE_XUAT_KPI_LOAIKPI");
            entity.HasOne(e => e.NhanVienDeXuat).WithMany().HasForeignKey(e => e.NguoiDeXuat).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_DE_XUAT_KPI_NGUOIDE_XUAT");
            entity.HasOne(e => e.NhanVienDuyet).WithMany().HasForeignKey(e => e.NguoiDuyet).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_DE_XUAT_KPI_NGUOIDUYET");
            entity.HasOne(e => e.NhanVienCapNhat).WithMany().HasForeignKey(e => e.NguoiCapNhat).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_DE_XUAT_KPI_NGUOICAPNHAT");
            entity.HasOne(e => e.NhanVienApDung).WithMany().HasForeignKey(e => e.MaNhanVienApDung).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_DE_XUAT_KPI_NHANVIEN_APDUNG");
            entity.HasOne(e => e.NhomApDung).WithMany().HasForeignKey(e => e.MaNhomApDung).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_DE_XUAT_KPI_NHOM_APDUNG");
            entity.HasOne(e => e.PhongBanApDung).WithMany().HasForeignKey(e => e.MaPhongBanApDung).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_DE_XUAT_KPI_PHONGBAN_APDUNG");
            entity.HasOne(e => e.DuAnApDung).WithMany().HasForeignKey(e => e.MaDuAnApDung).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_DE_XUAT_KPI_DUAN_APDUNG");
        });

        modelBuilder.Entity<KyNang>(entity =>
        {
            entity.ToTable("KYNANG");
            entity.HasKey(e => e.MaKyNang).HasName("PK_KYNANG");
            entity.Property(e => e.MaKyNang).HasColumnName("MAKYNANG");
            entity.Property(e => e.TenKyNang).HasColumnName("TENKYNANG").HasMaxLength(300).IsUnicode();
            entity.Property(e => e.MoTa).HasColumnName("MOTA").HasMaxLength(300).IsUnicode();
            entity.Property(e => e.TrangThai).HasColumnName("TRANGTHAI");
        });

        modelBuilder.Entity<KyNangNhanVien>(entity =>
        {
            entity.ToTable("KYNANGNHANVIEN");
            entity.HasKey(e => new { e.MaKyNang, e.MaNhanVien }).HasName("PK_KYNANGNHANVIEN");
            entity.Property(e => e.MaKyNang).HasColumnName("MAKYNANG");
            entity.Property(e => e.MaNhanVien).HasColumnName("MANHANVIEN");
            entity.Property(e => e.CapDo).HasColumnName("CAPDO");
            entity.Property(e => e.SoDuAnDaDung).HasColumnName("SODUANDADUNG");
            entity.Ignore(e => e.NgayDatDuoc);
            entity.HasOne(e => e.KyNang).WithMany(e => e.KyNangNhanViens).HasForeignKey(e => e.MaKyNang).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_KYNANGNHANVIEN_KYNANG");
            entity.HasOne(e => e.NhanVien).WithMany(e => e.KyNangNhanViens).HasForeignKey(e => e.MaNhanVien).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_KYNANGNHANVIEN_NHANVIEN");
        });

        modelBuilder.Entity<LoaiKpi>(entity =>
        {
            entity.ToTable("LOAIKPI");
            entity.HasKey(e => e.MaLoaiKpi).HasName("PK_LOAIKPI");
            entity.Property(e => e.MaLoaiKpi).HasColumnName("MALOAIKPI");
            entity.Property(e => e.TenLoaiKpi).HasColumnName("TENLOAIKPI").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.HeSo).HasColumnName("HESO").HasColumnType("decimal(5,2)").HasDefaultValue(1m);
            entity.Property(e => e.IsActive).HasColumnName("ISACTIVE").HasDefaultValue(true);
        });

        modelBuilder.Entity<KpiXepLoai>(entity =>
        {
            entity.ToTable("KPI_XEPLOAI");
            entity.HasKey(e => e.Id).HasName("PK_KPI_XEPLOAI");
            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Code).HasColumnName("CODE").HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.Label).HasColumnName("LABEL").HasMaxLength(100).IsUnicode();
            entity.Property(e => e.MoTa).HasColumnName("MOTA").HasMaxLength(500).IsUnicode();
            entity.Property(e => e.MinScore).HasColumnName("MINSCORE").HasColumnType("decimal(5,2)");
            entity.Property(e => e.MaxScore).HasColumnName("MAXSCORE").HasColumnType("decimal(5,2)");
            entity.Property(e => e.ColorHex).HasColumnName("COLORHEX").HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.SortOrder).HasColumnName("SORTORDER");
            entity.Property(e => e.IsActive).HasColumnName("ISACTIVE").HasDefaultValue(true);
            entity.Property(e => e.IsSystem).HasColumnName("ISSYSTEM").HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasColumnName("CREATEDAT").HasColumnType("datetime2(0)");
            entity.Property(e => e.UpdatedAt).HasColumnName("UPDATEDAT").HasColumnType("datetime2(0)");

            entity.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UX_KPI_XEPLOAI_CODE");
            entity.HasIndex(e => new { e.Label, e.IsActive }).HasDatabaseName("IX_KPI_XEPLOAI_LABEL_ACTIVE");

            entity.HasData(
                new KpiXepLoai { Id = 1, Code = "EXCELLENT", Label = "Xuat sac", MoTa = "Nhan vien hoan thanh vuot ky vong", MinScore = 90m, MaxScore = 100m, ColorHex = "#16A34A", SortOrder = 1, IsActive = true, IsSystem = true, CreatedAt = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc) },
                new KpiXepLoai { Id = 2, Code = "GOOD", Label = "Tot", MoTa = "Nhan vien hoan thanh tot muc tieu", MinScore = 75m, MaxScore = 89.99m, ColorHex = "#0EA5E9", SortOrder = 2, IsActive = true, IsSystem = true, CreatedAt = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc) },
                new KpiXepLoai { Id = 3, Code = "AVERAGE", Label = "Trung binh", MoTa = "Nhan vien dat muc trung binh", MinScore = 60m, MaxScore = 74.99m, ColorHex = "#F59E0B", SortOrder = 3, IsActive = true, IsSystem = true, CreatedAt = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc) },
                new KpiXepLoai { Id = 4, Code = "POOR", Label = "Kem", MoTa = "Nhan vien chua dat yeu cau", MinScore = 0m, MaxScore = 59.99m, ColorHex = "#DC2626", SortOrder = 4, IsActive = true, IsSystem = true, CreatedAt = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc) }
            );
        });

        modelBuilder.Entity<LoaiThongBao>(entity =>
        {
            entity.ToTable("LOAITHONGBAO");
            entity.HasKey(e => e.MaLoai).HasName("PK_LOAITHONGBAO");
            entity.Property(e => e.MaLoai).HasColumnName("MALOAI");
            entity.Property(e => e.TenLoai).HasColumnName("TENLOAI").HasMaxLength(50).IsUnicode();
        });

        modelBuilder.Entity<MoHinhAi>(entity =>
        {
            entity.ToTable("MOHINHAI");
            entity.HasKey(e => e.MaModel).HasName("PK_MOHINHAI");
            entity.Property(e => e.MaModel).HasColumnName("MAMODEL");
            entity.Property(e => e.TenModel).HasColumnName("TENMODEL").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.Version).HasColumnName("VERSION").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.NgayTrain).HasColumnName("NGAYTRAIN").HasColumnType("datetime2(0)");
        });

        modelBuilder.Entity<MoHinhDuLieuAi>(entity =>
        {
            entity.ToTable("MOHINH_DULIEUAI");
            entity.HasKey(e => new { e.MaModel, e.MaDuLieu }).HasName("PK_MOHINH_DULIEUAI");
            entity.Property(e => e.MaModel).HasColumnName("MAMODEL");
            entity.Property(e => e.MaDuLieu).HasColumnName("MADULIEU");
            entity.Property(e => e.MucDich).HasColumnName("MUC_DICH").HasMaxLength(30).IsUnicode();
            entity.Property(e => e.NgaySuDung).HasColumnName("NGAY_SU_DUNG").HasColumnType("datetime2(0)");
            entity.Property(e => e.MetricChinh).HasColumnName("METRIC_CHINH").HasColumnType("decimal(8,4)");
            entity.HasOne(e => e.MoHinhAi).WithMany(e => e.MoHinhDuLieuAis).HasForeignKey(e => e.MaModel).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_MOHINH_DULIEUAI_MOHINHAI");
            entity.HasOne(e => e.DuLieuAi).WithMany(e => e.MoHinhDuLieuAis).HasForeignKey(e => e.MaDuLieu).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_MOHINH_DULIEUAI_DULIEUAI");
        });

        modelBuilder.Entity<NhanVien>(entity =>
        {
            entity.ToTable("NHANVIEN");
            entity.HasKey(e => e.MaNhanVien).HasName("PK_NHANVIEN");
            entity.Property(e => e.MaNhanVien).HasColumnName("MANHANVIEN");
            entity.Property(e => e.MaPhongBan).HasColumnName("MAPHONGBAN");
            entity.Property(e => e.PhoMaPhongBan).HasColumnName("PHO_MAPHONGBAN");
            entity.Property(e => e.HoTen).HasColumnName("HOTEN").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.NgaySinh).HasColumnName("NGAYSINH").HasColumnType("datetime2(0)");
            entity.Property(e => e.Cccd).HasColumnName("CCCD").HasColumnType("varchar(12)").IsUnicode(false);
            entity.Property(e => e.DiaChi).HasColumnName("DIACHI").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.GioiTinh).HasColumnName("GIOITINH").HasMaxLength(10).IsUnicode();
            entity.Property(e => e.Email).HasColumnName("EMAIL").HasColumnType("varchar(100)").IsUnicode(false);
            entity.Property(e => e.Sdt).HasColumnName("SDT").HasColumnType("varchar(15)").IsUnicode(false);
            entity.Property(e => e.NgayVaoLam).HasColumnName("NGAYVAOLAM").HasColumnType("datetime2(0)");
            entity.Property(e => e.TrangThai).HasColumnName("TRANGTHAI");
            entity.Property(e => e.AspNetUserId).HasColumnName("AspNetUserId").HasMaxLength(128);
            entity.Property(e => e.MaChucVu).HasColumnName("MACHUCVU");

            entity.HasIndex(e => e.Cccd).HasDatabaseName("UX_NHANVIEN_CCCD").IsUnique().HasFilter("[CCCD] IS NOT NULL");
            entity.HasIndex(e => e.Email).HasDatabaseName("UX_NHANVIEN_EMAIL").IsUnique().HasFilter("[EMAIL] IS NOT NULL");
            entity.HasIndex(e => e.AspNetUserId).HasDatabaseName("UX_NHANVIEN_AspNetUserId").IsUnique().HasFilter("[AspNetUserId] IS NOT NULL");

            entity.HasOne(e => e.PhongBanQuanLy).WithMany(e => e.NhanVienQuanLys).HasForeignKey(e => e.MaPhongBan).HasConstraintName("FK_NHANVIEN_PHONGBAN");
            entity.HasOne(e => e.PhongBanPhoTrach).WithMany(e => e.NhanVienPhoTrachs).HasForeignKey(e => e.PhoMaPhongBan).HasConstraintName("FK_NHANVIEN_PHO_PHONGBAN");
            entity.HasOne(e => e.ChucVu).WithMany(e => e.NhanViens).HasForeignKey(e => e.MaChucVu).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_NHANVIEN_CHUCVU");
            entity.HasOne(e => e.AspNetUser).WithOne(e => e.NhanVien).HasForeignKey<NhanVien>(e => e.AspNetUserId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_NHANVIEN_AspNetUsers");
        });

        modelBuilder.Entity<NhatKyHoatDong>(entity =>
        {
            entity.ToTable("NHATKYHOATDONG");
            entity.HasKey(e => e.MaNhatKyHoatDong).HasName("PK_NHATKYHOATDONG");
            entity.Property(e => e.MaNhatKyHoatDong).HasColumnName("MANHATKYHOATDONG");
            entity.Property(e => e.MaNhanVien).HasColumnName("MANHANVIEN");
            entity.Property(e => e.HanhDong).HasColumnName("HANHDONG").HasMaxLength(300).IsUnicode();
            entity.Property(e => e.DoiTuong).HasColumnName("DOITUONG").HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.DuLieuCu).HasColumnName("DULIEUCU").HasColumnType("nvarchar(max)").IsUnicode();
            entity.Property(e => e.DuLieuMoi).HasColumnName("DULIEUMOI").HasColumnType("nvarchar(max)").IsUnicode();
            entity.Property(e => e.ThoiGian).HasColumnName("THOIGIAN").HasColumnType("datetime2(0)");
            entity.Property(e => e.Ip).HasColumnName("IP").HasMaxLength(64).IsUnicode(false);
            entity.Property(e => e.TrangThai).HasColumnName("TRANGTHAI").HasMaxLength(30).IsUnicode(false);
            entity.HasIndex(e => e.ThoiGian).HasDatabaseName("IX_NHATKYHOATDONG_THOIGIAN");
            entity.HasIndex(e => e.HanhDong).HasDatabaseName("IX_NHATKYHOATDONG_HANHDONG");
            entity.HasIndex(e => e.DoiTuong).HasDatabaseName("IX_NHATKYHOATDONG_DOITUONG");
            entity.HasOne(e => e.NhanVien).WithMany(e => e.NhatKyHoatDongs).HasForeignKey(e => e.MaNhanVien).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_NHATKYHOATDONG_NHANVIEN");
        });

        modelBuilder.Entity<Nhom>(entity =>
        {
            entity.ToTable("NHOM");
            entity.HasKey(e => e.MaNhom).HasName("PK_NHOM");
            entity.Property(e => e.MaNhom).HasColumnName("MANHOM");
            entity.Property(e => e.TenNhom).HasColumnName("TENNHOM").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.NgayTao).HasColumnName("NGAYTAO").HasColumnType("datetime2(0)");
            entity.Property(e => e.TruongNhom).HasColumnName("TRUONGNHOM");
            entity.HasOne(e => e.NhanVienTruongNhom).WithMany(e => e.NhomTruongNhoms).HasForeignKey(e => e.TruongNhom).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_NHOM_TRUONGNHOM");
        });

        modelBuilder.Entity<PhongBan>(entity =>
        {
            entity.ToTable("PHONGBAN");
            entity.HasKey(e => e.MaPhongBan).HasName("PK_PHONGBAN");
            entity.Property(e => e.MaPhongBan).HasColumnName("MAPHONGBAN");
            entity.Property(e => e.TenPhongBan).HasColumnName("TENPHONGBAN").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.MoTa).HasColumnName("MOTA").HasMaxLength(300).IsUnicode();
            entity.Property(e => e.MaTruongPhong).HasColumnName("MATRUONGPHONG");
            entity.HasOne(e => e.TruongPhong).WithMany(e => e.PhongBans).HasForeignKey(e => e.MaTruongPhong).HasConstraintName("FK_PHONGBAN_TRUONGPHONG");
        });

        modelBuilder.Entity<ThanhVienNhom>(entity =>
        {
            entity.ToTable("THANHVIENNHOM");
            entity.HasKey(e => new { e.MaNhanVien, e.MaNhom }).HasName("PK_THANHVIENNHOM");
            entity.Property(e => e.MaNhanVien).HasColumnName("MANHANVIEN");
            entity.Property(e => e.MaNhom).HasColumnName("MANHOM");
            entity.Property(e => e.NgayGiaNhap).HasColumnName("NGAYGIANHAP").HasColumnType("datetime2(0)");
            entity.Property(e => e.VaiTroTrongNhom).HasColumnName("VAITROTRONGNHOM").HasMaxLength(300).IsUnicode();
            entity.HasOne(e => e.NhanVien).WithMany(e => e.ThanhVienNhoms).HasForeignKey(e => e.MaNhanVien).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_THANHVIENNHOM_NHANVIEN");
            entity.HasOne(e => e.Nhom).WithMany(e => e.ThanhVienNhoms).HasForeignKey(e => e.MaNhom).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_THANHVIENNHOM_NHOM");
        });

        modelBuilder.Entity<ThongBao>(entity =>
        {
            entity.ToTable("THONGBAO");
            entity.HasKey(e => e.MaThongBao).HasName("PK_THONGBAO");
            entity.Property(e => e.MaThongBao).HasColumnName("MATHONGBAO");
            entity.Property(e => e.MaLoai).HasColumnName("MALOAI");
            entity.Property(e => e.NoiDung).HasColumnName("NOIDUNG").HasMaxLength(300).IsUnicode();
            entity.Property(e => e.ThoiGian).HasColumnName("THOIGIAN").HasColumnType("datetime2(0)");
            entity.HasOne(e => e.LoaiThongBao).WithMany(e => e.ThongBaos).HasForeignKey(e => e.MaLoai).HasConstraintName("FK_THONGBAO_LOAITHONGBAO");
        });

        modelBuilder.Entity<ThongBaoNhanVien>(entity =>
        {
            entity.ToTable("THONGBAO_NHANVIEN");
            entity.HasKey(e => new { e.MaNhanVien, e.MaThongBao }).HasName("PK_THONGBAO_NHANVIEN");
            entity.Property(e => e.MaNhanVien).HasColumnName("MANHANVIEN");
            entity.Property(e => e.MaThongBao).HasColumnName("MATHONGBAO");
            entity.Property(e => e.DaDoc).HasColumnName("DADOC");
            entity.HasOne(e => e.NhanVien).WithMany(e => e.ThongBaoNhanViens).HasForeignKey(e => e.MaNhanVien).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_THONGBAO_NHANVIEN_NHANVIEN");
            entity.HasOne(e => e.ThongBao).WithMany(e => e.ThongBaoNhanViens).HasForeignKey(e => e.MaThongBao).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_THONGBAO_NHANVIEN_THONGBAO");
        });

        // Task Models Configuration
        modelBuilder.Entity<CongViec>(entity =>
        {
            entity.ToTable("CONGVIEC");
            entity.HasKey(e => e.MaCongViec).HasName("PK_CONGVIEC");
            entity.Property(e => e.MaCongViec).HasColumnName("MACONGVIEC");
            entity.Property(e => e.MaDuAn).HasColumnName("MADUAN");
            entity.Property(e => e.MaCongViecCha).HasColumnName("MACONGVIECCHA");
            entity.Property(e => e.TenCongViec).HasColumnName("TENCONGVIEC").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.MoTa).HasColumnName("MOTA").HasMaxLength(300).IsUnicode();
            entity.Property(e => e.NgayBatDau).HasColumnName("NGAYBATDAU").HasColumnType("datetime2(0)");
            entity.Property(e => e.HanHoanThanh).HasColumnName("HANHOANTHANH").HasColumnType("datetime2(0)");
            entity.Property(e => e.MaTrangThai).HasColumnName("MATRANGTHAI");
            entity.Property(e => e.MaDoUuTien).HasColumnName("MADOUUTIEN");
            entity.Property(e => e.MaDoKho).HasColumnName("MADOKHO");
            entity.Property(e => e.DiemCongViec).HasColumnName("DIEMCONGVIEC").HasColumnType("decimal(5,2)");
            entity.Property(e => e.PhanTramHoanThanh).HasColumnName("PHANTRAMHOANTHANH").HasColumnType("decimal(5,2)");
            entity.Property(e => e.NgayTao).HasColumnName("NGAYTAO").HasColumnType("datetime2(0)");
            entity.Property(e => e.NguoiTao).HasColumnName("NGUOITAO").HasMaxLength(128);
            entity.Property(e => e.NgayCapNhat).HasColumnName("NGAYCAPNHAT").HasColumnType("datetime2(0)");
            entity.Property(e => e.NguoiCapNhat).HasColumnName("NGUOICAPNHAT").HasMaxLength(128);
            entity.Property(e => e.DaXoa).HasColumnName("DAXOA");
            entity.HasOne(e => e.DuAn).WithMany(e => e.CongViecs).HasForeignKey(e => e.MaDuAn).HasConstraintName("FK_CONGVIEC_DUAN");
            entity.HasOne(e => e.DoUuTien).WithMany(e => e.CongViecs).HasForeignKey(e => e.MaDoUuTien).HasConstraintName("FK_CONGVIEC_DOUUTIEN");
            entity.HasOne(e => e.DoKho).WithMany(e => e.CongViecs).HasForeignKey(e => e.MaDoKho).HasConstraintName("FK_CONGVIEC_DOKHO");
            entity.HasOne(e => e.CongViecCha).WithMany(e => e.CongViecCon).HasForeignKey(e => e.MaCongViecCha).HasConstraintName("FK_CONGVIEC_CONGVIEC");
        });

        modelBuilder.Entity<PhanCongNhanVien>(entity =>
        {
            entity.ToTable("PHANCONGNHANVIEN");
            entity.HasKey(e => e.MaPhaCong).HasName("PK_PHANCONGNHANVIEN");
            entity.Property(e => e.MaPhaCong).HasColumnName("MAPHANCONG");
            entity.Property(e => e.MaCongViec).HasColumnName("MACONGVIEC");
            entity.Property(e => e.MaNhanVien).HasColumnName("MANHANVIEN");
            entity.Property(e => e.NgayBatDauDuKien).HasColumnName("NGAYBATDAUDUKIEN").HasColumnType("datetime2(0)");
            entity.Property(e => e.NgayKetThucdukien).HasColumnName("NGAYKETTHUCDUKIEN").HasColumnType("datetime2(0)");
            entity.Property(e => e.NgayBatDauThucTe).HasColumnName("NGAYBATDAUTHUCTE").HasColumnType("datetime2(0)");
            entity.Property(e => e.NgayKetThucThucTe).HasColumnName("NGAYKETTHUCTHUCTE").HasColumnType("datetime2(0)");
            entity.Property(e => e.PhanTramHoanThanh).HasColumnName("PHANTRAMHOANTHANH").HasColumnType("decimal(5,2)");
            entity.Property(e => e.TrangThai).HasColumnName("TRANGTHAI").HasColumnType("int");
            entity.HasIndex(e => new { e.MaCongViec, e.MaNhanVien }).HasDatabaseName("UQ_PHANCONGNHANVIEN_CV_NV").IsUnique();
            entity.HasOne(e => e.CongViec).WithMany(e => e.PhanCongNhanViens).HasForeignKey(e => e.MaCongViec).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_PHANCONGNHANVIEN_CONGVIEC");
            entity.HasOne(e => e.NhanVien).WithMany(e => e.PhanCongNhanViens).HasForeignKey(e => e.MaNhanVien).HasConstraintName("FK_PHANCONGNHANVIEN_NHANVIEN");
        });

        modelBuilder.Entity<PhanCongNhom>(entity =>
        {
            entity.ToTable("PHANCONGNHOM");
            entity.HasKey(e => new { e.MaCongViec, e.MaNhom }).HasName("PK_PHANCONGNHOM");
            entity.Property(e => e.MaCongViec).HasColumnName("MACONGVIEC");
            entity.Property(e => e.MaNhom).HasColumnName("MANHOM");
            entity.Property(e => e.NgayGiao).HasColumnName("NGAYGIAO").HasColumnType("datetime2(0)");
            entity.Property(e => e.TrangThai).HasColumnName("TRANGTHAI").HasColumnType("int");
            entity.HasOne(e => e.CongViec).WithMany(e => e.PhanCongNhoms).HasForeignKey(e => e.MaCongViec).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_PHANCONGNHOM_CONGVIEC");
            entity.HasOne(e => e.Nhom).WithMany(e => e.PhanCongNhoms).HasForeignKey(e => e.MaNhom).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_PHANCONGNHOM_NHOM");
        });

        modelBuilder.Entity<PhanCongPhongBan>(entity =>
        {
            entity.ToTable("PHANCONGPHONGBAN");
            entity.HasKey(e => new { e.MaPhongBan, e.MaCongViec }).HasName("PK_PHANCONGPHONGBAN");
            entity.Property(e => e.MaPhongBan).HasColumnName("MAPHONGBAN");
            entity.Property(e => e.MaCongViec).HasColumnName("MACONGVIEC");
            entity.Property(e => e.NgayPhanCong).HasColumnName("NGAYPHANCONG").HasColumnType("datetime2(0)");
            entity.Property(e => e.TrangThai).HasColumnName("TRANGTHAI").HasColumnType("int");
            entity.HasOne(e => e.CongViec).WithMany(e => e.PhanCongPhongBans).HasForeignKey(e => e.MaCongViec).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_PHANCONGPHONGBAN_CONGVIEC");
            entity.HasOne(e => e.PhongBan).WithMany(e => e.PhanCongPhongBans).HasForeignKey(e => e.MaPhongBan).HasConstraintName("FK_PHANCONGPHONGBAN_PHONGBAN");
        });

        modelBuilder.Entity<NhatKyCongViec>(entity =>
        {
            entity.ToTable("NHATKYCONGVIEC");
            entity.HasKey(e => e.MaNhatKy).HasName("PK_NHATKYCONGVIEC");
            entity.Property(e => e.MaNhatKy).HasColumnName("MANHATKY");
            entity.Property(e => e.MaCongViec).HasColumnName("MACONGVIEC");
            entity.Property(e => e.MaNhanVien).HasColumnName("MANHANVIEN");
            entity.Property(e => e.PhanTramHoanThanh).HasColumnName("PHANTRAMHOANTHANH").HasColumnType("decimal(5,2)");
            entity.Property(e => e.NgayCapNhat).HasColumnName("NGAYCAPNHAT").HasColumnType("datetime2(0)");
            entity.Property(e => e.GhiChu).HasColumnName("GHICHU").HasMaxLength(300).IsUnicode();
            entity.HasOne(e => e.CongViec).WithMany(e => e.NhatKyCongViecs).HasForeignKey(e => e.MaCongViec).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_NHATKYCONGVIEC_CONGVIEC");
            entity.HasOne(e => e.NhanVien).WithMany(e => e.NhatKyCongViecs).HasForeignKey(e => e.MaNhanVien).HasConstraintName("FK_NHATKYCONGVIEC_NHANVIEN");
        });

        modelBuilder.Entity<CongViecKyNang>(entity =>
        {
            entity.ToTable("CONGVIEC_KYNANG");
            entity.HasKey(e => new { e.MaCongViec, e.MaKyNang }).HasName("PK_CONGVIEC_KYNANG");
            entity.Property(e => e.MaCongViec).HasColumnName("MACONGVIEC");
            entity.Property(e => e.MaKyNang).HasColumnName("MAKYNANG");
            entity.HasOne(e => e.CongViec).WithMany(e => e.CongViecKyNangs).HasForeignKey(e => e.MaCongViec).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_CONGVIEC_KYNANG_CONGVIEC");
            entity.HasOne(e => e.KyNang).WithMany().HasForeignKey(e => e.MaKyNang).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_CONGVIEC_KYNANG_KYNANG");
        });

        modelBuilder.Entity<TienDoCongViec>(entity =>
        {
            entity.ToTable("TIENDOCONGVIEC");
            entity.HasKey(e => e.MaTienDo).HasName("PK_TIENDOCONGVIEC");
            entity.Property(e => e.MaTienDo).HasColumnName("MATIENDO");
            entity.Property(e => e.MaCongViec).HasColumnName("MACONGVIEC");
            entity.Property(e => e.PhanTramHoanThanh).HasColumnName("PHANTRAMHOANTHANH").HasColumnType("decimal(5,2)");
            entity.Property(e => e.TrangThaiHienTai).HasColumnName("TRANGTHAIHIENTAI");
            entity.Property(e => e.NgayCapNhat).HasColumnName("NGAYCAPNHAT").HasColumnType("datetime2(0)");
            entity.Property(e => e.TrangThaiPheDuyet).HasColumnName("TRANGTHAIPHEDUYET").HasMaxLength(50).HasDefaultValue("Chờ duyệt");
            entity.Property(e => e.NguoiPheDuyet).HasColumnName("NGUOIPHEDUYET");
            entity.Property(e => e.NgayPheDuyet).HasColumnName("NGAYPHEDUYET").HasColumnType("datetime2(0)");
            entity.Property(e => e.LyDoTuChoi).HasColumnName("LYDOTUCHOI").HasMaxLength(500);
            entity.HasOne(e => e.CongViec).WithMany(e => e.TienDoCongViecs).HasForeignKey(e => e.MaCongViec).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_TIENDOCONGVIEC_CONGVIEC");
            entity.HasOne(e => e.NguoiPheDuyetNavigation).WithMany().HasForeignKey(e => e.NguoiPheDuyet).OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_TIENDOCONGVIEC_NHANVIEN");
        });
    }

    private static void ConfigureIdentity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("AspNetUsers");
            entity.Property(e => e.Id).HasMaxLength(128);
            entity.Property(e => e.UserName).HasMaxLength(256);
            entity.Property(e => e.NormalizedUserName).HasMaxLength(256);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.NormalizedEmail).HasMaxLength(256);
            entity.HasIndex(e => e.NormalizedUserName).HasDatabaseName("UserNameIndex").IsUnique().HasFilter("[NormalizedUserName] IS NOT NULL");
            entity.HasIndex(e => e.NormalizedEmail).HasDatabaseName("IX_AspNetUsers_NormalizedEmail");
        });

        modelBuilder.Entity<IdentityRole>(entity =>
        {
            entity.ToTable("AspNetRoles");
            entity.Property(e => e.Id).HasMaxLength(128);
            entity.Property(e => e.Name).HasMaxLength(256);
            entity.Property(e => e.NormalizedName).HasMaxLength(256);
            entity.HasIndex(e => e.NormalizedName).HasDatabaseName("RoleNameIndex").IsUnique().HasFilter("[NormalizedName] IS NOT NULL");
        });

        modelBuilder.Entity<IdentityUserClaim<string>>(entity =>
        {
            entity.ToTable("AspNetUserClaims");
            entity.Property(e => e.UserId).HasMaxLength(128);
        });

        modelBuilder.Entity<IdentityUserLogin<string>>(entity =>
        {
            entity.ToTable("AspNetUserLogins");
            entity.Property(e => e.UserId).HasMaxLength(128);
            entity.Property(e => e.LoginProvider).HasMaxLength(128);
            entity.Property(e => e.ProviderKey).HasMaxLength(128);
        });

        modelBuilder.Entity<IdentityUserToken<string>>(entity =>
        {
            entity.ToTable("AspNetUserTokens");
            entity.Property(e => e.UserId).HasMaxLength(128);
            entity.Property(e => e.LoginProvider).HasMaxLength(128);
            entity.Property(e => e.Name).HasMaxLength(128);
        });

        modelBuilder.Entity<IdentityRoleClaim<string>>(entity =>
        {
            entity.ToTable("AspNetRoleClaims");
            entity.Property(e => e.RoleId).HasMaxLength(128);
        });

        modelBuilder.Entity<IdentityUserRole<string>>(entity =>
        {
            entity.ToTable("AspNetUserRoles");
            entity.Property(e => e.UserId).HasMaxLength(128);
            entity.Property(e => e.RoleId).HasMaxLength(128);
        });
    }
}

