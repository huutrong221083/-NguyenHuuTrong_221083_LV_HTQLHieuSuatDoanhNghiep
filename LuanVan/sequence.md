# Sequence Diagram

## 1. Đăng nhập hệ thống
```plantuml
@startuml
Title Sequence - Đăng nhập
actor "Admin/Manager/Employee" as User
participant "Frontend UI\n(Login.cshtml)" as UI
participant "Backend API\nAccountController / Identity" as API
participant "Auth Service\nASP.NET Identity" as SVC
database "Database\nAspNetUsers, AspNetUserRoles, AspNetRoles" as DB

User -> UI: Nhập username/password
UI -> API: Gửi yêu cầu đăng nhập
API -> SVC: Xác thực thông tin
SVC -> DB: SELECT user + role theo username
DB --> SVC: Trả dữ liệu tài khoản/role

alt Đăng nhập hợp lệ
  SVC --> API: Kết quả thành công + claims
  API --> UI: Tạo session/cookie, chuyển Dashboard
else Đăng nhập thất bại
  SVC --> API: Sai thông tin hoặc tài khoản bị khóa
  API --> UI: Trả thông báo lỗi đăng nhập
end
@enduml
```

## 2. Quản lý nhân viên (Admin)
```plantuml
@startuml
Title Sequence - Quản lý nhân viên (Admin)
actor Admin
participant "Frontend UI\nEmployees.cshtml" as UI
participant "Backend API\nNhanVienController" as API
participant "Service\nValidate + Business Rule" as SVC
database "Database\nNHANVIEN, PHONGBAN, NHATKYHOATDONG, AspNetUsers" as DB

Admin -> UI: Tạo/Cập nhật trạng thái nhân viên
UI -> API: POST/PUT /nhanvien
API -> SVC: Validate dữ liệu (CCCD, email, SDT, phòng ban)
SVC -> DB: SELECT kiểm tra trùng CCCD/email + tồn tại PHONGBAN
DB --> SVC: Kết quả kiểm tra

alt Dữ liệu hợp lệ
  SVC --> API: Cho phép xử lý
  API -> DB: INSERT/UPDATE NHANVIEN
  API -> DB: UPDATE AspNetUsers lock/unlock (nếu có AspNetUserId)
  API -> DB: INSERT NHATKYHOATDONG (Tạo/Cập nhật/Đổi trạng thái)
  DB --> API: Commit thành công
  API --> UI: Trả kết quả thành công
else Dữ liệu không hợp lệ
  SVC --> API: Thông báo lỗi nghiệp vụ
  API --> UI: Trả lỗi 400/409
end
@enduml
```

## 3. Quản lý phòng ban (Admin)
```plantuml
@startuml
Title Sequence - Quản lý phòng ban (Admin)
actor Admin
participant "Frontend UI\nDepartments.cshtml" as UI
participant "Backend API\nPhongBanController" as API
participant "Service\nValidate Department Rule" as SVC
database "Database\nPHONGBAN, NHANVIEN" as DB

Admin -> UI: Tạo/Cập nhật/Xóa phòng ban
UI -> API: POST/PUT/DELETE /phongban
API -> SVC: Validate tên phòng ban + trưởng phòng
SVC -> DB: SELECT PHONGBAN (trùng tên), NHANVIEN (trưởng phòng hoạt động)
DB --> SVC: Kết quả validate

alt Hợp lệ
  API -> DB: INSERT/UPDATE/DELETE PHONGBAN
  DB --> API: Thành công
  API --> UI: Trả kết quả thành công
else Không hợp lệ
  API --> UI: Trả lỗi (trùng tên, trưởng phòng không hợp lệ, phòng ban còn nhân viên)
end
@enduml
```

## 4. Quản lý tài khoản và phân quyền (Admin)
```plantuml
@startuml
Title Sequence - Quản lý tài khoản và phân quyền (Admin)
actor Admin
participant "Frontend UI\nSettings.cshtml" as UI
participant "Backend API\nSystemController" as API
participant "Service\nSystem Overview Service" as SVC
database "Database\nAspNetUsers, AspNetRoles, AspNetUserRoles, LOAIKPI, DOKHO, DOUUTIEN" as DB

Admin -> UI: Mở trang quản trị tài khoản/phân quyền
UI -> API: GET /system/settings-overview
API -> SVC: Tổng hợp dữ liệu cấu hình hệ thống
SVC -> DB: SELECT role từ AspNetRoles
SVC -> DB: SELECT user từ AspNetUsers
SVC -> DB: SELECT mapping từ AspNetUserRoles
SVC -> DB: SELECT master data (LOAIKPI, DOKHO, DOUUTIEN)
DB --> SVC: Trả dữ liệu tổng hợp
SVC --> API: DTO SettingsOverview
API --> UI: Hiển thị tài khoản + role + cấu hình
@enduml
```

## 5. Quản lý công việc - Tạo công việc (Manager)
```plantuml
@startuml
Title Sequence - Quản lý công việc (Manager) - Tạo công việc
actor Manager
participant "Frontend UI\nTasks.cshtml" as UI
participant "Backend API\nCongViecController" as API
participant "Service\nValidate Task Rule" as SVC
database "Database\nCONGVIEC, DUAN, DOKHO, DOUUTIEN, TRANGTHAICONGVIEC, NHATKYHOATDONG" as DB

Manager -> UI: Nhập thông tin công việc mới
UI -> API: POST /congviec
API -> SVC: Validate nghiệp vụ công việc
SVC -> DB: SELECT DUAN/DOKHO/DOUUTIEN/TRANGTHAICONGVIEC
DB --> SVC: Kết quả tồn tại danh mục

alt Hợp lệ
  API -> DB: INSERT CONGVIEC
  API -> DB: INSERT NHATKYHOATDONG (nếu có MaNhanVienThucHien)
  DB --> API: Commit thành công
  API --> UI: Trả chi tiết công việc đã tạo
else Không hợp lệ
  SVC --> API: Lỗi validation
  API --> UI: Trả lỗi 400
end
@enduml
```

## 6. Quản lý công việc - Phân công công việc (Manager)
```plantuml
@startuml
Title Sequence - Quản lý công việc (Manager) - Phân công công việc
actor Manager
participant "Frontend UI\nTasks.cshtml" as UI
participant "Backend API\nPhanCongNhanVienController" as API
participant "Service\nAssign Validation Service" as SVC
database "Database\nCONGVIEC, NHANVIEN, PHANCONGNHANVIEN, NHATKYHOATDONG" as DB

Manager -> UI: Chọn công việc và nhân viên để phân công
UI -> API: POST /phancong/nhanvien
API -> SVC: Kiểm tra điều kiện phân công
SVC -> DB: SELECT CONGVIEC (chưa hoàn thành)
SVC -> DB: SELECT NHANVIEN (đang hoạt động)
SVC -> DB: SELECT PHANCONGNHANVIEN (kiểm tra trùng)
DB --> SVC: Kết quả kiểm tra

alt Có thể phân công
  API -> DB: INSERT PHANCONGNHANVIEN
  API -> DB: INSERT NHATKYHOATDONG
  DB --> API: Commit thành công
  API --> UI: Trả mã phân công
else Không thể phân công
  API --> UI: Trả lỗi (task hoàn thành / nhân viên nghỉ / phân công trùng)
end
@enduml
```

## 7. Theo dõi tiến độ công việc (Manager + Employee)
```plantuml
@startuml
Title Sequence - Theo dõi tiến độ công việc (Manager + Employee)
actor "Manager/Employee" as User
participant "Frontend UI\nTasks.cshtml / Dashboard.cshtml" as UI
participant "Backend API\nCongViecController" as API
participant "Service\nTask Query Service" as SVC
database "Database\nCONGVIEC, TIENDOCONGVIEC, PHANCONGNHANVIEN, TRANGTHAICONGVIEC" as DB

User -> UI: Mở danh sách/chi tiết công việc
UI -> API: GET /congviec?filters
API -> SVC: Tính toán trạng thái trễ hạn + truy vấn phân trang
SVC -> DB: SELECT CONGVIEC + TIENDOCONGVIEC + PHANCONGNHANVIEN + TRANGTHAICONGVIEC
DB --> SVC: Dữ liệu tiến độ

alt Có dữ liệu
  SVC --> API: Danh sách công việc + % tiến độ
  API --> UI: Hiển thị bảng theo dõi tiến độ
else Không có dữ liệu
  API --> UI: Trả danh sách rỗng
end
@enduml
```

## 8. Cập nhật tiến độ công việc (Employee)
```plantuml
@startuml
Title Sequence - Cập nhật tiến độ công việc (Employee)
actor Employee
participant "Frontend UI\nTasks.cshtml" as UI
participant "Backend API\nTienDoController" as API
participant "Service\nProgress Update Service" as SVC
database "Database\nCONGVIEC, PHANCONGNHANVIEN, NHANVIEN, TIENDOCONGVIEC, NHATKYCONGVIEC, NHATKYHOATDONG" as DB

Employee -> UI: Gửi % hoàn thành + ghi chú
UI -> API: POST /tiendo
API -> SVC: Validate % + quyền được phân công + trạng thái nhân viên
SVC -> DB: SELECT CONGVIEC + PHANCONGNHANVIEN + NHANVIEN
DB --> SVC: Kết quả kiểm tra

alt Hợp lệ
  API -> DB: UPSERT TIENDOCONGVIEC theo MATIENDO
  API -> DB: UPDATE CONGVIEC.MATRANGTHAI theo % tiến độ
  API -> DB: INSERT NHATKYCONGVIEC
  API -> DB: INSERT NHATKYHOATDONG
  DB --> API: Commit thành công
  API --> UI: Trả % cá nhân, % công việc, trạng thái mới
else Không hợp lệ
  API --> UI: Trả lỗi (không được phân công / task đã hoàn thành / dữ liệu sai)
end
@enduml
```

## 9. Đánh giá hiệu suất nhân viên (Manager)
```plantuml
@startuml
Title Sequence - Đánh giá hiệu suất nhân viên (Manager)
actor Manager
participant "Frontend UI\nEvaluation.cshtml" as UI
participant "Backend API\nKpiController" as API
participant "Service\nKpiService" as SVC
database "Database\nDANHMUCKPI, NHANVIEN, PHANCONGNHANVIEN, CONGVIEC, TIENDOCONGVIEC, KETQUAKPI" as DB

Manager -> UI: Chọn kỳ đánh giá KPI
UI -> API: POST /kpi/calculate
API -> SVC: CalculateAsync(tháng, năm, phòng ban/nhân viên)
SVC -> DB: SELECT DANHMUCKPI kiểm tra MAKPI
SVC -> DB: SELECT NHANVIEN đang hoạt động
SVC -> DB: SELECT PHANCONGNHANVIEN + CONGVIEC trong kỳ
SVC -> DB: SELECT TIENDOCONGVIEC để tính % hoàn thành

alt Dữ liệu hợp lệ
  SVC -> DB: UPSERT KETQUAKPI (tạo mới/cập nhật theo nhân viên)
  DB --> SVC: Lưu thành công
  SVC --> API: Trả kết quả tổng hợp tính KPI
  API --> UI: Hiển thị kết quả đánh giá
else Dữ liệu không hợp lệ
  SVC --> API: Ném lỗi nghiệp vụ (tháng/năm/MAKPI)
  API --> UI: Trả lỗi 400
end
@enduml
```

## 10. Xem KPI và kết quả đánh giá (Employee)
```plantuml
@startuml
Title Sequence - Xem KPI và kết quả đánh giá (Employee)
actor Employee
participant "Frontend UI\nKpi.cshtml / Profile.cshtml" as UI
participant "Backend API\nKpiController" as API
participant "Service\nKPI Query Service" as SVC
database "Database\nNHANVIEN, KETQUAKPI" as DB

Employee -> UI: Xem KPI cá nhân theo tháng/năm
UI -> API: GET /kpi/nhanvien/{id}
API -> SVC: Truy vấn lịch sử KPI
SVC -> DB: SELECT NHANVIEN theo id

alt Nhân viên tồn tại
  SVC -> DB: SELECT KETQUAKPI theo MANHANVIEN, MAKPI, THANG/NAM
  DB --> SVC: Trả lịch sử điểm KPI
  SVC --> API: DTO điểm hiện tại + xu hướng + xếp loại
  API --> UI: Hiển thị kết quả KPI cá nhân
else Không tồn tại nhân viên
  API --> UI: Trả lỗi 404
end
@enduml
```

## 11. Xem báo cáo hiệu suất (Manager)
```plantuml
@startuml
Title Sequence - Xem báo cáo hiệu suất (Manager)
actor Manager
participant "Frontend UI\nReports.cshtml / Dashboard.cshtml" as UI
participant "Backend API\nDashboardController" as API
participant "Service\nDashboard Service" as SVC
database "Database\nKETQUAKPI, NHANVIEN, PHONGBAN, CONGVIEC, PHANCONGNHANVIEN, TIENDOCONGVIEC, DUAN" as DB

Manager -> UI: Chọn kỳ báo cáo
UI -> API: GET /dashboard?thang=&nam=&maKpi=
API -> SVC: Tổng hợp KPI + tiến độ + task trễ + top nhân viên
SVC -> DB: SELECT KETQUAKPI + NHANVIEN
SVC -> DB: SELECT PHONGBAN
SVC -> DB: SELECT CONGVIEC + PHANCONGNHANVIEN
SVC -> DB: SELECT TIENDOCONGVIEC
SVC -> DB: SELECT DUAN
DB --> SVC: Dữ liệu tổng hợp

alt Có dữ liệu
  SVC --> API: DashboardDto
  API --> UI: Hiển thị báo cáo hiệu suất
else Không có dữ liệu
  API --> UI: Trả báo cáo rỗng
end
@enduml
```

## 12. Ghi log hoạt động hệ thống (Audit Log)
```plantuml
@startuml
Title Sequence - Ghi log hoạt động hệ thống (Audit Log)
actor "Admin/Manager/Employee" as User
participant "Frontend UI" as UI
participant "Backend API\n(NhanVien/CongViec/PhanCong/TienDo/DuAn)" as API
participant "Service\nAudit Logging" as SVC
database "Database\nNHATKYHOATDONG, NHANVIEN" as DB

User -> UI: Thực hiện hành động nghiệp vụ
UI -> API: Gọi API tương ứng
API -> SVC: Yêu cầu ghi log hành động
SVC -> DB: SELECT NHANVIEN kiểm tra actor tồn tại

alt Actor hợp lệ
  SVC -> DB: INSERT NHATKYHOATDONG(MANHANVIEN, HANHDONG, THOIGIAN)
  DB --> API: Ghi log thành công
  API --> UI: Trả kết quả nghiệp vụ
else Actor không tồn tại
  SVC --> API: Bỏ qua log
  API --> UI: Trả kết quả nghiệp vụ (không ghi log)
end
@enduml
```

## 13. Tích hợp hệ thống đánh giá KPI (xử lý dữ liệu KPI)
```plantuml
@startuml
Title Sequence - Tích hợp hệ thống đánh giá KPI
actor Manager
participant "Frontend UI\nEvaluation.cshtml" as UI
participant "Backend API\nKpiController" as API
participant "Service\nKpiService" as SVC
database "Database\nDANHMUCKPI, NHANVIEN, CONGVIEC, PHANCONGNHANVIEN, TIENDOCONGVIEC, KETQUAKPI" as DB

Manager -> UI: Kích hoạt tích hợp tính KPI theo kỳ
UI -> API: POST /kpi/calculate
API -> SVC: Chuyển request xử lý KPI
SVC -> DB: Đọc dữ liệu nguồn KPI (DANHMUCKPI, NHANVIEN)
SVC -> DB: Đọc dữ liệu thực thi công việc (CONGVIEC, PHANCONGNHANVIEN, TIENDOCONGVIEC)
SVC -> SVC: Tính điểm KPI theo công thức nghiệp vụ

alt Tính toán thành công
  SVC -> DB: Ghi kết quả vào KETQUAKPI
  SVC --> API: Trả số bản ghi tạo mới/cập nhật
  API --> UI: Hiển thị kết quả tích hợp KPI
else Lỗi cấu hình dữ liệu
  SVC --> API: Trả lỗi (ví dụ MAKPI không tồn tại)
  API --> UI: Hiển thị thông báo lỗi
end
@enduml
```
