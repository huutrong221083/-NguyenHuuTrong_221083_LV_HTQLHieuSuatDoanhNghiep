@startuml
skinparam classAttributeIconSize 0

class AspNetUsers <<table>> {
  +Id : string <<PK>>
  +UserName : string
  +Email : string
  --
  +GetById(id: string)
  +GetAll()
  +CreateUser(userName: string, email: string)
  +AssignRole(roleId: string)
  +Lock()
  +Unlock()
}

class AspNetRoles <<table>> {
  +Id : string <<PK>>
  +Name : string
  --
  +GetAll()
  +CreateRole(name: string)
  +UpdateRole(id: string, name: string)
  +DeleteRole(id: string)
}

class AspNetUserRoles <<table>> {
  +UserId : string <<PK,FK>>
  +RoleId : string <<PK,FK>>
  --
  +AssignUserRole(userId: string, roleId: string)
  +RemoveUserRole(userId: string, roleId: string)
}

class PhongBan <<table>> {
  +MaPhongBan : int <<PK>>
  +MaNhanVien : int <<FK>>
  +TenPhongBan : string
  +MoTa : string
  +MaTruongPhong : int
  --
  +GetPhongBans()
  +CreatePhongBan()
  +UpdatePhongBan(id: int)
  +DeletePhongBan(id: int)
  +GetNhanVienByPhongBan(id: int)
}

class NhanVien <<table>> {
  +MaNhanVien : int <<PK>>
  +MaPhongBan : int <<FK>>
  +PhoMaPhongBan : int <<FK>>
  +AspNetUserId : string <<FK>>
  +HoTên : string
  +NgaySinh : datetime
  +Email : string
  +Sdt : string
  +NgayVaoLam : datetime
  +TrangThai : int
  --
  +GetNhanViens()
  +GetNhanVienById(id: int)
  +CreateNhanVien()
  +UpdateNhanVien(id: int)
  +UpdateStatus(id: int)
  +SoftDeleteNhanVien(id: int)
  +AddSkill(id: int)
  +UpdateSkill(id: int, skillId: int)
  +RemoveSkill(id: int, skillId: int)
}

class DuAn <<table>> {
  +MaDuAn : int <<PK>>
  +TenDuAn : string
  +MoTa : string
  +NgayBatDau : datetime
  +NgayKetThuc : datetime
  +TrangThai : int
  --
  +GetDuAns()
  +GetDuAnById(id: int)
  +CreateDuAn()
  +UpdateDuAn(id: int)
  +DeleteDuAn(id: int)
}

class DoUuTien <<table>> {
  +MaDoUuTien : int <<PK>>
  +TenDoUuTien : string
  --
  +GetAll()
}

class DoKho <<table>> {
  +MaDoKho : int <<PK>>
  +TenDoKho : string
  --
  +GetAll()
}

class TrangThaiCongViec <<table>> {
  +MaTrangThai : int <<PK>>
  +TenTrangThai : string
  --
  +GetAll()
}

class CongViec <<table>> {
  +MaCongViec : int <<PK>>
  +MaDoUuTien : int <<FK>>
  +MaDoKho : int <<FK>>
  +MaTrangThai : int <<FK>>
  +ConMaCongViec : int <<FK>>
  +MaDuAn : int <<FK>>
  +TenCongViec : string
  +MoTa : string
  +HanHoanThanh : datetime
  +DiemCongViec : float
  --
  +GetCongViecs()
  +GetCongViecById(id: int)
  +CreateCongViec()
  +UpdateCongViec(id: int)
  +UpdateStatus(id: int)
  +DeleteCongViec(id: int)
}

class PhanCongNhanVien <<table>> {
  +MaPhanCong : int <<PK>>
  +MaCongViec : int <<FK>>
  +MaNhanVien : int <<FK>>
  +NgayGiao : datetime
  +NgayBatDauDuKien : datetime
  +NgayKetThucDuKien : datetime
  +NgayBatDauThucTe : datetime
  +NgayKetThucThucTe : datetime
  --
  +AssignTask()
}

class TienDoCongViec <<table>> {
  +MaTienDo : char(10) <<PK>>
  +MaCongViec : int <<FK>>
  +PhanTramHoanThanh : float
  +TrangThaiHienTai : int
  +NgayCapNhat : datetime
  --
  +UpdateTienDo()
  +ResolveTaskStatus()
  +BuildTienDoId(maCongViec: int, maNhanVien: int)
}

class NhatKyCongViec <<table>> {
  +MaNhatKy : int <<PK>>
  +MaCongViec : int <<FK>>
  +PhanTramHoanThanh : float
  +GhiChu : string
  +NgayCapNhat : datetime
  --
  +AddLog(maCongViec: int)
  +GetByCongViec(maCongViec: int)
}

class LichSuTrangThaiCongViec <<table>> {
  +MaLichSu : int <<PK>>
  +MaCongViec : int <<FK>>
  +TrangThaiCu : float
  +TrangThaiMoi : float
  +ThoiGian : datetime
  --
  +AddStatusHistory(maCongViec: int)
  +GetByCongViec(maCongViec: int)
}

class LoaiKpi <<table>> {
  +MaLoaiKpi : int <<PK>>
  +TenLoaiKpi : string
  --
  +GetAll()
}

class DanhMucKpi <<table>> {
  +MaKpi : int <<PK>>
  +MaLoaiKpi : int <<FK>>
  +TenKpi : string
  +TrongSo : float
  --
  +GetByLoaiKpi(maLoaiKpi: int)
}

class KetQuaKpi <<table>> {
  +MaKetQua : int <<PK>>
  +MaNhanVien : int <<FK>>
  +MaKpi : int <<FK>>
  +DiemSo : float
  +Thang : int
  +Nam : int
  --
  +CalculateKpi()
  +GetByNhanVien(id: int)
  +GetByPhongBan(id: int)
  +UpsertKetQuaKpi()
}

class NhatKyHoatDong <<table>> {
  +MaNhatKyHoatDong : int <<PK>>
  +MaNhanVien : int <<FK>>
  +HanhDong : string
  +ThoiGian : datetime
  --
  +GetNhatKy()
  +AddNhatKy(maNhanVien: int, hanhDong: string)
}

AspNetUsers "1" -- "0..1" NhanVien : AspNetUserId
AspNetUsers "1" -- "0..*" AspNetUserRoles : UserId
AspNetRoles "1" -- "0..*" AspNetUserRoles : RoleId

PhongBan "1" -- "0..*" NhanVien : MaPhongBan
PhongBan "1" -- "0..*" NhanVien : PhoMaPhongBan
NhanVien "1" -- "0..*" PhongBan : MaNhanVien (truong phong)

DuAn "1" -- "0..*" CongViec : MaDuAn
DoUuTien "1" -- "0..*" CongViec : MaDoUuTien
DoKho "1" -- "0..*" CongViec : MaDoKho
TrangThaiCongViec "1" -- "0..*" CongViec : MaTrangThai
CongViec "1" -- "0..*" CongViec : ConMaCongViec

NhanVien "1" -- "0..*" PhanCongNhanVien : MaNhanVien
CongViec "1" -- "0..*" PhanCongNhanVien : MaCongViec
CongViec "1" -- "0..*" TienDoCongViec : MaCongViec
CongViec "1" -- "0..*" NhatKyCongViec : MaCongViec
CongViec "1" -- "0..*" LichSuTrangThaiCongViec : MaCongViec

LoaiKpi "1" -- "0..*" DanhMucKpi : MaLoaiKpi
DanhMucKpi "1" -- "0..*" KetQuaKpi : MaKpi
NhanVien "1" -- "0..*" KetQuaKpi : MaNhanVien
NhanVien "1" -- "0..*" NhatKyHoatDong : MaNhanVien

CongViec ..> KetQuaKpi : KPI được tinh tu task

note right of AspNetRoles
  Vai tro hệ thống:
  - Admin
  - Manager
  - Employee
end note

note bottom of KetQuaKpi
  Bao cao hieu suat được tong hop
  tu KetQuaKpi + du lieu cong viec.
  Không co bang BaoCao rieng trong code.
end note

@enduml

