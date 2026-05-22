# So do class (Mermaid) - Full CSDL LV2026 (co thuoc tinh + phuong thuc)

```mermaid
%%{init: { "theme": "neutral", "flowchart": { "curve": "linear" } }}%%
classDiagram
direction LR

class __EFMigrationsHistory {
  +MigrationId : [nvarchar](150)
  +ProductVersion : [nvarchar](32)
  +validate() bool
  +toDto() object
}

class _ARCHIVE_AI_HITL_ACTION_20260521 {
  +MADUDOAN : [int]
  +MADANHGIA : [int]
  +MANHANVIEN : [int]
  +LOAI_TAC_DONG : [nvarchar](50)
  +TRANG_THAI_TRUOC : [nvarchar](200)
  +TRANG_THAI_SAU : [nvarchar](200)
  +NOI_DUNG_TRUOC : [nvarchar](max)
  +NOI_DUNG_SAU : [nvarchar](max)
  +NGUOI_DUYET : [int]
  +THOI_DIEM_DUYET : [datetime2](0)
  +SO_LAN_CHINH_SUA : [int]
  +KET_QUA_AP_DUNG : [bit]
  +LY_DO : [nvarchar](500)
  +GHI_CHU : [nvarchar](300)
  +validate() bool
  +toDto() object
}

class _ARCHIVE_THONGBAO_DOITUONG_20260521 {
  +MATHONGBAO : [int]
  +LOAI_DOITUONG : [nvarchar](50)
  +MA_DOITUONG : [int]
  +NGAYTAO : [datetime2](0)
  +validate() bool
  +toDto() object
}

class AI_BUSINESS_KPI_RUN {
  +MAMODEL : [int]
  +MAPHIENBAN : [int]
  +KY_DANHGIA : [nvarchar](50)
  +TU_NGAY : [date]
  +DEN_NGAY : [date]
  +NGAY_TAO : [datetime2](0)
  +TONG_DE_XUAT : [int]
  +SO_DE_XUAT_DUOC_AP_DUNG : [int]
  +SO_DE_XUAT_BI_SUA : [int]
  +SO_DE_XUAT_BI_BAC : [int]
  +GHI_CHU : [nvarchar](300)
  +validate() bool
  +toDto() object
}

class AI_DANHGIA_CHITIET {
  +MADANHGIA : [int]
  +MADUDOAN : [int]
  +MANHANVIEN : [int]
  +MACONGVIEC : [int]
  +MADUAN : [int]
  +NHAN_DU_DOAN : [nvarchar](50)
  +NHAN_THUC_TE : [nvarchar](50)
  +DUNG_NHAN : [bit]
  +DUNG_SO : [bit]
  +GHI_CHU : [nvarchar](300)
  +validate() bool
  +toDto() object
}

class AI_DANHGIA_RUN {
  +MAMODEL : [int]
  +MAPHIENBAN : [int]
  +LOAI_MO_HINH : [nvarchar](50)
  +TU_NGAY : [date]
  +DEN_NGAY : [date]
  +NGAY_DANHGIA : [datetime2](0)
  +TONG_BAN_GHI : [int]
  +TONG_DUNG : [int]
  +TONG_SAI : [int]
  +GHI_CHU : [nvarchar](300)
  +validate() bool
  +toDto() object
}

class AI_FEATURE_IMPORTANCE {
  +MAMODEL : [int]
  +MAPHIENBAN : [int]
  +TEN_FEATURE : [nvarchar](100)
  +XEP_HANG : [int]
  +NGAY_TAO : [datetime2](0)
  +GHI_CHU : [nvarchar](300)
  +validate() bool
  +toDto() object
}

class AI_FEATURE_STORE {
  +MAMODEL : [int]
  +MANHANVIEN : [int]
  +MACONGVIEC : [int]
  +MADUAN : [int]
  +FEATURE_NAME : [nvarchar](100)
  +FEATURE_VALUE : [nvarchar](200)
  +FEATURE_TYPE : [nvarchar](30)
  +SOURCE_TABLE : [nvarchar](50)
  +SOURCE_KEY : [nvarchar](100)
  +VERSION_TAG : [nvarchar](50)
  +DONG_CHOT : [datetime2](0)
  +validate() bool
  +toDto() object
}

class AI_FEEDBACK {
  +MADANHGIA : [int]
  +MADUDOAN : [int]
  +MANHANVIEN : [int]
  +DO_CHINH_XAC : [int]
  +MUC_HUU_ICH : [int]
  +DUNG_SAI : [bit]
  +NOI_DUNG : [nvarchar](500)
  +HANH_DONG_DE_XUAT : [nvarchar](300)
  +NGAY_PHAN_HOI : [datetime2](0)
  +LOAI_PHAN_HOI : [nvarchar](50)
  +TRANGTHAI_XU_LY : [nvarchar](50)
  +IS_AP_DUNG : [bit]
  +NGUOI_XU_LY : [int]
  +NGAY_XU_LY : [datetime2](0)
  +validate() bool
  +toDto() object
}

class AI_NHATKY_CAN_THIEP {
  +MADANHGIA : [int]
  +MADUDOAN : [int]
  +MANHANVIEN : [int]
  +NGUOI_CANTHIEP : [int]
  +ACTION_TYPE : [nvarchar](50)
  +ACTION_SOURCE : [nvarchar](50)
  +LY_DO : [nvarchar](500)
  +GIA_TRI_CU : [nvarchar](max)
  +GIA_TRI_MOI : [nvarchar](max)
  +NGUON_CANTHIEP : [nvarchar](50)
  +NGAY_CAN_THIEP : [datetime2](0)
  +SO_LAN_CHINH_SUA : [int]
  +validate() bool
  +toDto() object
}

class AspNetRoleClaims {
  +RoleId : [nvarchar](128)
  +ClaimType : [nvarchar](max)
  +ClaimValue : [nvarchar](max)
  +validate() bool
  +toDto() object
}

class AspNetRoles {
  +Id : [nvarchar](128)
  +Name : [nvarchar](256)
  +NormalizedName : [nvarchar](256)
  +ConcurrencyStamp : [nvarchar](max)
  +validate() bool
  +toDto() object
}

class AspNetUserClaims {
  +UserId : [nvarchar](128)
  +ClaimType : [nvarchar](max)
  +ClaimValue : [nvarchar](max)
  +validate() bool
  +toDto() object
}

class AspNetUserLogins {
  +LoginProvider : [nvarchar](128)
  +ProviderKey : [nvarchar](128)
  +ProviderDisplayName : [nvarchar](max)
  +UserId : [nvarchar](128)
  +validate() bool
  +toDto() object
}

class AspNetUserRoles {
  +UserId : [nvarchar](128)
  +RoleId : [nvarchar](128)
  +validate() bool
  +toDto() object
}

class AspNetUsers {
  +Id : [nvarchar](128)
  +UserName : [nvarchar](256)
  +NormalizedUserName : [nvarchar](256)
  +Email : [nvarchar](256)
  +NormalizedEmail : [nvarchar](256)
  +EmailConfirmed : [bit]
  +PasswordHash : [nvarchar](max)
  +SecurityStamp : [nvarchar](max)
  +ConcurrencyStamp : [nvarchar](max)
  +PhoneNumber : [nvarchar](max)
  +PhoneNumberConfirmed : [bit]
  +TwoFactorEnabled : [bit]
  +LockoutEnd : [datetimeoffset](7)
  +LockoutEnabled : [bit]
  +AccessFailedCount : [int]
  +validate() bool
  +toDto() object
}

class AspNetUserTokens {
  +UserId : [nvarchar](128)
  +LoginProvider : [nvarchar](128)
  +Name : [nvarchar](128)
  +Value : [nvarchar](max)
  +validate() bool
  +toDto() object
}

class BAOCAO_PORTAL {
  +TENBAOCAO : [nvarchar](200)
  +LOAIBAOCAO : [nvarchar](50)
  +MADUAN : [int]
  +MAPHONGBAN : [int]
  +NGUOITAO : [nvarchar](128)
  +NGAYTAO : [datetime2](0)
  +NGAYCAPNHAT : [datetime2](0)
  +NGAYBATDAU : [datetime2](0)
  +NGAYKETTHUC : [datetime2](0)
  +DINH_DANG : [nvarchar](50)
  +TRANGTHAI : [nvarchar](50)
  +NOIDUNG : [nvarchar](max)
  +ISDELETED : [bit]
  +validate() bool
  +toDto() object
}

class BAOCAOCHITIET_PORTAL {
  +MABAOCAO : [int]
  +TIEUDE : [nvarchar](200)
  +DULIEU : [nvarchar](max)
  +THUTUU : [int]
  +validate() bool
  +toDto() object
}

class CHUCVU {
  +TENCHUCVU : [nvarchar](50)
  +validate() bool
  +toDto() object
}

class CONGVIEC {
  +MADOUUTIEN : [int]
  +MADOKHO : [int]
  +MATRANGTHAI : [int]
  +MADUAN : [int]
  +MACONGVIECCHA : [int]
  +TENCONGVIEC : [nvarchar](50)
  +MOTA : [nvarchar](300)
  +HANHOANTHANH : [datetime2](0)
  +NGAYBATDAU : [datetime2](0)
  +NGAYTAO : [datetime2](0)
  +NGUOITAO : [nvarchar](128)
  +NGAYCAPNHAT : [datetime2](0)
  +NGUOICAPNHAT : [nvarchar](128)
  +DAXOA : [bit]
  +validate() bool
  +toDto() object
}

class CONGVIEC_KYNANG {
  +MACONGVIEC : [int]
  +MAKYNANG : [int]
  +CAPDO_YEUCAU : [int]
  +validate() bool
  +toDto() object
}

class DANHMUCKPI {
  +MALOAIKPI : [int]
  +TENKPI : [nvarchar](50)
  +TRANGTHAI : [int]
  +validate() bool
  +toDto() object
}

class DE_XUAT_KPI {
  +MAKPI : [int]
  +MALOAIKPI : [int]
  +NGUOIDE_XUAT : [int]
  +NGUOIDUYET : [int]
  +NGUOICAPNHAT : [int]
  +LOAI_DEXUAT : [nvarchar](50)
  +MANHANVIEN_APDUNG : [int]
  +MANHOM_APDUNG : [int]
  +MAPHONGBAN_APDUNG : [int]
  +MADUAN_APDUNG : [int]
  +TU_NGAY : [date]
  +DEN_NGAY : [date]
  +TENKPI_DEXUAT : [nvarchar](150)
  +MOTA_KPI_DEXUAT : [nvarchar](500)
  +LYDO : [nvarchar](500)
  +TRANGTHAI : [nvarchar](50)
  +PHANHOI_ADMIN : [nvarchar](500)
  +GHI_CHU : [nvarchar](300)
  +NGAYTAO : [datetime2](0)
  +NGAYCAPNHAT : [datetime2](0)
  +NGAYDUYET : [datetime2](0)
  +validate() bool
  +toDto() object
}

class DOKHO {
  +TENDOKHO : [nvarchar](50)
  +ISACTIVE : [bit]
  +validate() bool
  +toDto() object
}

class DOUUTIEN {
  +TENDOUUTIEN : [nvarchar](50)
  +ISACTIVE : [bit]
  +validate() bool
  +toDto() object
}

class DUAN {
  +TENDUAN : [nvarchar](50)
  +MOTA : [nvarchar](300)
  +NGAYBATDAU : [datetime2](0)
  +NGAYKETTHUC : [datetime2](0)
  +TRANGTHAI : [int]
  +validate() bool
  +toDto() object
}

class DUAN_NHANVIEN {
  +MADUAN : [int]
  +MANHANVIEN : [int]
  +VAITRO : [nvarchar](100)
  +NGAYTHAMGIA : [datetime2](0)
  +NGAYROI : [datetime2](0)
  +TRANGTHAI : [tinyint]
  +validate() bool
  +toDto() object
}

class DUAN_NHOM {
  +MADUAN : [int]
  +MANHOM : [int]
  +NGAYTHAMGIA : [datetime2](0)
  +TRANGTHAI : [tinyint]
  +validate() bool
  +toDto() object
}

class DUAN_PHONGBAN {
  +MADUAN : [int]
  +MAPHONGBAN : [int]
  +NGAYTHAMGIA : [datetime2](0)
  +TRANGTHAI : [tinyint]
  +validate() bool
  +toDto() object
}

class DUDOANAI {
  +MANHANVIEN : [int]
  +MAMODEL : [int]
  +THANG : [int]
  +NAM : [int]
  +DEXUATCAITHIEN : [nvarchar](300)
  +GOIYNGUONLUC : [nvarchar](300)
  +THOIGIANDUDOAN : [datetime2](0)
  +ACTOR : [nvarchar](128)
  +INPUTDATA : [nvarchar](max)
  +MODELNAME : [nvarchar](100)
  +OUTPUTDATA : [nvarchar](max)
  +TRANGTHAI_DUYET : [nvarchar](50)
  +DUOC_AP_DUNG : [bit]
  +NGUOI_DUYET : [int]
  +THOI_DIEM_DUYET : [datetime2](0)
  +SO_LAN_CHINH_SUA : [int]
  +validate() bool
  +toDto() object
}

class DULIEUAI {
  +MANHANVIEN : [int]
  +TONGCONGVIECDANGLAM : [int]
  +SOCONGVIECHOANTHANH : [int]
  +SOCONGVIECTREHAN : [int]
  +validate() bool
  +toDto() object
}

class KETQUAKPI {
  +MANHANVIEN : [int]
  +MAKPI : [int]
  +THANG : [int]
  +NAM : [int]
  +validate() bool
  +toDto() object
}

class KETQUAKPI_TONG {
  +MANHANVIEN : [int]
  +THANG : [int]
  +NAM : [int]
  +XEPLOAI : [nvarchar](50)
  +SOKPI_THANHPHAN : [int]
  +NGAYTINH : [datetime2](0)
  +validate() bool
  +toDto() object
}

class KPI_DUAN {
  +MAKPI : [int]
  +MADUAN : [int]
  +TU_NGAY : [date]
  +DEN_NGAY : [date]
  +TRANGTHAI : [tinyint]
  +GHI_CHU : [nvarchar](300)
  +IS_ACTIVE : [bit]
  +NGAYKETTHUC_APDUNG : [datetime2](0)
  +validate() bool
  +toDto() object
}

class KPI_NHANVIEN {
  +MAKPI : [int]
  +MANHANVIEN : [int]
  +TU_NGAY : [date]
  +DEN_NGAY : [date]
  +TRANGTHAI : [tinyint]
  +GHI_CHU : [nvarchar](300)
  +IS_ACTIVE : [bit]
  +NGAYKETTHUC_APDUNG : [datetime2](0)
  +validate() bool
  +toDto() object
}

class KPI_NHOM {
  +MAKPI : [int]
  +MANHOM : [int]
  +TU_NGAY : [date]
  +DEN_NGAY : [date]
  +TRANGTHAI : [tinyint]
  +GHI_CHU : [nvarchar](300)
  +IS_ACTIVE : [bit]
  +NGAYKETTHUC_APDUNG : [datetime2](0)
  +validate() bool
  +toDto() object
}

class KPI_PHONGBAN {
  +MAKPI : [int]
  +MAPHONGBAN : [int]
  +TU_NGAY : [date]
  +DEN_NGAY : [date]
  +TRANGTHAI : [tinyint]
  +GHI_CHU : [nvarchar](300)
  +IS_ACTIVE : [bit]
  +NGAYKETTHUC_APDUNG : [datetime2](0)
  +validate() bool
  +toDto() object
}

class KPI_XEPLOAI {
  +CODE : [nvarchar](50)
  +LABEL : [nvarchar](100)
  +MOTA : [nvarchar](500)
  +COLORHEX : [nvarchar](20)
  +SORTORDER : [int]
  +ISACTIVE : [bit]
  +ISSYSTEM : [bit]
  +CREATEDAT : [datetime2](0)
  +UPDATEDAT : [datetime2](0)
  +validate() bool
  +toDto() object
}

class KYNANG {
  +TENKYNANG : [nvarchar](300)
  +MOTA : [nvarchar](300)
  +TRANGTHAI : [int]
  +validate() bool
  +toDto() object
}

class KYNANGNHANVIEN {
  +MAKYNANG : [int]
  +MANHANVIEN : [int]
  +CAPDO : [int]
  +SODUANDADUNG : [int]
  +validate() bool
  +toDto() object
}

class LICHSUTRANGTHAICONGVIEC {
  +MACONGVIEC : [int]
  +TRANGTHAICU : [int]
  +TRANGTHAIMOI : [int]
  +THOIGIAN : [datetime2](0)
  +validate() bool
  +toDto() object
}

class LOAIKPI {
  +TENLOAIKPI : [nvarchar](50)
  +ISACTIVE : [bit]
  +validate() bool
  +toDto() object
}

class LOAITHONGBAO {
  +TENLOAI : [nvarchar](50)
  +validate() bool
  +toDto() object
}

class LOG_AI {
  +MAMODEL : [int]
  +LOAI_SUKIEN : [nvarchar](30)
  +KET_QUA : [nvarchar](100)
  +THOI_GIAN : [datetime2](0)
  +NOI_DUNG : [nvarchar](500)
  +validate() bool
  +toDto() object
}

class MOHINH_DULIEUAI {
  +MAMODEL : [int]
  +MADULIEU : [int]
  +MUC_DICH : [nvarchar](30)
  +NGAY_SU_DUNG : [datetime2](0)
  +validate() bool
  +toDto() object
}

class MOHINHAI {
  +TENMODEL : [nvarchar](50)
  +VERSION : [nvarchar](50)
  +NGAYTRAIN : [datetime2](0)
  +validate() bool
  +toDto() object
}

class NHANVIEN {
  +MAPHONGBAN : [int]
  +PHO_MAPHONGBAN : [int]
  +HOTEN : [nvarchar](50)
  +NGAYSINH : [datetime2](0)
  +CCCD : [varchar](12)
  +DIACHI : [nvarchar](50)
  +GIOITINH : [nvarchar](10)
  +EMAIL : [varchar](100)
  +SDT : [varchar](10)
  +NGAYVAOLAM : [datetime2](0)
  +TRANGTHAI : [int]
  +AspNetUserId : [nvarchar](128)
  +MACHUCVU : [int]
  +validate() bool
  +toDto() object
}

class NHATKYCONGVIEC {
  +MACONGVIEC : [int]
  +GHICHU : [nvarchar](300)
  +NGAYCAPNHAT : [datetime2](0)
  +HANHDONG : [nvarchar](300)
  +MANHANVIEN : [int]
  +NGAYTAO : [datetime2](0)
  +NOIDUNG : [nvarchar](500)
  +validate() bool
  +toDto() object
}

class NHATKYHOATDONG {
  +MANHANVIEN : [int]
  +HANHDONG : [nvarchar](300)
  +THOIGIAN : [datetime2](0)
  +DOITUONG : [varchar](100)
  +DULIEUCU : [nvarchar](max)
  +DULIEUMOI : [nvarchar](max)
  +IP : [varchar](64)
  +TRANGTHAI : [varchar](30)
  +validate() bool
  +toDto() object
}

class NHOM {
  +TENNHOM : [nvarchar](50)
  +NGAYTAO : [datetime2](0)
  +TRUONGNHOM : [int]
  +validate() bool
  +toDto() object
}

class PHANCONGNHANVIEN {
  +MACONGVIEC : [int]
  +MANHANVIEN : [int]
  +NGAYGIAO : [datetime2](0)
  +NGAYBATDAUDUKIEN : [datetime2](0)
  +NGAYKETTHUCDUKIEN : [datetime2](0)
  +NGAYBATDAUTHUCTE : [datetime2](0)
  +NGAYKETTHUCTHUCTE : [datetime2](0)
  +TRANGTHAI : [int]
  +validate() bool
  +toDto() object
}

class PHANCONGNHOM {
  +MACONGVIEC : [int]
  +MANHOM : [int]
  +NGAYGIAO : [datetime2](0)
  +TRANGTHAI : [int]
  +validate() bool
  +toDto() object
}

class PHANCONGPHONGBAN {
  +MAPHONGBAN : [int]
  +MACONGVIEC : [int]
  +NGAYPHANCONG : [datetime2](0)
  +TRANGTHAI : [int]
  +validate() bool
  +toDto() object
}

class PHIENBAN_MOHINH {
  +MAMODEL : [int]
  +PHIEN_BAN : [nvarchar](50)
  +NGAY_TAO : [datetime2](0)
  +GHI_CHU : [nvarchar](300)
  +validate() bool
  +toDto() object
}

class PHONGBAN {
  +TENPHONGBAN : [nvarchar](50)
  +MOTA : [nvarchar](300)
  +MATRUONGPHONG : [int]
  +validate() bool
  +toDto() object
}

class THAMSOAI {
  +TEN_THAMSO : [nvarchar](100)
  +GIATRI : [nvarchar](max)
  +MOTA : [nvarchar](300)
  +MAMODEL : [int]
  +NGAY_TAO : [datetime2](0)
  +NGAY_CAPNHAT : [datetime2](0)
  +validate() bool
  +toDto() object
}

class THANHVIENNHOM {
  +MANHANVIEN : [int]
  +MANHOM : [int]
  +NGAYGIANHAP : [datetime2](0)
  +VAITROTRONGNHOM : [nvarchar](300)
  +validate() bool
  +toDto() object
}

class THONGBAO {
  +MALOAI : [int]
  +NOIDUNG : [nvarchar](300)
  +THOIGIAN : [datetime2](0)
  +validate() bool
  +toDto() object
}

class THONGBAO_NHANVIEN {
  +MANHANVIEN : [int]
  +MATHONGBAO : [int]
  +DADOC : [bit]
  +validate() bool
  +toDto() object
}

class TIENDOCONGVIEC {
  +MACONGVIEC : [int]
  +TRANGTHAIHIENTAI : [int]
  +NGAYCAPNHAT : [datetime2](0)
  +TRANGTHAIPHEDUYET : [nvarchar](50)
  +NGUOIPHEDUYET : [int]
  +NGAYPHEDUYET : [datetime2](7)
  +LYDOTUCHOI : [nvarchar](500)
  +validate() bool
  +toDto() object
}

class TRANGTHAICONGVIEC {
  +TENTRANGTHAI : [nvarchar](50)
  +validate() bool
  +toDto() object
}

class YEUCAU_CAPNHAT_HOSO {
  +MANHANVIEN : [int]
  +TRANGTHAI : [varchar](30)
  +DANHSACH_TRUONG : [varchar](200)
  +DULIEU_CU_JSON : [nvarchar](max)
  +DULIEU_MOI_JSON : [nvarchar](max)
  +LYDO_GUI : [nvarchar](500)
  +LYDO_TUCHOI : [nvarchar](500)
  +GHICHU_DUYET : [nvarchar](500)
  +NGUOITAO : [int]
  +NGUOIDUYET : [int]
  +NGUOICAPNHAT : [int]
  +NGAYTAO : [datetime2](0)
  +NGAYDUYET : [datetime2](0)
  +NGAYCAPNHAT : [datetime2](0)
  +IP_TAO : [varchar](64)
  +IP_DUYET : [varchar](64)
  +ISDELETED : [bit]
  +validate() bool
  +toDto() object
}

class YEUCAUBAOCAO {
  +NGUOIYEUCAU : [nvarchar](128)
  +NGUOINHAN : [nvarchar](128)
  +TIEUDE : [nvarchar](200)
  +MOTA : [nvarchar](max)
  +PRIORITY : [nvarchar](50)
  +HANCHOT : [datetime2](7)
  +TRANGTHAI : [nvarchar](50)
  +NGAYTAO : [datetime2](7)
  +NGAYCAPNHAT : [datetime2](7)
  +ISDELETED : [bit]
  +validate() bool
  +toDto() object
}

%% Quan he chinh (core relationships)
AspNetUsers "1" --> "0..1" NHANVIEN : AspNetUserId
AspNetUsers "1" --> "0..*" AspNetUserRoles
AspNetRoles "1" --> "0..*" AspNetUserRoles
AspNetUsers "1" --> "0..*" AspNetUserClaims
AspNetRoles "1" --> "0..*" AspNetRoleClaims
AspNetUsers "1" --> "0..*" AspNetUserLogins
AspNetUsers "1" --> "0..*" AspNetUserTokens

CHUCVU "1" --> "0..*" NHANVIEN
PHONGBAN "1" --> "0..*" NHANVIEN
NHANVIEN "1" --> "0..*" PHONGBAN : truong/pho phong
NHANVIEN "1" --> "0..*" NHOM : truong nhom
NHOM "1" --> "0..*" THANHVIENNHOM
NHANVIEN "1" --> "0..*" THANHVIENNHOM

DUAN "1" --> "0..*" CONGVIEC
CONGVIEC "1" --> "0..*" CONGVIEC : parent-child
DOKHO "1" --> "0..*" CONGVIEC
DOUUTIEN "1" --> "0..*" CONGVIEC
TRANGTHAICONGVIEC "1" --> "0..*" CONGVIEC

CONGVIEC "1" --> "0..*" PHANCONGNHANVIEN
NHANVIEN "1" --> "0..*" PHANCONGNHANVIEN
CONGVIEC "1" --> "0..*" PHANCONGNHOM
NHOM "1" --> "0..*" PHANCONGNHOM
CONGVIEC "1" --> "0..*" PHANCONGPHONGBAN
PHONGBAN "1" --> "0..*" PHANCONGPHONGBAN

LOAIKPI "1" --> "0..*" DANHMUCKPI
DANHMUCKPI "1" --> "0..*" KETQUAKPI
NHANVIEN "1" --> "0..*" KETQUAKPI

MOHINHAI "1" --> "0..*" DUDOANAI
NHANVIEN "1" --> "0..*" DUDOANAI
MOHINHAI "1" --> "0..*" AI_DANHGIA_RUN
AI_DANHGIA_RUN "1" --> "0..*" AI_DANHGIA_CHITIET

LOAITHONGBAO "1" --> "0..*" THONGBAO
THONGBAO "1" --> "0..*" THONGBAO_NHANVIEN
NHANVIEN "1" --> "0..*" THONGBAO_NHANVIEN
BAOCAO_PORTAL "1" --> "0..*" BAOCAOCHITIET_PORTAL
```
