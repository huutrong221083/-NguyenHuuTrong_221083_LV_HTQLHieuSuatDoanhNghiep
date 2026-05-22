# Roadmap 3 Tuần - Module KPI Quản Lý (Bản Đơn Giản)

## Mục tiêu chốt phạm vi
Triển khai đủ 5 menu:
1. Dashboard KPI
2. KPI Nhân viên
3. KPI Nhóm
4. AI KPI
5. Báo cáo KPI

Phạm vi này ưu tiên:
- Dễ demo luận văn
- Có AI rõ ràng
- Không sa đà workflow phức tạp

---

## Kiến trúc màn hình đề xuất
- `Portal/Kpi` dùng cho `Dashboard KPI + KPI Nhân viên + KPI Nhóm` (3 tab rõ ràng).
- `Portal/AiInsights` dùng cho `AI KPI`.
- `Portal/ReportManagement` dùng cho `Báo cáo KPI`.

Không tạo quá nhiều page mới để giảm rủi ro.

---

## Tuần 1 - Dashboard KPI + KPI Nhân viên

### A. Dashboard KPI (tab 1 trong `Kpi.cshtml`)
Hiển thị:
- KPI trung bình phòng/nhóm
- Số nhân viên đạt KPI
- Số nhân viên KPI thấp
- Task đúng hạn
- Task trễ hạn
- Top nhân viên hiệu suất cao

### B. KPI Nhân viên (tab 2 trong `Kpi.cshtml`)
Hiển thị bảng:
- Nhân viên
- KPI
- Công việc hoàn thành
- Trễ hạn

Bộ lọc:
- Tháng
- Phòng ban
- Nhóm
- Mức KPI

Chi tiết khi click nhân viên:
- KPI hiện tại
- Tổng task
- Task đúng hạn
- Task trễ hạn
- Biểu đồ KPI theo tháng

### API cần bổ sung/sửa
- Trong [KpiController.cs](C:\Users\trong\Downloads\LuanVan\Luanvan2026\LuanVan\LuanVan\Controllers\Api\KpiController.cs):
1. `GET /kpi/dashboard-summary`
2. `GET /kpi/employees`
3. `GET /kpi/employees/{id}/detail`

Gợi ý tận dụng:
- `GET /kpi/nhanvien/{id}` (đã có) để lấy lịch sử KPI.
- `KpiService` + `CONGVIEC`, `PHANCONGNHANVIEN`, `TIENDOCONGVIEC` để thống kê task.

### File UI cần sửa
- [Kpi.cshtml](C:\Users\trong\Downloads\LuanVan\Luanvan2026\LuanVan\LuanVan\Views\Portal\Kpi.cshtml)
  - Tách 3 tab rõ: Dashboard, Nhân viên, Nhóm
  - Bảng KPI nhân viên + panel chi tiết

### Tiêu chí Done tuần 1
- Manager vào `Portal/Kpi` thấy dashboard số liệu đúng.
- Lọc KPI nhân viên hoạt động theo tháng/phòng/nhóm/mức KPI.
- Click nhân viên xem chi tiết + chart KPI theo tháng.

---

## Tuần 2 - KPI Nhóm + AI KPI

### A. KPI Nhóm (tab 3 trong `Kpi.cshtml`)
Hiển thị:
- KPI trung bình nhóm
- KPI trung bình phòng ban
- Nhóm tốt nhất
- Nhóm KPI thấp

### API cần bổ sung
- Trong [KpiController.cs](C:\Users\trong\Downloads\LuanVan\Luanvan2026\LuanVan\LuanVan\Controllers\Api\KpiController.cs):
1. `GET /kpi/team-summary`
2. `GET /kpi/department-summary`

### B. AI KPI (giữ ở `AiInsights.cshtml`)
Chốt 3 tính năng demo:
1. Dự đoán nguy cơ trễ deadline (`POST /ai/predict-delay`)
2. Cảnh báo KPI giảm/task trễ nhiều (từ rule + classify)
3. Đề xuất cải thiện (`POST /ai/suggest-employee`)

### File UI cần sửa
- [AiInsights.cshtml](C:\Users\trong\Downloads\LuanVan\Luanvan2026\LuanVan\LuanVan\Views\Portal\AiInsights.cshtml)
  - Đưa về ngôn ngữ KPI manager (bớt kỹ thuật model)
  - 3 khối rõ: Dự đoán, Cảnh báo, Đề xuất
- [Kpi.cshtml](C:\Users\trong\Downloads\LuanVan\Luanvan2026\LuanVan\LuanVan\Views\Portal\Kpi.cshtml)
  - Bổ sung bảng/biểu đồ KPI nhóm

### Tiêu chí Done tuần 2
- Manager xem được KPI nhóm/phòng ban theo kỳ.
- AI trả được dự báo trễ hạn + danh sách cảnh báo + gợi ý phân bổ nhân sự.

---

## Tuần 3 - Báo cáo KPI + Thông báo KPI + hoàn thiện demo

### A. Báo cáo KPI
Sử dụng nền có sẵn:
- [ReportController.cs](C:\Users\trong\Downloads\LuanVan\Luanvan2026\LuanVan\LuanVan\Controllers\Api\ReportController.cs)
- [ReportManagement.cshtml](C:\Users\trong\Downloads\LuanVan\Luanvan2026\LuanVan\LuanVan\Views\Portal\ReportManagement.cshtml)

Hoàn thiện:
- Mẫu báo cáo KPI theo tháng/quý
- Export PDF/Excel(CSV)
- Bổ sung bộ lọc KPI-focused

### B. Thông báo KPI cho manager
Tận dụng:
- [ThongBaoController.cs](C:\Users\trong\Downloads\LuanVan\Luanvan2026\LuanVan\LuanVan\Controllers\Api\ThongBaoController.cs)

Bổ sung rule tạo thông báo:
- KPI thấp dưới ngưỡng
- KPI giảm liên tục
- Task trễ hạn cao
- KPI vượt mục tiêu

### C. Tinh chỉnh phân quyền/menu
- Kiểm tra permission manager trong [Permissions.cs](C:\Users\trong\Downloads\LuanVan\Luanvan2026\LuanVan\LuanVan\Contracts\Permissions.cs)
- Menu cuối cùng chỉ giữ 5 mục KPI

### Tiêu chí Done tuần 3
- Xuất báo cáo KPI chạy ổn (PDF/Excel/CSV).
- Manager nhận thông báo KPI quan trọng.
- Demo end-to-end thông suốt theo kịch bản luận văn.

---

## Mapping chức năng -> code hiện có -> việc cần làm

1. Dashboard KPI
- Có sẵn: mini dashboard ở `Kpi.cshtml`
- Cần làm: API tổng hợp chuẩn + card số liệu manager

2. KPI Nhân viên
- Có sẵn: `GET /kpi/nhanvien/{id}`
- Cần làm: list/filter/detail đầy đủ cho manager

3. KPI Nhóm
- Có sẵn: dữ liệu gán `KPI_NHOM`, `KPI_PHONGBAN`
- Cần làm: endpoint summary + UI so sánh nhóm/phòng

4. AI KPI
- Có sẵn: `predict-delay`, `classify-performance`, `suggest-employee`
- Cần làm: đóng gói thành màn nghiệp vụ manager dễ hiểu

5. Báo cáo KPI
- Có sẵn: `ReportManagement` + export
- Cần làm: template KPI theo tháng/quý + bộ lọc gọn

---

## Kịch bản demo luận văn (khuyên dùng)
1. Vào Dashboard KPI xem toàn cảnh.
2. Mở KPI Nhân viên, lọc theo tháng/phòng, chọn 1 nhân viên xem chi tiết.
3. Mở KPI Nhóm xem nhóm tốt nhất và nhóm thấp.
4. Mở AI KPI chạy dự báo/cảnh báo/gợi ý.
5. Mở Báo cáo KPI, xuất PDF.
6. Kiểm tra Notification nhận cảnh báo KPI.

---

## Checklist nghiệm thu cuối
- [ ] Manager xem được KPI tổng quan theo kỳ.
- [ ] Manager theo dõi KPI nhân viên có filter + detail chart.
- [ ] Manager xem KPI nhóm/phòng ban.
- [ ] AI có dự đoán + cảnh báo + đề xuất.
- [ ] Xuất báo cáo KPI PDF/Excel/CSV.
- [ ] Có thông báo KPI quan trọng.
- [ ] Demo 5 menu chạy mượt không lỗi chính.

