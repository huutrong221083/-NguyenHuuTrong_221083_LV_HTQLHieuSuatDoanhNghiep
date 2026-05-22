# So do lop (Class Diagram) - LV2026

Ban nhan xet dung: ban truoc bi rut gon qua nhieu. Theo `AppDbContext` hien tai co **48 DbSet**, cong them cac bang Identity (`AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetRoleClaims`, `AspNetUserLogins`, `AspNetUserTokens`) thi tong so bang thuc te lon hon dang ke.

## 1) Ky hieu truy cap UML

- `+` : public / cong khai
- `-` : private / rieng tu
- `#` : protected / bao ve
- `~` : internal / noi bo

## 2) Vi sao thay nhieu dau `+`?

Dieu nay la dung voi code C# EF Core hien tai:
- Cac **entity model** (trong `Models`) gan nhu deu dung `public` property de EF map cot/bang.
- Vi vay tren class diagram cua **entity/database layer**, ky hieu `+` se chiem da so.
- Dau `-` va `#` xuat hien ro nhat o lop ha tang nhu `AppDbContext`:
  - `#OnModelCreating(...)`
  - `-ConfigureIdentity(...)`

## 3) So do class theo module (day du hon)

### 3.1 Identity + Nhan su
```plantuml
@startuml
skinparam classAttributeIconSize 0

class AppDbContext <<infrastructure>> {
  +NhanViens: DbSet<NHANVIEN>
  +PhongBans: DbSet<PHONGBAN>
  +Nhoms: DbSet<NHOM>
  +YeuCauCapNhatHoSos: DbSet<YEUCAU_CAPNHAT_HOSO>
  #OnModelCreating(modelBuilder: ModelBuilder): void
  -ConfigureIdentity(modelBuilder: ModelBuilder): void
}

class AspNetUsers <<entity>> {
  +Id: string <<PK>>
  +UserName: string
  +Email: string
  +PasswordHash: string
  +LockoutEnd: DateTimeOffset?
  +dangNhap(userNameOrEmail: string, password: string): bool
}

class AspNetRoles <<entity>> {
  +Id: string <<PK>>
  +Name: string
  +NormalizedName: string
  +capNhatTenRole(tenMoi: string): void
}

class AspNetUserRoles <<entity>> {
  +UserId: string <<PK,FK>>
  +RoleId: string <<PK,FK>>
}

class AspNetUserClaims <<entity>> {
  +Id: int <<PK>>
  +UserId: string <<FK>>
  +ClaimType: string
  +ClaimValue: string
}

class AspNetRoleClaims <<entity>> {
  +Id: int <<PK>>
  +RoleId: string <<FK>>
  +ClaimType: string
  +ClaimValue: string
}

class AspNetUserLogins <<entity>> {
  +LoginProvider: string <<PK>>
  +ProviderKey: string <<PK>>
  +UserId: string <<FK>>
}

class AspNetUserTokens <<entity>> {
  +UserId: string <<PK,FK>>
  +LoginProvider: string <<PK>>
  +Name: string <<PK>>
  +Value: string
}

class CHUCVU <<entity>> {
  +MaChucVu: int <<PK>>
  +TenChucVu: string
}

class PHONGBAN <<entity>> {
  +MaPhongBan: int <<PK>>
  +TenPhongBan: string
  +MoTa: string
  +MaTruongPhong: int <<FK>>
  +doiTruongPhong(maNhanVien: int): void
}

class NHANVIEN <<entity>> {
  +MaNhanVien: int <<PK>>
  +MaPhongBan: int <<FK>>
  +PhoMaPhongBan: int <<FK>>
  +MaChucVu: int <<FK>>
  +AspNetUserId: string <<FK>>
  +HoTen: string
  +Email: string
  +Sdt: string
  +TrangThai: int
  +capNhatThongTin(): void
}

class NHOM <<entity>> {
  +MaNhom: int <<PK>>
  +TenNhom: string
  +TruongNhom: int <<FK>>
}

class THANHVIENNHOM <<entity>> {
  +MaNhanVien: int <<PK,FK>>
  +MaNhom: int <<PK,FK>>
  +NgayGiaNhap: DateTime
  +VaiTroTrongNhom: string
}

class YEUCAU_CAPNHAT_HOSO <<entity>> {
  +MaYeuCau: int <<PK>>
  +MaNhanVien: int <<FK>>
  +TrangThai: string
  +NguoiDuyet: int <<FK>>
  +NgayTao: DateTime
  +duyet(nguoiDuyet: int): void
  +tuChoi(lyDo: string): void
}

AspNetUsers "1" --> "0..1" NHANVIEN
CHUCVU "1" --> "0..*" NHANVIEN
PHONGBAN "1" --> "0..*" NHANVIEN
NHANVIEN "1" --> "0..*" PHONGBAN : truong/pho phong
NHANVIEN "1" --> "0..*" NHOM : truong nhom
NHOM "1" --> "0..*" THANHVIENNHOM
NHANVIEN "1" --> "0..*" THANHVIENNHOM
NHANVIEN "1" --> "0..*" YEUCAU_CAPNHAT_HOSO : nguoi tao/duyet
AspNetUsers "1" --> "0..*" AspNetUserClaims
AspNetUsers "1" --> "0..*" AspNetUserRoles
AspNetUsers "1" --> "0..*" AspNetUserLogins
AspNetUsers "1" --> "0..*" AspNetUserTokens
AspNetRoles "1" --> "0..*" AspNetUserRoles
AspNetRoles "1" --> "0..*" AspNetRoleClaims
AppDbContext ..> NHANVIEN
AppDbContext ..> PHONGBAN
AppDbContext ..> NHOM
AppDbContext ..> YEUCAU_CAPNHAT_HOSO
@enduml
```

### 3.2 Du an - Cong viec - Phan cong
```plantuml
@startuml
hide methods
skinparam classAttributeIconSize 0

class DUAN
class CONGVIEC
class DOKHO
class DOUUTIEN
class TRANGTHAICONGVIEC
class PHANCONGNHANVIEN
class PHANCONGNHOM
class PHANCONGPHONGBAN
class TIENDOCONGVIEC
class NHATKYCONGVIEC
class LICHSUTRANGTHAICONGVIEC
class DUAN_NHANVIEN
class DUAN_NHOM
class DUAN_PHONGBAN
class CONGVIEC_KYNANG
class KYNANG
class KYNANGNHANVIEN
class NHANVIEN
class NHOM
class PHONGBAN

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

CONGVIEC "1" --> "0..*" TIENDOCONGVIEC
NHANVIEN "1" --> "0..*" TIENDOCONGVIEC : nguoi phe duyet
TRANGTHAICONGVIEC "1" --> "0..*" TIENDOCONGVIEC
CONGVIEC "1" --> "0..*" NHATKYCONGVIEC
CONGVIEC "1" --> "0..*" LICHSUTRANGTHAICONGVIEC

DUAN "1" --> "0..*" DUAN_NHANVIEN
NHANVIEN "1" --> "0..*" DUAN_NHANVIEN
DUAN "1" --> "0..*" DUAN_NHOM
NHOM "1" --> "0..*" DUAN_NHOM
DUAN "1" --> "0..*" DUAN_PHONGBAN
PHONGBAN "1" --> "0..*" DUAN_PHONGBAN

CONGVIEC "1" --> "0..*" CONGVIEC_KYNANG
KYNANG "1" --> "0..*" CONGVIEC_KYNANG
NHANVIEN "1" --> "0..*" KYNANGNHANVIEN
KYNANG "1" --> "0..*" KYNANGNHANVIEN
@enduml
```

### 3.3 KPI
```plantuml
@startuml
hide methods
skinparam classAttributeIconSize 0

class LOAIKPI
class DANHMUCKPI
class DE_XUAT_KPI
class KETQUAKPI
class KETQUAKPI_TONG
class KPI_NHANVIEN
class KPI_NHOM
class KPI_PHONGBAN
class KPI_DUAN
class KPI_XEPLOAI
class NHANVIEN
class NHOM
class PHONGBAN
class DUAN

LOAIKPI "1" --> "0..*" DANHMUCKPI
DANHMUCKPI "1" --> "0..*" DE_XUAT_KPI
LOAIKPI "1" --> "0..*" DE_XUAT_KPI

DANHMUCKPI "1" --> "0..*" KETQUAKPI
NHANVIEN "1" --> "0..*" KETQUAKPI
NHANVIEN "1" --> "0..*" KETQUAKPI_TONG

DANHMUCKPI "1" --> "0..*" KPI_NHANVIEN
NHANVIEN "1" --> "0..*" KPI_NHANVIEN
DANHMUCKPI "1" --> "0..*" KPI_NHOM
NHOM "1" --> "0..*" KPI_NHOM
DANHMUCKPI "1" --> "0..*" KPI_PHONGBAN
PHONGBAN "1" --> "0..*" KPI_PHONGBAN
DANHMUCKPI "1" --> "0..*" KPI_DUAN
DUAN "1" --> "0..*" KPI_DUAN

DE_XUAT_KPI "0..*" --> "0..1" NHANVIEN : nguoi de xuat/duyet/cap nhat/ap dung
DE_XUAT_KPI "0..*" --> "0..1" NHOM : ap dung
DE_XUAT_KPI "0..*" --> "0..1" PHONGBAN : ap dung
DE_XUAT_KPI "0..*" --> "0..1" DUAN : ap dung
@enduml
```

### 3.4 AI + Mo hinh
```plantuml
@startuml
hide methods
skinparam classAttributeIconSize 0

class MOHINHAI
class PHIENBAN_MOHINH
class THAMSOAI
class DULIEUAI
class MOHINH_DULIEUAI
class DUDOANAI
class AI_FEATURE_STORE
class AI_FEATURE_IMPORTANCE
class AI_DANHGIA_RUN
class AI_DANHGIA_CHITIET
class AI_FEEDBACK
class AI_NHATKY_CAN_THIEP
class AI_BUSINESS_KPI_RUN
class LOG_AI
class NHANVIEN
class CONGVIEC
class DUAN

MOHINHAI "1" --> "0..*" PHIENBAN_MOHINH
MOHINHAI "1" --> "0..*" THAMSOAI
MOHINHAI "1" --> "0..*" LOG_AI

MOHINHAI "1" --> "0..*" DUDOANAI
NHANVIEN "1" --> "0..*" DUDOANAI

MOHINHAI "1" --> "0..*" AI_FEATURE_STORE
NHANVIEN "1" --> "0..*" AI_FEATURE_STORE
CONGVIEC "1" --> "0..*" AI_FEATURE_STORE
DUAN "1" --> "0..*" AI_FEATURE_STORE

MOHINHAI "1" --> "0..*" AI_FEATURE_IMPORTANCE
PHIENBAN_MOHINH "1" --> "0..*" AI_FEATURE_IMPORTANCE

MOHINHAI "1" --> "0..*" AI_DANHGIA_RUN
PHIENBAN_MOHINH "1" --> "0..*" AI_DANHGIA_RUN
AI_DANHGIA_RUN "1" --> "0..*" AI_DANHGIA_CHITIET
DUDOANAI "1" --> "0..*" AI_DANHGIA_CHITIET
NHANVIEN "1" --> "0..*" AI_DANHGIA_CHITIET
CONGVIEC "1" --> "0..*" AI_DANHGIA_CHITIET
DUAN "1" --> "0..*" AI_DANHGIA_CHITIET

AI_DANHGIA_RUN "1" --> "0..*" AI_FEEDBACK
DUDOANAI "1" --> "0..*" AI_FEEDBACK
NHANVIEN "1" --> "0..*" AI_FEEDBACK : nguoi gui/xu ly

DUDOANAI "1" --> "0..*" AI_NHATKY_CAN_THIEP
AI_DANHGIA_RUN "1" --> "0..*" AI_NHATKY_CAN_THIEP
NHANVIEN "1" --> "0..*" AI_NHATKY_CAN_THIEP

MOHINHAI "1" --> "0..*" AI_BUSINESS_KPI_RUN
PHIENBAN_MOHINH "1" --> "0..*" AI_BUSINESS_KPI_RUN

DULIEUAI "1" --> "0..*" MOHINH_DULIEUAI
MOHINHAI "1" --> "0..*" MOHINH_DULIEUAI
NHANVIEN "1" --> "0..*" DULIEUAI
@enduml
```

### 3.5 Thong bao - Bao cao - Nhat ky
```plantuml
@startuml
hide methods
skinparam classAttributeIconSize 0

class LOAITHONGBAO
class THONGBAO
class THONGBAO_NHANVIEN
class NHANVIEN
class BAOCAO_PORTAL
class BAOCAOCHITIET_PORTAL
class YEUCAUBAOCAO
class AspNetUsers
class NHATKYHOATDONG

LOAITHONGBAO "1" --> "0..*" THONGBAO
THONGBAO "1" --> "0..*" THONGBAO_NHANVIEN
NHANVIEN "1" --> "0..*" THONGBAO_NHANVIEN

BAOCAO_PORTAL "1" --> "0..*" BAOCAOCHITIET_PORTAL
AspNetUsers "1" --> "0..*" YEUCAUBAOCAO : nguoi yeu cau / nguoi nhan
NHANVIEN "1" --> "0..*" NHATKYHOATDONG
@enduml
```

## 4) Mau the hien co `+ - # ~` (vi du lop C#)

```plantuml
@startuml
class ViDuVisibility {
  +PublicMethod() : void
  -privateField : string
  #ProtectedMethod() : void
  ~InternalHelper() : void
}
@enduml
```
