# Class Diagram (ER rút gọn)

## Mục đích
Biểu diễn cấu trúc dữ liệu cốt lõi của hệ thống theo mức ER rút gọn, tập trung vào thực thể nghiệp vụ chính, khóa và quan hệ.

## A) Core Business
```plantuml
@startuml
hide methods
skinparam classAttributeIconSize 0

class ApplicationUser {
  +Id : string <<PK>>
  +UserName : string
  +Email : string
}

class NhanVien {
  +MaNhanVien : int <<PK>>
  +AspNetUserId : string <<FK>>
  +MaPhongBan : int <<FK>>
  +MaChucVu : int <<FK>>
  +HoTen : string
  +TrangThai : int
}

class PhongBan {
  +MaPhongBan : int <<PK>>
  +TenPhongBan : string
  +MaTruongPhong : int <<FK>>
}

class ChucVu {
  +MaChucVu : int <<PK>>
  +TenChucVu : string
}

class Nhom {
  +MaNhom : int <<PK>>
  +TenNhom : string
  +TruongNhom : int <<FK>>
}

class ThanhVienNhom {
  +MaNhanVien : int <<PK,FK>>
  +MaNhom : int <<PK,FK>>
  +VaiTroTrongNhom : string
}

class DuAn {
  +MaDuAn : int <<PK>>
  +TenDuAn : string
  +TrangThai : int
}

class CongViec {
  +MaCongViec : int <<PK>>
  +MaDuAn : int <<FK>>
  +TenCongViec : string
  +TrangThai : int
}

class PhanCongNhanVien {
  +MaPhaCong : int <<PK>>
  +MaCongViec : int <<FK>>
  +MaNhanVien : int <<FK>>
  +PhanTramHoanThanh : decimal
  +TrangThai : int
}

class PhanCongNhom {
  +MaCongViec : int <<PK,FK>>
  +MaNhom : int <<PK,FK>>
  +TrangThai : int
}

class PhanCongPhongBan {
  +MaCongViec : int <<PK,FK>>
  +MaPhongBan : int <<PK,FK>>
  +TrangThai : int
}

class TienDoCongViec {
  +MaTienDo : int <<PK>>
  +MaCongViec : int <<FK>>
  +PhanTramHoanThanh : decimal
  +TrangThaiPheDuyet : string
  +NguoiPheDuyet : int <<FK>>
}

class NhatKyCongViec {
  +MaNhatKy : int <<PK>>
  +MaCongViec : int <<FK>>
  +MaNhanVien : int <<FK>>
  +PhanTramHoanThanh : decimal
}

ApplicationUser "1" -- "0..1" NhanVien : liên kết tài khoản
PhongBan "1" -- "0..*" NhanVien : quản lý nhân viên
ChucVu "1" -- "0..*" NhanVien : thuộc chức vụ
NhanVien "1" -- "0..*" Nhom : trưởng nhóm
NhanVien "1" -- "0..*" ThanhVienNhom
Nhom "1" -- "0..*" ThanhVienNhom
DuAn "1" -- "0..*" CongViec
CongViec "1" -- "0..*" PhanCongNhanVien
NhanVien "1" -- "0..*" PhanCongNhanVien
CongViec "1" -- "0..*" PhanCongNhom
Nhom "1" -- "0..*" PhanCongNhom
CongViec "1" -- "0..*" PhanCongPhongBan
PhongBan "1" -- "0..*" PhanCongPhongBan
CongViec "1" -- "0..*" TienDoCongViec
NhanVien "1" -- "0..*" TienDoCongViec : người duyệt
CongViec "1" -- "0..*" NhatKyCongViec
NhanVien "1" -- "0..*" NhatKyCongViec
@enduml
```

## B) KPI + AI
```plantuml
@startuml
hide methods
skinparam classAttributeIconSize 0

class LoaiKpi {
  +MaLoaiKpi : int <<PK>>
  +TenLoaiKpi : string
  +HeSo : decimal
}

class DanhMucKpi {
  +MaKpi : int <<PK>>
  +MaLoaiKpi : int <<FK>>
  +TenKpi : string
  +TrongSoGoc : decimal
}

class KpiNhanVien {
  +MaKpi : int <<PK,FK>>
  +MaNhanVien : int <<PK,FK>>
}

class KpiNhom {
  +MaKpi : int <<PK,FK>>
  +MaNhom : int <<PK,FK>>
}

class KpiPhongBan {
  +MaKpi : int <<PK,FK>>
  +MaPhongBan : int <<PK,FK>>
}

class KpiDuAn {
  +MaKpi : int <<PK,FK>>
  +MaDuAn : int <<PK,FK>>
}

class KetQuaKpi {
  +MaKetQua : int <<PK>>
  +MaNhanVien : int <<FK>>
  +MaKpi : int <<FK>>
  +Thang : int
  +Nam : int
  +DiemKpi : decimal
}

class KetQuaKpiTong {
  +MaTong : int <<PK>>
  +MaNhanVien : int <<FK>>
  +Thang : int
  +Nam : int
  +DiemTong : decimal
}

class DeXuatKpi {
  +MaDeXuat : int <<PK>>
  +MaKpi : int <<FK>>
  +NguoiDeXuat : int <<FK>>
  +TrangThai : string
}

class KpiXepLoai {
  +MaXepLoai : int <<PK>>
  +TenXepLoai : string
  +DiemTu : decimal
  +DiemDen : decimal
}

class MoHinhAi {
  +MaModel : int <<PK>>
  +TenModel : string
  +Version : string
}

class DuDoanAi {
  +MaDuDoan : int <<PK>>
  +MaModel : int <<FK>>
  +MaNhanVien : int <<FK>>
  +MaCongViec : int <<FK>>
  +XacSuatTreHan : decimal
}

class AiFeatureStore {
  +MaFeature : int <<PK>>
  +MaModel : int <<FK>>
  +MaNhanVien : int <<FK>>
  +FeatureName : string
}

class AiDanhGiaRun {
  +MaDanhGia : int <<PK>>
  +MaModel : int <<FK>>
  +NgayDanhGia : datetime
  +Accuracy : decimal
}

class AiDanhGiaChiTiet {
  +MaDanhGiaChiTiet : int <<PK>>
  +MaDanhGia : int <<FK>>
  +MaDuDoan : int <<FK>>
  +DungNhan : bool
}

class AiFeedback {
  +MaFeedback : int <<PK>>
  +MaDanhGia : int <<FK>>
  +MaDuDoan : int <<FK>>
  +MaNhanVien : int <<FK>>
  +MucHuuIch : int
}

class AiNhatKyCanThiep {
  +MaCanThiep : int <<PK>>
  +MaDanhGia : int <<FK>>
  +MaDuDoan : int <<FK>>
  +NguoiCanThiep : int <<FK>>
  +ActionType : string
}

class AiBusinessKpiRun {
  +MaBusinessKpi : int <<PK>>
  +MaModel : int <<FK>>
  +NgayTao : datetime
  +UtilityScore : decimal
}

LoaiKpi "1" -- "0..*" DanhMucKpi
DanhMucKpi "1" -- "0..*" KpiNhanVien
DanhMucKpi "1" -- "0..*" KpiNhom
DanhMucKpi "1" -- "0..*" KpiPhongBan
DanhMucKpi "1" -- "0..*" KpiDuAn
DanhMucKpi "1" -- "0..*" KetQuaKpi
DanhMucKpi "1" -- "0..*" DeXuatKpi

MoHinhAi "1" -- "0..*" DuDoanAi
MoHinhAi "1" -- "0..*" AiFeatureStore
MoHinhAi "1" -- "0..*" AiDanhGiaRun
MoHinhAi "1" -- "0..*" AiBusinessKpiRun
AiDanhGiaRun "1" -- "0..*" AiDanhGiaChiTiet
DuDoanAi "1" -- "0..*" AiDanhGiaChiTiet
AiDanhGiaRun "1" -- "0..*" AiFeedback
DuDoanAi "1" -- "0..*" AiFeedback
DuDoanAi "1" -- "0..*" AiNhatKyCanThiep
AiDanhGiaRun "1" -- "0..*" AiNhatKyCanThiep
@enduml
```

## C) Report + Notification
```plantuml
@startuml
hide methods
skinparam classAttributeIconSize 0

class BaoCao {
  +MaBaoCao : int <<PK>>
  +NguoiTao : string <<FK>>
  +MaDuAn : int <<FK>>
  +MaPhongBan : int <<FK>>
  +TrangThai : string
}

class BaoCaoChiTiet {
  +MaBaoCaoChiTiet : int <<PK>>
  +MaBaoCao : int <<FK>>
  +TieuDe : string
}

class YeuCauBaoCao {
  +MaYeuCau : int <<PK>>
  +NguoiYeuCau : string <<FK>>
  +NguoiNhanYeuCau : string <<FK>>
  +TrangThai : string
}

class LoaiThongBao {
  +MaLoai : int <<PK>>
  +TenLoai : string
}

class ThongBao {
  +MaThongBao : int <<PK>>
  +MaLoai : int <<FK>>
  +NoiDung : string
}

class ThongBaoNhanVien {
  +MaNhanVien : int <<PK,FK>>
  +MaThongBao : int <<PK,FK>>
  +DaDoc : bool
}

BaoCao "1" -- "1..*" BaoCaoChiTiet
LoaiThongBao "1" -- "0..*" ThongBao
ThongBao "1" -- "0..*" ThongBaoNhanVien
@enduml
```

## Mô tả ngắn
- **Thành phần tham gia:** các thực thể domain cốt lõi thuộc Core Business, KPI+AI, Report+Notification.
- **Dữ liệu chính:** khóa định danh nghiệp vụ, liên kết phân quyền/ngữ cảnh tổ chức, quan hệ phân công và đánh giá.
- **Kết quả đầu ra:** cấu trúc dữ liệu đủ dùng cho phân tích thiết kế ở mức luận văn, phản ánh quan hệ 1-n và n-n qua bảng trung gian.

