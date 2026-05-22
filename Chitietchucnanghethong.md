# Chi tiết chức năng hệ thống theo vai trò (WebApp + AI)

## 1) Cơ sở xác định quyền trong dự án
- Hệ thống dùng `Role` + `Claim` (`Permission`) để phân quyền.
- `FallbackPolicy` yêu cầu người dùng phải đăng nhập cho hầu hết endpoint.
- `Admin` có toàn bộ quyền mặc định (`Permissions.AllowedClaims`).
- `Manager`, `Employee` có bộ quyền mặc định riêng (`Permissions.DefaultRoleClaims`), có thể điều chỉnh qua chức năng quản trị role/claim.

## 2) Tác nhân trong hệ thống
- `Admin` (Quản trị viên)
- `Manager` (Quản lý)
- `Employee` (Nhân viên)

## 3) Tác nhân ngoài hệ thống
- `Identity Service` (ASP.NET Identity): xác thực đăng nhập, quản lý role/claim, khóa tài khoản, reset mật khẩu.
- `Notification/Email Service` (qua `EmailService`, thông báo nội bộ): gửi email/thông báo cho các sự kiện nghiệp vụ.
- `SQL Server Database`: lưu trữ dữ liệu nghiệp vụ, nhật ký, KPI, AI, báo cáo.

## 3.1) Thành phần nội bộ AI trong hệ thống
- `AiPredictionService`: xử lý dự báo rủi ro, phân loại hiệu suất, gợi ý nguồn lực dựa trên dữ liệu nghiệp vụ.
- `AiEvaluationService` và `AiEvaluationHostedService`: đánh giá chất lượng mô hình, tổng hợp chỉ số hiệu năng AI theo kỳ.
- Kết luận mô hình kiến trúc: với phạm vi dự án hiện tại, AI là **thành phần nội bộ** của WebApp, không phải hệ thống ngoài độc lập.

## 4) Chức năng chi tiết theo vai trò và khác biệt quyền

### 4.1 Quản trị hệ thống & tài khoản
| Chức năng | Admin | Manager | Employee | Ghi chú khác biệt quyền |
|---|---|---|---|---|
| Đăng nhập/đăng xuất, đổi mật khẩu | Có | Có | Có | Chức năng nền cho mọi vai trò |
| Quản lý tài khoản (`account-management`) | Toàn quyền | Không mặc định | Không | API yêu cầu policy `ManageUser` (thực tế dành Admin/role có `Settings.Edit`) |
| Quản lý role/claim (`system/roles`) | Toàn quyền | Chỉ khi được cấp `Settings.Edit` | Không mặc định | Vai trò có thể mở rộng nếu được gán claim |
| Cấu hình hệ thống (AI/UI/KPI master) | Toàn quyền | Xem/Sửa khi có `SettingsView/SettingsEdit` | Không mặc định | Thuộc `SystemController` |

### 4.2 Nhân sự - phòng ban - nhóm
| Chức năng | Admin | Manager | Employee | Ghi chú khác biệt quyền |
|---|---|---|---|---|
| Xem danh sách nhân viên/phòng ban/nhóm | Có | Có (`Employees.View`) | Có (`Employees.View`) | Cùng chức năng xem nhưng phạm vi thao tác khác |
| Tạo/sửa/xóa nhân viên | Có | Không mặc định | Không | Yêu cầu `Employees.Create/Edit/Delete` |
| Tạo/sửa/xóa phòng ban, nhóm | Có | Không mặc định | Không | Dựa trên các policy `Employees.Create/Edit/Delete` |
| Quản lý kỹ năng nhân viên | Có | Có (`Employees.Skills`) | Không mặc định | Manager thường cập nhật theo điều phối nguồn lực |
| Hồ sơ cá nhân (`me/profile`) | Có (nếu là user) | Có | Có | Mọi user có thể xem/sửa hồ sơ bản thân |
| Gửi yêu cầu cập nhật hồ sơ | Có thể | Có thể | Có thể | Luồng `profile-change-requests` |
| Duyệt/từ chối yêu cầu cập nhật hồ sơ | Có | Có (`Employees.Edit`) | Không | Cùng module hồ sơ nhưng quyền xử lý khác nhau |

### 4.3 Dự án & công việc
| Chức năng | Admin | Manager | Employee | Ghi chú khác biệt quyền |
|---|---|---|---|---|
| Xem dự án/công việc | Có | Có (`Projects.View`, `Tasks.View`) | Có (`Projects.View`, `Tasks.View`) | Cùng chức năng xem |
| Tạo/sửa/xóa dự án | Có | Tạo có, sửa tùy claim (`Projects.Create/Edit/Delete`) | Không mặc định | Manager mặc định có `Projects.Create`, không có `Projects.Edit/Delete` mặc định |
| Tạo công việc | Có | Có (`Tasks.Create`) | Không mặc định | Nhân viên không có quyền tạo mặc định |
| Sửa công việc/đính kèm/lịch sử | Có | Có (`Tasks.Edit/Attach/History`) | Có một phần (`Tasks.Edit/Attach/History`) | Nhân viên có thể sửa trong phạm vi được giao (theo kiểm tra nghiệp vụ) |
| Phân công công việc (NV/Nhóm/PB) | Có | Có (`Tasks.Assign`) | Không | Khác biệt lớn trong cùng module công việc |
| Duyệt/Từ chối tiến độ | Có | Có (`Tasks.Approve`) | Không | Manager là vai trò chính duyệt tiến độ |

### 4.4 KPI & đánh giá hiệu suất
| Chức năng | Admin | Manager | Employee | Ghi chú khác biệt quyền |
|---|---|---|---|---|
| Xem KPI tổng hợp | Có | Có (`Kpi.View`) | Không mặc định | Employee dùng luồng KPI cá nhân |
| Xem KPI cá nhân (`MyKpi`) | Có | Có thể nếu cấp claim | Có (`Kpi.MyView`) | Tách riêng policy `MyKpiView` |
| Quản lý danh mục KPI, gán KPI, tính KPI | Có | Có (`Kpi.Manage`, `Kpi.Evaluate`) | Không | Manager là vai trò vận hành KPI |
| Đề xuất KPI và duyệt đề xuất | Có | Có (`Kpi.Manage`) | Có thể tạo/feedback tùy cấu hình màn hình | Khác biệt giữa tạo đề xuất và phê duyệt |
| Xem xếp hạng/nhóm (`Kpi.TeamView`, `Kpi.Ranking`) | Có | Có | Không mặc định | Thuộc nhóm phân tích hiệu suất |

### 4.5 Báo cáo & dashboard
| Chức năng | Admin | Manager | Employee | Ghi chú khác biệt quyền |
|---|---|---|---|---|
| Xem dashboard | Có | Có | Có (policy `DashboardView` cho Employee) | Employee xem góc nhìn cá nhân là chính |
| Tạo/Lưu nháp/Nộp báo cáo | Có | Có (`Reports.Create`, `Reports.Submit`) | Có (`Reports.Create`, `Reports.Submit`) | Cùng chức năng nhưng phạm vi dữ liệu theo quyền |
| Duyệt/Từ chối báo cáo | Có | Có (`Reports.Review`) | Không | Vai trò phê duyệt khác vai trò lập báo cáo |
| Quản trị/xóa báo cáo | Có | Có khi có `Reports.Manage` | Không mặc định | Quyền quản trị sâu |
| Xuất PDF/Excel | Có | Có (`Reports.Export`) | Có (`Reports.Export`) | Có thể áp dụng lọc phạm vi khi export |

### 4.6 AI dự báo/đánh giá/feedback/HITL
| Chức năng | Admin | Manager | Employee | Ghi chú khác biệt quyền |
|---|---|---|---|---|
| Xem cảnh báo/dự báo AI | Có | Có (`Ai.ViewAlerts`, `Ai.ViewForecast`) | Có (`Ai.ViewAlerts`, `Ai.ViewForecast`) | Employee chủ yếu tiêu thụ thông tin |
| Xem hiệu năng mô hình AI | Có | Có (`Ai.ViewPerformance`) | Có (`Ai.ViewPerformance`) | Phục vụ minh bạch mô hình |
| Chạy đánh giá mô hình / can thiệp HITL | Có | Có (`Ai.SuggestResources`) | Không mặc định | Quyền thao tác AI nâng cao |
| Gửi feedback AI | Có | Có | Có | Mọi vai trò có thể phản hồi khi có quyền truy cập màn hình/API |
| Gợi ý nguồn lực từ AI | Có | Có (`Ai.SuggestResources`) | Không mặc định | Dùng cho điều phối quản lý |

### 4.7 Thông báo
| Chức năng | Admin | Manager | Employee | Ghi chú khác biệt quyền |
|---|---|---|---|---|
| Nhận danh sách thông báo | Có | Có (`Notifications.Receive`) | Có (`Notifications.Receive`) | Mọi vai trò sử dụng trung tâm thông báo |
| Đánh dấu đã đọc / đã đọc tất cả | Có | Có | Có | Cùng hành vi nhưng dữ liệu theo từng người nhận |
| Đồng bộ cảnh báo KPI/AI | Có | Có | Thụ động nhận | Được kích hoạt bởi nghiệp vụ hệ thống |

## 5) Cùng một chức năng nhưng khác quyền: các ví dụ điển hình
- **Công việc:** Employee có thể cập nhật tiến độ phần việc được giao; Manager có thêm quyền phân công và duyệt/từ chối tiến độ; Admin có toàn quyền.
- **KPI:** Employee thiên về xem KPI cá nhân; Manager quản trị danh mục và chạy tính KPI; Admin giám sát và can thiệp toàn hệ thống.
- **Báo cáo:** Employee có thể lập và gửi báo cáo trong phạm vi; Manager có quyền duyệt; Admin có quyền quản trị/xóa/cấu hình toàn cục.
- **AI:** Employee chủ yếu xem dự báo và gửi phản hồi; Manager sử dụng chức năng AI nâng cao (gợi ý nguồn lực, can thiệp HITL); Admin có quyền cao nhất.

## 6) Chuỗi tác động của tác nhân ngoài hệ thống
- **Identity Service ->** tác động trực tiếp vào đăng nhập, xác thực cookie, role/claim, khóa/mở khóa tài khoản.
- **Notification/Email Service ->** nhận sự kiện nghiệp vụ (phân công, duyệt tiến độ, cảnh báo KPI/AI, trạng thái báo cáo), gửi thông báo tới người dùng.
- **Database ->** là nguồn sự thật cho toàn bộ chức năng; mọi vai trò thao tác gián tiếp thông qua controller/service.

## 6.1) Chuỗi xử lý AI nội bộ
- **WebApp Service Layer -> AI nội bộ -> Database:** dữ liệu nghiệp vụ được chuẩn hóa trong service, tính toán AI được thực hiện nội bộ, kết quả lưu lại vào bảng AI/KPI để phục vụ dashboard, cảnh báo và quyết định điều hành.

## 7) Gợi ý đưa vào sơ đồ use case tổng
- Giữ 3 actor nội bộ: `Admin`, `Manager`, `Employee`.
- Thêm actor ngoài: `Identity Service`, `Notification/Email Service`.
- Với AI nội bộ hiện tại, thể hiện AI như **module bên trong biên hệ thống** thay vì actor ngoài.
- Tách use case cùng tên nhưng khác mức quyền bằng ghi chú quyền hoặc use case con:
  - `Quản lý công việc` + `Duyệt tiến độ` (Manager/Admin).
  - `Xem KPI cá nhân` (Employee) tách khỏi `Quản lý KPI` (Manager/Admin).
- Dùng `<<include>>` cho bước bắt buộc (xác thực, kiểm tra quyền), dùng `<<extend>>` cho nhánh phát sinh (gửi cảnh báo, ghi log phụ trợ).

## 8) Nguồn đối chiếu trong mã nguồn
- `LuanVan/LuanVan/Contracts/Roles.cs`
- `LuanVan/LuanVan/Contracts/Permissions.cs`
- `LuanVan/LuanVan/Program.cs` (policy authorization)
- `LuanVan/LuanVan/Controllers/PortalController.cs` (điều hướng màn hình theo quyền)
- Các API controller theo module: `CongViecController`, `DuAnController`, `KpiController`, `ReportController`, `AiController`, `SystemController`, `NhanVienController`, `PhongBanController`, `NhomController`, `ThongBaoController`, `AccountManagementController`.
