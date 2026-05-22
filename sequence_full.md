# Sequence Diagram Full (Theo luồng nghiệp vụ)

## Mục đích
Mô tả động học xử lý nghiệp vụ của hệ thống WebApp + AI, tập trung các luồng nghiệp vụ cốt lõi thay vì chi tiết hóa từng endpoint nhỏ.

## 1) Đăng nhập + phân quyền truy cập
```plantuml
@startuml
actor User
participant "UI/Login.cshtml" as UI
participant "AccountController" as C
participant "Identity Service" as I
participant "Authorization Policy" as P
participant "AppDbContext" as R
database "SQL Server" as DB

User -> UI: Nhập tài khoản/mật khẩu
UI -> C: POST /Account/Login
C -> I: SignInAsync()
I -> DB: Kiểm tra AspNetUsers, Role, Claims
DB --> I: Dữ liệu định danh
alt Thông tin hợp lệ
  I --> C: Sign-in thành công
  C -> P: Nạp claim/policy
  P --> C: Quyền truy cập theo vai trò
  C --> UI: Chuyển hướng Dashboard
else Sai thông tin hoặc bị khóa
  I --> C: Thất bại
  C --> UI: Thông báo lỗi xác thực
end
@enduml
```

## 2) Tạo công việc + phân công nhân viên/nhóm/phòng ban
```plantuml
@startuml
actor Manager
participant "UI/Tasks.cshtml" as UI
participant "CongViecController" as C
participant "Validation Service" as S
participant "AppDbContext" as R
database "SQL Server" as DB
participant "Notification Service" as N

Manager -> UI: Nhập thông tin công việc + danh sách phân công
UI -> C: POST /congviec
C -> S: Validate dự án, dữ liệu công việc
S -> DB: Kiểm tra DUAN, trạng thái, ràng buộc
DB --> S: Kết quả kiểm tra
alt Hợp lệ
  C -> R: Insert CONGVIEC
  C -> R: Insert PHANCONGNHANVIEN/NHOM/PHONGBAN
  R -> DB: SaveChanges()
  DB --> R: Thành công
  C -> N: Tạo thông báo phân công
  N --> C: Kết quả gửi
  C --> UI: Trả kết quả tạo công việc
else Dữ liệu không hợp lệ hoặc sai trạng thái dự án
  C --> UI: Trả lỗi nghiệp vụ
end
@enduml
```

## 3) Nhân viên cập nhật tiến độ + quản lý duyệt/từ chối
```plantuml
@startuml
actor Employee
actor Manager
participant "UI/Tasks.cshtml" as UI
participant "CongViecController" as C
participant "Task Progress Service" as S
participant "AppDbContext" as R
database "SQL Server" as DB
participant "Notification Service" as N

Employee -> UI: Nhập % hoàn thành + ghi chú
UI -> C: POST /tiendo
C -> S: Kiểm tra quyền phân công, trạng thái công việc
S -> DB: Đọc PHANCONGNHANVIEN, CONGVIEC
DB --> S: Dữ liệu hiện tại
alt Hợp lệ
  C -> R: Insert TienDoCongViec (chờ duyệt)
  R -> DB: SaveChanges()
  DB --> R: Thành công
  C -> N: Thông báo quản lý duyệt tiến độ
else Không phải người được giao hoặc task không hợp lệ
  C --> UI: Trả lỗi quyền/trạng thái
end

Manager -> UI: Chọn duyệt hoặc từ chối
UI -> C: PUT /tiendo/{id}/approve hoặc /reject
C -> S: Validate quyền duyệt
alt Đủ quyền và trạng thái hợp lệ
  C -> R: Cập nhật TrangThaiPheDuyet
  R -> DB: SaveChanges()
  C -> N: Thông báo kết quả cho nhân viên
  C --> UI: Trả kết quả duyệt/từ chối
else Không đủ quyền hoặc đã xử lý trước đó
  C --> UI: Trả lỗi xử lý tiến độ
end
@enduml
```

## 4) Tính KPI nhân viên/đội theo kỳ đánh giá
```plantuml
@startuml
actor Manager
participant "UI/Kpi.cshtml" as UI
participant "KpiController" as C
participant "KpiService" as S
participant "AppDbContext" as R
database "SQL Server" as DB

Manager -> UI: Chọn tháng/năm/phạm vi
UI -> C: POST /kpi/calculate hoặc /kpi/calculate-all
C -> S: CalculateAsync()
S -> DB: Đọc DANHMUCKPI, KpiAssignment, CONGVIEC, TIENDO
DB --> S: Dữ liệu tính toán
alt Đủ dữ liệu và cấu hình hợp lệ
  S -> R: Upsert KETQUAKPI, KETQUAKPITONG
  R -> DB: SaveChanges()
  S --> C: Kết quả tính KPI
  C --> UI: Trả điểm và thống kê
else Dữ liệu thiếu hoặc tham số kỳ không hợp lệ
  S --> C: Lỗi nghiệp vụ
  C --> UI: Trả lỗi tính KPI
end
@enduml
```

## 5) Đề xuất KPI + duyệt/từ chối đề xuất
```plantuml
@startuml
actor Employee
actor Manager
participant "UI/MyKpi.cshtml / Kpi.cshtml" as UI
participant "KpiController" as C
participant "Kpi Proposal Service" as S
participant "AppDbContext" as R
database "SQL Server" as DB
participant "Notification Service" as N

Employee -> UI: Gửi đề xuất KPI
UI -> C: POST /kpi/proposals
C -> S: Validate đề xuất
alt Hợp lệ
  C -> R: Insert DEXUATKPI (Pending)
  R -> DB: SaveChanges()
  C -> N: Thông báo quản lý duyệt
else Không hợp lệ
  C --> UI: Trả lỗi dữ liệu đề xuất
end

Manager -> UI: Review đề xuất KPI
UI -> C: POST /kpi/proposals/{id}/review
C -> S: Kiểm tra quyền và trạng thái Pending
alt Đủ điều kiện duyệt
  C -> R: Cập nhật trạng thái Approved/Rejected
  R -> DB: SaveChanges()
  C -> N: Thông báo kết quả cho người đề xuất
  C --> UI: Trả kết quả review
else Không đủ quyền hoặc đề xuất đã xử lý
  C --> UI: Trả lỗi review
end
@enduml
```

## 6) AI dự báo rủi ro trễ hạn công việc
```plantuml
@startuml
actor Manager
participant "UI/AiForecast.cshtml" as UI
participant "AiController" as C
participant "AiFeatureBuilderService" as F
participant "AiPredictionService" as S
participant "AppDbContext" as R
database "SQL Server" as DB
participant "AI Service" as AI
participant "Notification Service" as N

Manager -> UI: Yêu cầu dự báo rủi ro trễ hạn
UI -> C: POST /ai/predict-delay
C -> F: Trích xuất đặc trưng đầu vào
F -> DB: Đọc CONGVIEC, TIENDO, PHANCONG
DB --> F: Feature data
alt Dữ liệu đầu vào hợp lệ
  C -> S: PredictDelay(features)
  S -> AI: Gọi mô hình dự báo
  alt AI trả kết quả thành công
    AI --> S: Xác suất + mức rủi ro + khuyến nghị
    S --> C: Kết quả dự báo
    C -> R: Insert DUDOANAI, AI_FEATURE_STORE
    R -> DB: SaveChanges()
    C -> N: Cảnh báo rủi ro cao (nếu cần)
    C --> UI: Trả kết quả dự báo
  else Lỗi AI/timeout
    AI --> S: Exception
    S --> C: Lỗi dịch vụ AI
    C --> UI: Trả lỗi dự báo AI
  end
else Dữ liệu thiếu hoặc sai định dạng
  C --> UI: Trả lỗi validation
end
@enduml
```

## 7) AI feedback + intervention log/HITL
```plantuml
@startuml
actor Employee
actor Manager
participant "UI/AiInsights.cshtml / AiPerformance.cshtml" as UI
participant "AiController" as C
participant "AiEvaluationService" as E
participant "AppDbContext" as R
database "SQL Server" as DB
participant "AI Service" as AI

Employee -> UI: Gửi phản hồi dự báo AI
UI -> C: POST /ai/feedback
C -> E: Validate feedback
alt Feedback hợp lệ
  C -> R: Insert AI_FEEDBACK
  R -> DB: SaveChanges()
  C --> UI: Ghi nhận phản hồi thành công
else Dữ liệu feedback không hợp lệ
  C --> UI: Trả lỗi feedback
end

Manager -> UI: Ghi nhận can thiệp HITL
UI -> C: POST /ai/nhatky-can-thiep
C -> E: Kiểm tra quyền can thiệp
alt Đủ quyền và đối tượng tồn tại
  C -> R: Insert AI_NHATKY_CANTHIEP
  R -> DB: SaveChanges()
  C -> AI: Đồng bộ tín hiệu can thiệp (nếu cấu hình)
  AI --> C: Kết quả đồng bộ
  C --> UI: Trả kết quả can thiệp
else Không đủ quyền hoặc dữ liệu sai trạng thái
  C --> UI: Trả lỗi can thiệp
end
@enduml
```

## 8) Tạo báo cáo + export PDF/Excel
```plantuml
@startuml
actor Manager
participant "UI/CreateReport.cshtml / ReportManagement.cshtml" as UI
participant "ReportController" as C
participant "Report Service" as S
participant "AppDbContext" as R
database "SQL Server" as DB

Manager -> UI: Tạo báo cáo và nhập nội dung
UI -> C: POST /api/report/save-draft hoặc /submit
C -> S: Validate dữ liệu và trạng thái
alt Dữ liệu hợp lệ
  C -> R: Insert/Update BAOCAO + BAOCAOCHITIET
  R -> DB: SaveChanges()
  C --> UI: Trả kết quả lưu nháp/nộp
else Dữ liệu không hợp lệ
  C --> UI: Trả lỗi tạo báo cáo
end

Manager -> UI: Yêu cầu xuất PDF/Excel
UI -> C: POST /api/report/export-pdf hoặc /export-excel
C -> S: Sinh dữ liệu xuất
alt Báo cáo đủ điều kiện xuất
  S --> C: Tệp kết quả
  C --> UI: Trả file PDF/Excel
else Báo cáo không tồn tại hoặc trạng thái không hợp lệ
  C --> UI: Trả lỗi export
end
@enduml
```

## 9) Quản lý nhân viên + yêu cầu cập nhật hồ sơ
```plantuml
@startuml
actor Admin
actor Employee
actor Manager
participant "UI/Employees.cshtml / Profile.cshtml" as UI
participant "NhanVienController" as C
participant "Employee Profile Service" as S
participant "AppDbContext" as R
database "SQL Server" as DB

Admin -> UI: Tạo/Cập nhật nhân viên
UI -> C: POST/PUT /nhanvien
C -> S: Validate hồ sơ và ràng buộc
alt Hợp lệ
  C -> R: Save NHANVIEN
  R -> DB: SaveChanges()
  C --> UI: Trả kết quả thành công
else Sai dữ liệu/khóa ngoại
  C --> UI: Trả lỗi nghiệp vụ
end

Employee -> UI: Gửi yêu cầu cập nhật hồ sơ
UI -> C: POST /nhanvien/me/profile-change-requests
C -> R: Insert YEUCAU_CAPNHAT_HOSO
R -> DB: SaveChanges()

Manager -> UI: Duyệt/Từ chối yêu cầu
UI -> C: POST /nhanvien/profile-change-requests/{id}/approve|reject
alt Trạng thái yêu cầu hợp lệ
  C -> R: Update yêu cầu + hồ sơ (nếu approve)
  R -> DB: SaveChanges()
  C --> UI: Trả kết quả xử lý
else Đã xử lý hoặc không đủ quyền
  C --> UI: Trả lỗi xử lý yêu cầu
end
@enduml
```

## 10) Quản lý phòng ban/nhóm và thành viên
```plantuml
@startuml
actor Admin
actor Manager
participant "UI/Departments.cshtml" as UI
participant "PhongBanController/NhomController" as C
participant "Organization Service" as S
participant "AppDbContext" as R
database "SQL Server" as DB

Admin -> UI: Tạo/sửa/xóa phòng ban hoặc nhóm
UI -> C: POST/PUT/DELETE phongban, api/nhom
C -> S: Validate tổ chức
alt Hợp lệ
  C -> R: Save PHONGBAN/NHOM
  R -> DB: SaveChanges()
  C --> UI: Trả kết quả thành công
else Dữ liệu không hợp lệ hoặc quan hệ phụ thuộc
  C --> UI: Trả lỗi tổ chức
end

Manager -> UI: Thêm/xóa thành viên nhóm
UI -> C: POST add-member, DELETE member
alt Thành viên hợp lệ
  C -> R: Save THANHVIENNHOM
  R -> DB: SaveChanges()
  C --> UI: Cập nhật danh sách nhóm
else Nhân viên không hợp lệ hoặc trùng thành viên
  C --> UI: Trả lỗi thành viên nhóm
end
@enduml
```

## 11) Dashboard tổng hợp chỉ số
```plantuml
@startuml
actor Manager
participant "UI/Dashboard.cshtml" as UI
participant "DashboardController" as C
participant "Dashboard Aggregation Service" as S
participant "AppDbContext" as R
database "SQL Server" as DB

Manager -> UI: Chọn bộ lọc kỳ/thời gian/phạm vi
UI -> C: GET /dashboard
C -> S: Tổng hợp KPI, task, rủi ro, phê duyệt
S -> DB: Query KETQUAKPI, CONGVIEC, TIENDO, DUDOANAI, BAOCAO
DB --> S: Dữ liệu tổng hợp
alt Có dữ liệu hợp lệ
  S --> C: DashboardResponseDto
  C --> UI: Hiển thị dashboard
else Bộ lọc không hợp lệ hoặc không dữ liệu
  C --> UI: Trả dashboard rỗng hoặc thông báo lỗi
end
@enduml
```

## 12) Thông báo + đánh dấu đã đọc
```plantuml
@startuml
actor Employee
participant "UI/Notifications.cshtml" as UI
participant "ThongBaoController" as C
participant "Notification Runtime Service" as S
participant "AppDbContext" as R
database "SQL Server" as DB

Employee -> UI: Mở trung tâm thông báo
UI -> C: GET /thongbao, GET /thongbao/summary
C -> S: Tải danh sách thông báo theo người dùng
S -> DB: Query THONGBAO + THONGBAO_NHANVIEN
DB --> S: Danh sách + trạng thái đã đọc
S --> C: DTO thông báo
C --> UI: Hiển thị thông báo

Employee -> UI: Đánh dấu đã đọc
UI -> C: POST /thongbao/{id}/mark-read hoặc /mark-all-read
alt Thông báo thuộc quyền người dùng
  C -> R: Update THONGBAO_NHANVIEN.DaDoc
  R -> DB: SaveChanges()
  C --> UI: Trả kết quả cập nhật
else Không thuộc quyền truy cập
  C --> UI: Trả lỗi quyền
end
@enduml
```

## Mô tả ngắn
- **Thành phần tham gia:** Actor nghiệp vụ, UI/View, Controller/API, Service, AppDbContext, Database và dịch vụ ngoài (AI, Notification, Identity).
- **Dữ liệu chính:** tài khoản/role/claim, dữ liệu công việc-tiến độ, KPI, báo cáo, bản ghi dự báo và phản hồi AI, thông báo.
- **Kết quả đầu ra:** mỗi quy trình phản ánh đầy đủ luồng chính và nhánh ngoại lệ phục vụ phân tích thiết kế hệ thống.

