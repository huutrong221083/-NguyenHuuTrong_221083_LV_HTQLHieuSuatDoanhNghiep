# Báo Cáo Kiểm Tra Nghiệp Vụ: Tạo Dự Án > Giao Dự Án > Tạo Công Việc > Giao Việc > Cập Nhật Tiến Độ > Duyệt Tiến Độ

**Ngày kiểm tra**: 11 tháng 5 năm 2026  
**Phiên bản**: LV2026 (ASP.NET Core 8.0)

---

## 📊 Kết Quả Tổng Quan

| Công Đoạn | API | UI | DB | Trạng Thái |
|-----------|-----|----|----|-----------|
| 1️⃣ Tạo Dự Án | ✅ | ✅ | ✅ | ✅ **HOÀN THIỆN** |
| 2️⃣ Giao Dự Án | ✅ | ✅ | ✅ | ✅ **HOÀN THIỆN** |
| 3️⃣ Tạo Công Việc | ✅ | ✅ | ✅ | ✅ **HOÀN THIỆN** |
| 4️⃣ Giao Việc | ⚠️ | ✅ | ✅ | ⚠️ **THIẾU XÓA** |
| 5️⃣ Cập Nhật Tiến Độ | ✅ | ✅ | ✅ | ✅ **HOÀN THIỆN** |
| 6️⃣ Duyệt Tiến Độ | ❌ | ❌ | ❌ | ❌ **HOÀN TOÀN THIẾU** |

---

## 🟢 PHẦN HOÀN THIỆN (Chạy Đúng)

### 1. Tạo Dự Án (Project Creation)
```
✅ API:      POST /duan
✅ UI:       Views/Portal/Projects.cshtml - Modal form
✅ Auth:     Permissions.ProjectsCreate (Admin/Manager)
✅ DB:       DuAn table
```

**Chi tiết công nghệ:**
- Controller: `DuAnController.CreateDuAn()`
- Request: `UpsertDuAnRequest` (TenDuAn, MoTa, NgayBatDau, NgayKetThuc, TrangThai, MaPhongBan)
- Tự động tạo `DuAnPhongBan` nếu có MaPhongBan

---

### 2. Giao Dự Án (Project Assignment)

#### A. Giao cho Nhân Viên Cá Nhân
```
✅ API:      POST /duan/{id}/nhanvien
✅ UI:       Button "Gán" (btnAssignEmployee)
✅ DB:       DuAnNhanVien table
✅ Remove:   DELETE /duan/{id}/nhanvien/{maNhanVien}
```

#### B. Giao cho Nhóm (Team)
```
✅ API:      POST /duan/{id}/nhom
✅ UI:       Button "Gán nhóm" (btnAssignTeam)
✅ DB:       DuAnNhom table
✅ Remove:   DELETE /duan/{id}/nhom/{maNhom}
```

#### C. Giao cho Phòng Ban (Department)
```
✅ API:      POST /duan/{id}/phongban (hoặc auto via MaPhongBan)
✅ UI:       Button "Gán PB" (btnAssignDepartment)
✅ DB:       DuAnPhongBan table
✅ Remove:   DELETE /duan/{id}/phongban/{maPhongBan}
```

---

### 3. Tạo Công Việc (Task Creation)
```
✅ API:      POST /congviec
✅ UI:       Views/Portal/Tasks.cshtml - Modal form
✅ Auth:     Permissions.TasksCreate (Admin/Manager)
✅ DB:       CongViec table
✅ Fields:   TenCongViec, MoTa, MaDuAn, MaCongViecCha, MaDoKho, MaDoUuTien, 
             NgayBatDau, Deadline, MaTrangThai, DiemCongViec
```

---

### 4. Giao Việc - PHẦN HOÀN THIỆN (Tạo), PHẦN THIẾU (Xóa)

#### A. Giao cho Nhân Viên ⚠️
```
✅ CREATE:   POST /phancong/nhanvien
✅ UI:       Button "Giao" (btnAssignEmployee)
✅ DB:       PhanCongNhanVien table
❌ DELETE:   KHÔNG CÓ ENDPOINT!!! (CRITICAL)
❌ UI:       Không thể xóa assignment sau khi tạo
```

#### B. Giao cho Nhóm ✅
```
✅ CREATE:   POST /congviec/{id}/assignments/nhom
✅ UI:       Button "Giao" (btnAssignTeam)
✅ DB:       PhanCongNhom table
✅ DELETE:   DELETE /congviec/{id}/assignments/nhom/{maNhom}
```

#### C. Giao cho Phòng Ban ✅
```
✅ CREATE:   POST /congviec/{id}/assignments/phongban
✅ UI:       Button "Giao" (btnAssignDepartment)
✅ DB:       PhanCongPhongBan table
✅ DELETE:   DELETE /congviec/{id}/assignments/phongban/{maPhongBan}
```

---

### 5. Cập Nhật Tiến Độ (Progress Update)
```
✅ API:      POST /tiendo
✅ UI:       Task detail drawer - saveProgress()
✅ Auth:     Permissions.TasksEdit (Admin/Manager/Employee)
✅ DB:       TienDoCongViec + NhatKyCongViec tables
✅ Fields:   MaCongViec, PhanTramHoanThanh, GhiChu
```

**Chi tiết:**
- Nhân viên submit: POST /tiendo
- Tạo TienDoCongViec: PhanTramHoanThanh, TrangThaiHienTai, NgayCapNhat
- Tạo NhatKyCongViec: Audit log của các updates
- Status tự động cập nhật: 1 (Chưa bắt đầu) → 2 (Đang làm) → 3 (Hoàn thành)

---

## 🔴 PHẦN THIẾU HOÀN TOÀN (Critical Issues)

### 6. Duyệt Tiến Độ (Progress Approval) - ❌ **HOÀN TOÀN KHÔNG CÓ**

**Vấn đề hiện tại:**
- ❌ Không có endpoint để quản lý xem danh sách tiến độ chờ duyệt
- ❌ Không có endpoint để phê duyệt tiến độ
- ❌ Không có endpoint để từ chối tiến độ
- ❌ TienDoCongViec không có trường lưu trạng thái phê duyệt
- ❌ Không có UI/Dashboard cho quản lý để duyệt
- ❌ Không có lịch sử phê duyệt/từ chối
- ❌ Không có notification cho nhân viên khi tiến độ bị từ chối

**Quy trình cần phải có:**
```
NHÂN VIÊN (Employee)
├─ Cập nhật tiến độ → POST /tiendo
│  └─ TienDoCongViec được tạo với TrangThaiPheDuyet = "Chờ duyệt"
└─ Xem trạng thái phê duyệt trong detail drawer

QUẢN LÝ (Manager/Supervisor)
├─ Xem danh sách tiến độ chờ duyệt
│  └─ GET /tiendo?trangthai=pending (hoặc tương tự)
├─ Xem chi tiết update + lịch sử công việc
├─ Phê duyệt tiến độ → PUT /tiendo/{id}/approve
│  └─ TrangThaiPheDuyet = "Đã duyệt", NgayPheDuyet = now()
└─ Từ chối tiến độ → PUT /tiendo/{id}/reject
   └─ TrangThaiPheDuyet = "Từ chối", LyDoTuChoi = "..."

NHÂN VIÊN (Sau khi quản lý duyệt)
└─ Xem trạng thái: ✅ "Đã duyệt" hoặc ❌ "Từ chối - [lý do]"
```

---

## 🟡 CÁC VẤN ĐỀ KHÁC (Medium Priority)

### 1. Xóa Assignment Nhân Viên từ Công Việc - ⚠️ **THIẾU**

**Vấn đề:**
- Có thể tạo assignment cho nhân viên: ✅ POST /phancong/nhanvien
- Không thể xóa/hủy assignment nhân viên: ❌ Không có DELETE endpoint
- So sánh: Team/Department assignments có DELETE endpoint đầy đủ

**Cần thêm:**
```csharp
// CongViecController.cs
[HttpDelete("{id:int}/assignments/nhanvien/{maNhanVien:int}")]
[Authorize(Policy = Permissions.TasksAssign)]
public async Task<ActionResult<ApiResponse<object>>> RemoveEmployeeAssignment(
    int id, int maNhanVien)
{
    var existing = await _dbContext.PhanCongNhanViens
        .FirstOrDefaultAsync(x => x.MaCongViec == id && x.MaNhanVien == maNhanVien);
    
    if (existing == null)
        return NotFound(ApiResponse<object>.Fail("Không tìm thấy phân công."));
    
    _dbContext.PhanCongNhanViens.Remove(existing);
    await _dbContext.SaveChangesAsync();
    
    return Ok(ApiResponse<object>.Ok(null, "Đã gỡ phân công nhân viên"));
}
```

---

## 📋 Bảng Kiểm Tra Chi Tiết

### **Công Đoạn 1: Tạo Dự Án**
- [x] API endpoint POST /duan
- [x] UI form modal
- [x] Authorization check
- [x] Validation (tên dự án, dates)
- [x] Audit log
- [x] Tự động tạo DuAnPhongBan nếu có department

### **Công Đoạn 2: Giao Dự Án**
- [x] POST /duan/{id}/nhanvien (Employee assignment)
- [x] POST /duan/{id}/nhom (Team assignment)
- [x] POST /duan/{id}/phongban (Department assignment)
- [x] DELETE endpoints cho cả 3 loại
- [x] UI buttons: "Gán", "Gán nhóm", "Gán PB"
- [x] UI list with removal buttons
- [x] Authorization checks
- [x] Audit logs

### **Công Đoạn 3: Tạo Công Việc**
- [x] API endpoint POST /congviec
- [x] UI form modal
- [x] Support parent task (MaCongViecCha)
- [x] Support multiple priority levels
- [x] Support difficulty levels
- [x] Authorization check (TasksCreate)
- [x] Validation
- [x] Audit log

### **Công Đoạn 4: Giao Việc**
- [x] POST /phancong/nhanvien (Employee)
- [x] POST /congviec/{id}/assignments/nhom (Team)
- [x] POST /congviec/{id}/assignments/phongban (Department)
- [ ] ❌ DELETE /phancong/nhanvien/{maNhanVien} - **MISSING**
- [x] DELETE /congviec/{id}/assignments/nhom/{maNhom}
- [x] DELETE /congviec/{id}/assignments/phongban/{maPhongBan}
- [x] UI buttons for creation
- [ ] ❌ UI buttons for removal (Employee) - **MISSING**
- [x] Authorization checks
- [x] Tạo assignment trong create task form (portal-task-form.js)

### **Công Đoạn 5: Cập Nhật Tiến Độ**
- [x] POST /tiendo endpoint
- [x] TienDoCongViec table
- [x] NhatKyCongViec logging
- [x] Auto-update task status (1 → 2 → 3)
- [x] UI input fields (percent, note)
- [x] Save button with loading state
- [x] Authorization (TasksEdit)
- [ ] ❌ Approval status tracking - **MISSING**
- [ ] ❌ Approval history - **MISSING**

### **Công Đoạn 6: Duyệt Tiến Độ**
- [ ] ❌ GET endpoint list progress updates
- [ ] ❌ GET endpoint for pending approvals
- [ ] ❌ PUT endpoint to approve
- [ ] ❌ PUT endpoint to reject
- [ ] ❌ TienDoCongViec columns for approval (TrangThaiPheDuyet, NgayPheDuyet, NguoiPheDuyet, LyDoTuChoi)
- [ ] ❌ UI dashboard/page for manager approval queue
- [ ] ❌ UI detail view in task for approval status
- [ ] ❌ Authorization policy for approval (TasksApprove)
- [ ] ❌ Notification to employee when rejected
- [ ] ❌ Audit log for approvals

---

## 💡 Khuyến Cáo Chi Tiết

### **Priority 1 - CRITICAL (Phải sửa ngay)**

#### Issue 1.1: Implement Progress Approval Workflow

**File cần tạo/sửa:**
1. `Models/TaskEntities.cs` - Thêm fields vào TienDoCongViec
2. `Migrations/` - Create migration để add columns
3. `Controllers/Api/CongViecController.cs` - Thêm endpoints
4. `Views/Portal/Tasks.cshtml` - Thêm UI approval status
5. `Views/Portal/ProgressApproval.cshtml` - Tạo page mới cho manager

**SQL Migration:**
```sql
ALTER TABLE TIENDO_CONGVIEC ADD
    TRANGTHAI_PHE_DUYET NVARCHAR(50) DEFAULT 'Chờ duyệt',
    NGAY_PHE_DUYET DATETIME NULL,
    NGUOI_PHE_DUYET INT NULL,
    LY_DO_TU_CHOI NVARCHAR(500) NULL;
```

**Endpoints cần thêm:**
```
GET    /tiendo?trangthai=pending
GET    /tiendo/{id}
PUT    /tiendo/{id}/approve
PUT    /tiendo/{id}/reject
GET    /tiendo/statistics (optional - for dashboard)
```

**Authorization:**
- New policy: `Permissions.TasksApprove` (for Manager/Admin)

---

### **Priority 2 - HIGH**

#### Issue 2.1: Add Missing Employee Assignment Removal Endpoint

**File:** `Controllers/Api/CongViecController.cs`

```csharp
[HttpDelete("{id:int}/assignments/nhanvien/{maNhanVien:int}")]
[Authorize(Policy = Permissions.TasksAssign)]
public async Task<ActionResult<ApiResponse<object>>> RemoveEmployeeAssignment(
    int id, int maNhanVien)
{
    var existing = await _dbContext.PhanCongNhanViens
        .FirstOrDefaultAsync(x => x.MaCongViec == id && x.MaNhanVien == maNhanVien);

    if (existing == null)
    {
        return NotFound(ApiResponse<object>.Fail("Không tìm thấy phân công nhân viên."));
    }

    _dbContext.PhanCongNhanViens.Remove(existing);
    await _dbContext.SaveChangesAsync();

    return Ok(ApiResponse<object>.Ok(null, "Đã gỡ phân công nhân viên"));
}
```

**UI Update:** `Views/Portal/Tasks.cshtml`
- Add button to remove assignment in detail view
- Link to DELETE endpoint

---

### **Priority 3 - NICE TO HAVE**

#### Issue 3.1: Create TienDoController
- Move approval-related endpoints from CongViecController to dedicated TienDoController
- Better separation of concerns
- Cleaner API routing

#### Issue 3.2: Add Progress Approval Dashboard
- Page: `/Portal/ProgressApproval` or `/Portal/ApprovalQueue`
- Show: List of pending approvals grouped by task/project
- Filter: By assignee, project, date range
- Actions: Approve/Reject with comment

---

## 📝 File Checklist to Review

- [x] Controllers/Api/DuAnController.cs - Project CRUD + assignments
- [x] Controllers/Api/CongViecController.cs - Task CRUD + progress + assignments
- [ ] ❓ Controllers/Api/TienDoController.cs - **SHOULD EXIST but doesn't**
- [x] Views/Portal/Projects.cshtml - Project management UI
- [x] Views/Portal/Tasks.cshtml - Task management UI
- [ ] Views/Portal/ProgressApproval.cshtml - **MISSING** - Approval dashboard
- [x] Models/TaskEntities.cs - Task domain models
- [ ] ❓ Models/TaskEntities.cs - TienDoCongViec needs approval fields
- [x] Data/AppDbContext.cs - DB mappings
- [ ] ❓ Migrations - Need approval-related migration
- [x] Contracts/Permissions.cs - Permission policies
- [ ] ❓ Contracts/Permissions.cs - Need TasksApprove policy
- [x] wwwroot/js/portal-task-form.js - Task creation form
- [ ] Contracts/AiDtos.cs - **Check if approval status included**

---

## 🎯 Kết Luận

### Status Tổng Quát
- **Công đoạn 1-5**: 80% hoàn thiện (chỉ thiếu xóa assignment nhân viên)
- **Công đoạn 6**: 0% - Hoàn toàn chưa implement

### Khuyến Cáo Ưu Tiên
1. **Ngay**: Implement progress approval workflow (Priority 1)
2. **Nhanh**: Thêm endpoint xóa employee assignment (Priority 2)
3. **Tuần sau**: Refactor thành TienDoController + UI dashboard (Priority 3)

### Ảnh Hưởng Kinh Doanh
- **Hiện tại**: Manager không thể kiểm soát/xác minh tiến độ công việc → Risk cao
- **Cần sửa**: Để đảm bảo Quality Management & Accountability

---

## 📞 Chi Tiết Triển Khai

Tất cả code files, migrations, và UI updates sẽ được thực hiện trong next session.
Ước tính timeline:
- Progress Approval: **2-3 giờ** (APIs + UI + migration)
- Employee Assignment Removal: **30 phút**
- Testing & fixes: **1 giờ**
- **Total: ~4-5 giờ work**

---

**Generated**: 2026-05-11 | **Tool**: Workflow Analyzer | **Version**: 1.0
