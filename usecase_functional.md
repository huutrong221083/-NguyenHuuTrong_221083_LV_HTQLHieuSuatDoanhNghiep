# Use Case Functional (Theo cụm chức năng)

## 1) Quản trị hệ thống & tài khoản
```plantuml
@startuml
left to right direction
actor Admin as A_Admin
actor "Identity Service" as A_Identity

rectangle "Cụm Quản trị hệ thống & tài khoản" {
  usecase "Đăng nhập hệ thống" as UC_Login
  usecase "Quản lý tài khoản người dùng" as UC_UserMgmt
  usecase "Quản lý vai trò (Role)" as UC_RoleMgmt
  usecase "Quản lý quyền chi tiết (Claim)" as UC_ClaimMgmt
  usecase "Khóa/Mở khóa, đặt lại mật khẩu" as UC_SecOps
  usecase "Xem sức khỏe danh tính hệ thống" as UC_IdentityHealth
  usecase "Ghi nhật ký quản trị" as UC_AuditAdmin
}

A_Admin --> UC_Login
A_Admin --> UC_UserMgmt
A_Admin --> UC_RoleMgmt
A_Admin --> UC_ClaimMgmt
A_Admin --> UC_SecOps
A_Admin --> UC_IdentityHealth
A_Identity --> UC_Login

UC_UserMgmt ..> UC_Login : <<include>>
UC_RoleMgmt ..> UC_Login : <<include>>
UC_ClaimMgmt ..> UC_Login : <<include>>
UC_SecOps ..> UC_Login : <<include>>
UC_IdentityHealth ..> UC_Login : <<include>>

UC_UserMgmt ..> UC_AuditAdmin : <<include>>
UC_RoleMgmt ..> UC_AuditAdmin : <<include>>
UC_ClaimMgmt ..> UC_AuditAdmin : <<include>>
UC_SecOps ..> UC_AuditAdmin : <<include>>
@enduml
```

### Use case tiêu biểu: Quản lý tài khoản người dùng
- **Mục tiêu:** Tạo mới, cập nhật và liên kết tài khoản với nhân viên theo chính sách bảo mật.
- **Actor chính:** Admin.
- **Tiền điều kiện:** Admin đã xác thực thành công và có quyền quản trị tài khoản.
- **Hậu điều kiện:** Tài khoản được cập nhật hợp lệ; lịch sử thao tác được ghi nhận.
- **Luồng chính:** Mở danh sách tài khoản -> chọn tạo/cập nhật -> kiểm tra dữ liệu và ràng buộc -> lưu thay đổi -> phản hồi thành công.
- **Luồng thay thế/ngoại lệ:** Trùng username/email; nhân viên liên kết không hợp lệ; không đủ quyền thao tác.

---

## 2) Nhân sự/cơ cấu tổ chức
```plantuml
@startuml
left to right direction
actor Admin as A_Admin
actor Manager as A_Manager
actor Employee as A_Employee

rectangle "Cụm Nhân sự/cơ cấu tổ chức" {
  usecase "Quản lý hồ sơ nhân viên" as UC_EmployeeProfileMgmt
  usecase "Gửi yêu cầu cập nhật hồ sơ cá nhân" as UC_ProfileChangeRequest
  usecase "Duyệt/Từ chối yêu cầu cập nhật hồ sơ" as UC_ProfileChangeReview
  usecase "Quản lý phòng ban" as UC_DepartmentMgmt
  usecase "Quản lý nhóm và thành viên nhóm" as UC_TeamMemberMgmt
  usecase "Quản lý kỹ năng nhân viên" as UC_EmployeeSkillMgmt
  usecase "Ghi nhật ký hoạt động nhân sự" as UC_AuditHr
}

A_Admin --> UC_EmployeeProfileMgmt
A_Admin --> UC_DepartmentMgmt
A_Admin --> UC_TeamMemberMgmt
A_Admin --> UC_EmployeeSkillMgmt
A_Manager --> UC_ProfileChangeReview
A_Manager --> UC_TeamMemberMgmt
A_Employee --> UC_ProfileChangeRequest
A_Employee --> UC_EmployeeSkillMgmt

UC_ProfileChangeReview ..> UC_EmployeeProfileMgmt : <<extend>>
UC_EmployeeProfileMgmt ..> UC_AuditHr : <<include>>
UC_DepartmentMgmt ..> UC_AuditHr : <<include>>
UC_TeamMemberMgmt ..> UC_AuditHr : <<include>>
UC_EmployeeSkillMgmt ..> UC_AuditHr : <<include>>
@enduml
```

### Use case tiêu biểu: Duyệt/Từ chối yêu cầu cập nhật hồ sơ
- **Mục tiêu:** Bảo đảm thay đổi thông tin nhân sự được kiểm soát trước khi áp dụng chính thức.
- **Actor chính:** Manager (hoặc người có thẩm quyền duyệt).
- **Tiền điều kiện:** Có yêu cầu ở trạng thái chờ duyệt; actor có quyền phê duyệt.
- **Hậu điều kiện:** Yêu cầu chuyển trạng thái duyệt/từ chối; dữ liệu hồ sơ được cập nhật nếu duyệt.
- **Luồng chính:** Xem danh sách yêu cầu -> mở chi tiết thay đổi -> chọn duyệt -> ghi nhận quyết định -> cập nhật hồ sơ nhân viên.
- **Luồng thay thế/ngoại lệ:** Yêu cầu đã xử lý trước đó; dữ liệu đề xuất không hợp lệ; actor không thuộc phạm vi phê duyệt.

---

## 3) Dự án - công việc - tiến độ
```plantuml
@startuml
left to right direction
actor Manager as A_Manager
actor Employee as A_Employee
actor "Notification/Email Service" as A_Notify

rectangle "Cụm Dự án - công việc - tiến độ" {
  usecase "Quản lý dự án" as UC_ProjectMgmt
  usecase "Phân bổ nguồn lực dự án\n(nhân viên/nhóm/phòng ban)" as UC_ProjectAssignment
  usecase "Tạo và cập nhật công việc" as UC_TaskCreateUpdate
  usecase "Phân công công việc" as UC_TaskAssignment
  usecase "Cập nhật tiến độ công việc" as UC_TaskProgressUpdate
  usecase "Duyệt/Từ chối tiến độ" as UC_TaskProgressReview
  usecase "Gửi thông báo điều phối" as UC_NotifyWorkflow
}

A_Manager --> UC_ProjectMgmt
A_Manager --> UC_ProjectAssignment
A_Manager --> UC_TaskCreateUpdate
A_Manager --> UC_TaskAssignment
A_Manager --> UC_TaskProgressReview
A_Employee --> UC_TaskProgressUpdate
A_Notify --> UC_NotifyWorkflow

UC_TaskAssignment ..> UC_ProjectAssignment : <<extend>>
UC_TaskProgressReview ..> UC_TaskProgressUpdate : <<extend>>
UC_TaskAssignment ..> UC_NotifyWorkflow : <<extend>>
UC_TaskProgressReview ..> UC_NotifyWorkflow : <<extend>>
@enduml
```

### Use case tiêu biểu: Cập nhật tiến độ công việc
- **Mục tiêu:** Ghi nhận tiến độ thực hiện công việc, phục vụ giám sát và đánh giá hiệu suất.
- **Actor chính:** Employee.
- **Tiền điều kiện:** Employee đã được phân công công việc hợp lệ.
- **Hậu điều kiện:** Bản ghi tiến độ được lưu; trạng thái phê duyệt phản ánh đúng vòng đời.
- **Luồng chính:** Mở công việc được giao -> nhập tỷ lệ hoàn thành và ghi chú -> gửi cập nhật -> hệ thống lưu tiến độ chờ duyệt -> thông báo quản lý.
- **Luồng thay thế/ngoại lệ:** Công việc không còn hiệu lực; dữ liệu phần trăm không hợp lệ; không đúng người được phân công.

---

## 4) KPI & đánh giá hiệu suất
```plantuml
@startuml
left to right direction
actor Manager as A_Manager
actor Employee as A_Employee

rectangle "Cụm KPI & đánh giá hiệu suất" {
  usecase "Quản lý danh mục KPI" as UC_KpiCatalog
  usecase "Đồng bộ gán KPI theo phạm vi\n(nhân viên/nhóm/phòng ban/dự án)" as UC_KpiAssignment
  usecase "Tính KPI theo kỳ đánh giá" as UC_KpiCalculate
  usecase "Đề xuất KPI" as UC_KpiProposal
  usecase "Duyệt/Từ chối đề xuất KPI" as UC_KpiProposalReview
  usecase "Xem KPI cá nhân/đội nhóm" as UC_KpiView
}

A_Manager --> UC_KpiCatalog
A_Manager --> UC_KpiAssignment
A_Manager --> UC_KpiCalculate
A_Manager --> UC_KpiProposalReview
A_Manager --> UC_KpiView
A_Employee --> UC_KpiProposal
A_Employee --> UC_KpiView

UC_KpiCalculate ..> UC_KpiAssignment : <<include>>
UC_KpiProposalReview ..> UC_KpiProposal : <<extend>>
@enduml
```

### Use case tiêu biểu: Tính KPI theo kỳ đánh giá
- **Mục tiêu:** Tính toán điểm KPI định kỳ dựa trên dữ liệu công việc và trọng số KPI đã cấu hình.
- **Actor chính:** Manager.
- **Tiền điều kiện:** Danh mục KPI và phạm vi áp dụng đã được cấu hình.
- **Hậu điều kiện:** Kết quả KPI kỳ đánh giá được lưu và có thể truy vấn báo cáo.
- **Luồng chính:** Chọn kỳ đánh giá -> chọn phạm vi tính -> hệ thống tổng hợp dữ liệu thực hiện -> tính điểm -> lưu kết quả và phản hồi.
- **Luồng thay thế/ngoại lệ:** Thiếu dữ liệu đầu vào; kỳ đánh giá không hợp lệ; cấu hình KPI chưa đầy đủ.

---

## 5) Báo cáo & dashboard
```plantuml
@startuml
left to right direction
actor Manager as A_Manager
actor Admin as A_Admin
actor "Notification/Email Service" as A_Notify

rectangle "Cụm Báo cáo & dashboard" {
  usecase "Tạo/Lưu nháp/Nộp báo cáo" as UC_ReportLifecycle
  usecase "Duyệt/Từ chối báo cáo" as UC_ReportReview
  usecase "Xuất báo cáo PDF/Excel" as UC_ReportExport
  usecase "Yêu cầu tạo báo cáo" as UC_ReportRequest
  usecase "Xem dashboard tổng hợp chỉ số" as UC_DashboardView
  usecase "Gửi thông báo trạng thái báo cáo" as UC_ReportNotify
}

A_Manager --> UC_ReportLifecycle
A_Manager --> UC_ReportRequest
A_Manager --> UC_DashboardView
A_Admin --> UC_ReportReview
A_Admin --> UC_DashboardView
A_Notify --> UC_ReportNotify

UC_ReportReview ..> UC_ReportLifecycle : <<extend>>
UC_ReportLifecycle ..> UC_ReportExport : <<extend>>
UC_ReportReview ..> UC_ReportNotify : <<extend>>
UC_ReportLifecycle ..> UC_ReportNotify : <<extend>>
@enduml
```

### Use case tiêu biểu: Tạo/Lưu nháp/Nộp báo cáo
- **Mục tiêu:** Chuẩn hóa quy trình lập báo cáo nghiệp vụ theo vòng đời bản nháp đến phê duyệt.
- **Actor chính:** Manager.
- **Tiền điều kiện:** Người dùng có quyền lập báo cáo và phạm vi dữ liệu hợp lệ.
- **Hậu điều kiện:** Báo cáo được lưu nháp hoặc chuyển trạng thái chờ duyệt.
- **Luồng chính:** Khởi tạo báo cáo -> nhập nội dung/chỉ số -> lưu nháp hoặc nộp -> hệ thống ghi nhận trạng thái mới.
- **Luồng thay thế/ngoại lệ:** Thiếu trường bắt buộc; kỳ dữ liệu không hợp lệ; quyền truy cập bị từ chối.

---

## 6) AI dự báo/đánh giá/feedback/HITL
```plantuml
@startuml
left to right direction
actor Manager as A_Manager
actor Employee as A_Employee
actor "AI Service" as A_Ai
actor "Notification/Email Service" as A_Notify

rectangle "Cụm AI dự báo/đánh giá/feedback/HITL" {
  usecase "Dự báo rủi ro trễ hạn công việc" as UC_AiPredictDelay
  usecase "Đánh giá hiệu năng mô hình AI" as UC_AiEvaluate
  usecase "Gửi phản hồi AI" as UC_AiFeedback
  usecase "Ghi nhận can thiệp HITL" as UC_AiHitl
  usecase "Đồng bộ cảnh báo AI" as UC_AiAlert
}

A_Manager --> UC_AiPredictDelay
A_Manager --> UC_AiEvaluate
A_Manager --> UC_AiHitl
A_Employee --> UC_AiFeedback
A_Ai --> UC_AiPredictDelay
A_Ai --> UC_AiEvaluate
A_Ai --> UC_AiAlert
A_Notify --> UC_AiAlert

UC_AiFeedback ..> UC_AiHitl : <<extend>>
UC_AiPredictDelay ..> UC_AiAlert : <<extend>>
UC_AiEvaluate ..> UC_AiAlert : <<extend>>
@enduml
```

### Use case tiêu biểu: Dự báo rủi ro trễ hạn công việc
- **Mục tiêu:** Cảnh báo sớm nguy cơ trễ hạn để hỗ trợ điều phối nguồn lực.
- **Actor chính:** Manager.
- **Tiền điều kiện:** Có dữ liệu công việc/tiến độ hợp lệ; dịch vụ AI khả dụng.
- **Hậu điều kiện:** Kết quả dự báo và mức rủi ro được lưu, có thể phát sinh cảnh báo.
- **Luồng chính:** Người dùng yêu cầu dự báo -> hệ thống chuẩn hóa dữ liệu đặc trưng -> gọi AI Service -> nhận kết quả xác suất/khuyến nghị -> lưu lịch sử dự báo.
- **Luồng thay thế/ngoại lệ:** Dịch vụ AI lỗi/timeout; dữ liệu đầu vào thiếu; mô hình chưa sẵn sàng.

