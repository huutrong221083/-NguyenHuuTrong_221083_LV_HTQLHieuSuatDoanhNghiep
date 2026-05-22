# UML Mapping (Use case/Sequence -> Controller -> Endpoint -> Entity)

## Mục đích
Bảng ánh xạ nhanh giữa các use case/sequence trọng yếu với controller, action/endpoint và thực thể dữ liệu chính, phục vụ truy vết thiết kế - hiện thực.

| Use case / Sequence | Controller | Endpoint/Action trọng yếu | Entity chính | Ghi chú |
|---|---|---|---|---|
| Đăng nhập + phân quyền truy cập | `AccountController` | `POST /Account/Login` | `ApplicationUser`, `IdentityRole`, `AspNetUserClaims` | Xác thực bằng Identity, gắn quyền theo role/claim |
| Quản trị tài khoản người dùng | `AccountManagementController` | `GET /account-management/accounts`, `POST /account-management/accounts`, `PUT /account-management/accounts/{userId}/role` | `ApplicationUser`, `NhanVien` | Admin quản trị vòng đời tài khoản và liên kết nhân sự |
| Quản lý vai trò/quyền | `SystemController` | `GET/POST/PUT/DELETE /system/roles`, `PUT /system/roles/{roleId}/claims` | `IdentityRole`, `IdentityRoleClaim` | Thiết lập policy truy cập cho module nghiệp vụ |
| Quản lý nhân viên | `NhanVienController` | `GET /nhanvien`, `POST /nhanvien`, `PUT /nhanvien/{id}`, `PUT /nhanvien/{id}/status` | `NhanVien`, `ChucVu`, `PhongBan` | Bao gồm cập nhật trạng thái hoạt động |
| Yêu cầu cập nhật hồ sơ | `NhanVienController` | `POST /nhanvien/me/profile-change-requests`, `POST /nhanvien/profile-change-requests/{id}/approve`, `.../reject` | `YeuCauCapNhatHoSo`, `NhanVien` | Luồng Employee gửi, Manager/Admin duyệt |
| Quản lý phòng ban | `PhongBanController` | `GET/POST/PUT/DELETE /phongban` | `PhongBan`, `NhanVien` | Bao gồm liên kết trưởng phòng và danh sách nhân viên |
| Quản lý nhóm và thành viên | `NhomController` | `POST /api/nhom`, `POST /api/nhom/add-member`, `DELETE /api/nhom/{maNhom}/members/{maNhanVien}` | `Nhom`, `ThanhVienNhom`, `NhanVien` | Điều phối cơ cấu nhóm nghiệp vụ |
| Quản lý dự án | `DuAnController` | `POST/PUT/DELETE /duan`, `POST /duan/{id}/nhanvien`, `.../nhom`, `.../phongban` | `DuAn`, `DuAnNhanVien`, `DuAnNhom`, `DuAnPhongBan` | Gán nguồn lực nhiều cấp cho dự án |
| Tạo công việc + phân công | `CongViecController` | `POST /congviec`, `POST /phancong/nhanvien`, `POST /congviec/{id}/assignments/nhom`, `.../phongban` | `CongViec`, `PhanCongNhanVien`, `PhanCongNhom`, `PhanCongPhongBan` | Manager điều phối công việc theo phạm vi |
| Cập nhật tiến độ + duyệt/từ chối | `CongViecController` | `POST /tiendo`, `PUT /tiendo/{id}/approve`, `PUT /tiendo/{id}/reject` | `TienDoCongViec`, `NhatKyCongViec`, `CongViec` | Luồng có trạng thái chờ duyệt/đã duyệt/từ chối |
| Quản lý danh mục KPI | `KpiController` | `GET /kpi/catalog`, `POST /kpi/catalog`, `PUT /kpi/catalog/{id}` | `DanhMucKpi`, `LoaiKpi` | Chuẩn hóa chỉ số và trọng số gốc |
| Tính KPI theo kỳ | `KpiController` | `POST /kpi/calculate`, `POST /kpi/calculate-all` | `KetQuaKpi`, `KetQuaKpiTong`, `KpiNhanVien/KpiNhom/KpiPhongBan/KpiDuAn` | Tổng hợp dữ liệu công việc và lưu kết quả định kỳ |
| Đề xuất KPI + review | `KpiController` | `POST /kpi/proposals`, `POST /kpi/proposals/{id}/review` | `DeXuatKpi`, `DanhMucKpi`, `NhanVien` | Employee đề xuất, Manager/Admin phê duyệt |
| Xem KPI cá nhân | `MyKpiController` | `GET /kpi/my/overview`, `GET /kpi/my/history`, `POST /kpi/my/feedback` | `KetQuaKpi`, `KetQuaKpiTong` | Màn hình KPI theo người dùng đăng nhập |
| Tạo/Nộp báo cáo | `ReportController` | `POST /api/report/save-draft`, `POST /api/report/submit`, `GET /api/report/list` | `BaoCao`, `BaoCaoChiTiet` | Vòng đời báo cáo từ nháp đến gửi duyệt |
| Duyệt/Từ chối báo cáo | `ReportController` | `PUT /api/report/review/approve`, `PUT /api/report/review/reject` | `BaoCao`, `YeuCauBaoCao` | Quản lý trạng thái nghiệp vụ báo cáo |
| Export PDF/Excel | `ReportController` | `POST /api/report/export-pdf`, `POST /api/report/export-excel` | `BaoCao`, `BaoCaoChiTiet` | Xuất dữ liệu trình bày phục vụ điều hành |
| Dashboard tổng hợp chỉ số | `DashboardController` | `GET /dashboard`, `GET /dashboard/export` | `KetQuaKpi`, `CongViec`, `TienDoCongViec`, `DuAn`, `BaoCao` | Tổng hợp KPI, tiến độ, rủi ro và cảnh báo |
| Quản lý thông báo | `ThongBaoController` | `GET /thongbao`, `GET /thongbao/summary`, `POST /thongbao/mark-all-read`, `POST /thongbao/{id}/mark-read` | `ThongBao`, `ThongBaoNhanVien`, `LoaiThongBao` | Đồng bộ thông báo và trạng thái đã đọc |
| AI dự báo rủi ro trễ hạn | `AiController` | `POST /ai/predict-delay`, `GET /ai/history` | `DuDoanAi`, `AiFeatureStore`, `MoHinhAi` | Sinh dự báo và lưu lịch sử/đặc trưng |
| AI đánh giá mô hình | `AiEvaluationController`, `AiController` | `POST /api/ai/evaluate/run`, `GET /ai/models/perf` | `AiDanhGiaRun`, `AiDanhGiaChiTiet`, `AiBusinessKpiRun` | Đánh giá chất lượng mô hình theo kỳ |
| AI feedback + HITL | `AiController` | `POST /ai/feedback`, `POST /ai/nhatky-can-thiep`, `GET /ai/intervention-log` | `AiFeedback`, `AiNhatKyCanThiep`, `DuDoanAi` | Thu hồi phản hồi người dùng và lịch sử can thiệp |

