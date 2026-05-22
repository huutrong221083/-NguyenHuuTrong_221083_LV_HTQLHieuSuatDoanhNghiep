# Sequence Diagram Mermaid - LuanVan 2026

## 1) Đăng nhập (AccountController + Identity)
```mermaid
sequenceDiagram
    title 1) Đăng nhập (AccountController + Identity)
    actor U as Người dùng
    participant V as Boundary: Login View
    participant C as Control: AccountController
    participant I as Control: SignInManager/UserManager
    participant DB as Entity: SQL Server
    participant D as Boundary: Portal Dashboard

    U->>V: Nhập user/email + password
    V->>C: POST /Account/Login
    C->>I: FindByEmail/FindByName
    I->>DB: Query user + trạng thái nhân viên
    DB-->>I: user/profile
    I-->>C: auth context

    alt user không tồn tại
        C-->>V: Sai tài khoản hoặc mật khẩu

    else tài khoản bị khóa/vô hiệu hóa
        C-->>V: Tài khoản bị khóa hoặc vô hiệu hóa

    else hợp lệ
        C->>I: CheckPasswordSignInAsync
        alt sai mật khẩu
            C->>I: AccessFailedAsync
            C-->>V: Sai mật khẩu / có thể bị khóa tạm
        else đúng mật khẩu
            C->>I: ResetAccessFailed + SignInAsync

            C-->>V: Login success
            V->>D: Redirect /Portal/Dashboard
        end
    end
```

## 2) Tạo công việc + phân công (CongViecController)
```mermaid
sequenceDiagram
    title 2) Tạo công việc + phân công (CongViecController)
    actor M as Quản lý
    participant V as Boundary: UI Công việc
    participant C as Control: CongViecController
    participant DB as Entity: SQL Server
    participant N as Service: NotificationService

    M->>V: Nhập thông tin công việc
    V->>C: POST /congviec
    alt dữ liệu không hợp lệ
        C-->>V: 400 BadRequest

    else hợp lệ
        C->>DB: Insert CONGVIEC
        opt auto trạng thái theo ngày bắt đầu
            C->>C: ResolveTaskStatusForCreate()
        end
        C-->>V: Tạo công việc thành công
    end

    par phân công nhân viên
        V->>C: POST /congviec/assignments/nhanvien
        C->>DB: Upsert PHANCONGNHANVIEN
    and phân công nhóm
        V->>C: POST /congviec/{id}/assignments/nhom
        C->>DB: Upsert PHANCONGNHOM
    and phân công phòng ban
        V->>C: POST /congviec/{id}/assignments/phongban
        C->>DB: Upsert PHANCONGPHONGBAN
    end

    opt có người nhận hợp lệ
        C->>N: Gửi thông báo giao việc
    end
```

## 3) Cập nhật tiến độ (POST /tiendo)
```mermaid
sequenceDiagram
    title 3) Cập nhật tiến độ (POST /tiendo)
    actor E as Nhân viên
    participant V as Boundary: UI Công việc cá nhân
    participant C as Control: CongViecController
    participant DB as Entity: SQL Server
    participant N as Service: NotificationService

    E->>V: Nhập % + ghi chú
    V->>C: POST /tiendo
    C->>DB: Check actor scope + quyền
    alt không có quyền/ngoài phạm vi
        C-->>V: 403

    else hợp lệ
        C->>DB: Insert TIENDOCONGVIEC
        alt cần duyệt
            C->>DB: TrangThaiPheDuyet = "Chờ duyệt"
            C->>N: Notify quản lý
        else không cần duyệt
            C->>DB: TrangThaiPheDuyet = "Đã duyệt"
        end
        C->>DB: Insert NHATKYCONGVIEC
        C-->>V: Cập nhật thành công
    end
```

## 4) Duyệt/Từ chối tiến độ
```mermaid
sequenceDiagram
    title 4) Duyệt/Từ chối tiến độ
    actor M as Quản lý
    participant V as Boundary: UI Duyệt tiến độ
    participant C as Control: CongViecController
    participant DB as Entity: SQL Server

    alt duyệt
        V->>C: PUT /tiendo/{id}/approve
    else từ chối
        V->>C: PUT /tiendo/{id}/reject
    end

    C->>DB: Load TIENDOCONGVIEC
    alt không tồn tại
        C-->>V: 404

    else trạng thái != "Chờ duyệt"
        C-->>V: 400 Đã xử lý trước đó

    else ngoài phạm vi quản lý
        C-->>V: 403

    else hợp lệ
        alt approve
            C->>DB: update TrangThaiPheDuyet = "Đã duyệt"
                C->>DB: update CONGVIEC.PhanTramHoanThanh + MaTrangThai

        else reject
            C->>DB: update TrangThaiPheDuyet = "Từ chối" + LyDoTuChoi
        end
        C-->>V: Xử lý thành công
    end
```

## 5) Tính KPI theo kỳ (KpiController + KpiService)
```mermaid
sequenceDiagram
    title 5) Tính KPI theo kỳ (KpiController + KpiService)
    actor A as Admin/Quản lý
    participant V as Boundary: Giao diện KPI
    participant C as Control: KpiController
    participant S as Service: KpiService
    participant DB as Entity: SQL Server

    A->>V: Chọn tháng/năm/kỳ
    alt tính một KPI
        V->>C: POST /kpi/calculate
    else tính all active
        V->>C: POST /kpi/calculate-all
    end

    C->>DB: kiểm tra KPI catalog + trạng thái
    alt KPI không tồn tại / tạm dừng / kỳ không hợp lệ
        C-->>V: lỗi dữ liệu

    else hợp lệ
        C->>S: CalculateAsync(request)
        loop từng nhân viên thuộc scope
            S->>DB: đọc task/progress/weight
            S->>DB: upsert KETQUAKPI + KETQUAKPI_TONG
        end
        S-->>C: result tổng hợp
        C-->>V: Tính KPI thành công
    end
```

## 6) Đề xuất KPI + review đề xuất
```mermaid
sequenceDiagram
    title 6) Đề xuất KPI + review đề xuất
    actor P as Quản lý/Admin đề xuất
    actor R as Admin duyệt
    participant V as Boundary: Giao diện đề xuất KPI
    participant C as Control: KpiController
    participant DB as Entity: SQL Server
    participant N as Service: NotificationService

    P->>V: Nhập đề xuất KPI
    V->>C: POST /kpi/proposals
    C->>DB: validate + check duplicate pending
    alt dữ liệu không hợp lệ / trùng pending
        C-->>V: 400/409

    else hợp lệ
        C->>DB: insert DE_XUAT_KPI (ChoDuyet)
        opt gửi notify admin
            C->>N: KPI_PROPOSAL
        end
        C-->>V: tạo đề xuất thành công
    end

    R->>V: duyệt / từ chối / yêu cầu chỉnh sửa
    V->>C: POST /kpi/proposals/{id}/review
    C->>DB: begin transaction + load proposal
    alt không tồn tại / đã đóng
        C-->>V: lỗi trạng thái
    else action = YeuCauChinhSua
        C->>DB: TrangThai = CanChinhSua
    else action = TuChoi
        C->>DB: TrangThai = TuChoi
    else action = Duyet
        C->>DB: ApplyApprovedProposalAsync()
        C->>DB: TrangThai = DaDuyet
    end
    C->>DB: commit
    opt gửi notify người đề xuất
        C->>N: kết quả review
    end
```

## 7) AI dự báo rủi ro trễ hạn
```mermaid
sequenceDiagram
    title 7) AI dự báo rủi ro trễ hạn
    actor M as Quản lý
    participant V as Boundary: Giao diện AI
    participant C as Control: AiController
    participant S as Service: AiPredictionService
    participant DB as Entity: SQL Server
    participant N as Service: NotificationService

    M->>V: Mở dự báo
    V->>C: POST /ai/predict-delay
    alt payload null/không hợp lệ
        C-->>V: 400

    else hợp lệ
        C->>S: PredictDelayAsync(command)
        S->>DB: build feature + validate data
        alt AI runtime tắt / dữ liệu thiếu
            S-->>C: Fail
            C-->>V: 400 fail message
        else lỗi model
            S->>S: fallback prediction
            S->>DB: save DUDOANAI
            S-->>C: success (fallback)
        else dự báo chuẩn
            S->>DB: save DUDOANAI + feature snapshots
            S-->>C: success
        end
        opt risk level cao
            C->>N: gửi cảnh báo
        end
        C-->>V: trả kết quả dự báo
    end
```

## 8) Feedback AI + HITL intervention
```mermaid
sequenceDiagram
    title 8) Feedback AI + HITL intervention
    actor U as Quản lý/Nhân viên
    participant V as Boundary: Giao diện AI
    participant C as Control: AiController
    participant DB as Entity: SQL Server
    participant N as Service: NotificationService

    U->>V: Gửi feedback
    V->>C: POST /ai/feedback
    alt thiếu dữ liệu bắt buộc
        C-->>V: 400

    else hợp lệ
        C->>DB: validate range (DoChinhXac/MucHuuIch)
        C->>DB: insert AI_FEEDBACK
        C-->>V: ghi nhận feedback
    end

    opt quản lý can thiệp HITL
        U->>V: Gửi can thiệp
        V->>C: POST /ai/intervention-log
        alt thiếu MaDuDoan và MaDanhGia
            C-->>V: 400
        else hợp lệ
            C->>DB: insert AI_NHATKY_CAN_THIEP
            par ghi log
                C-->>V: can thiệp thành công
            and tùy chọn notify
                C->>N: notify parties liên quan
            end
        end
    end
```

## 9) Tạo báo cáo + xuất PDF/Excel
```mermaid
sequenceDiagram
    title 9) Tạo báo cáo + xuất PDF/Excel
    actor U as Người dùng
    participant V as Boundary: Giao diện báo cáo
    participant C as Control: ReportController
    participant DB as Entity: SQL Server
    participant E as Service: ExportService

    alt lưu nháp
        U->>V: Lưu nháp
        V->>C: POST /report/save-draft
    else nộp báo cáo
        U->>V: Nộp báo cáo
        V->>C: POST /report/submit
    end

    C->>DB: validate tiêu đề/loại/ngày + trùng tên + người nhận
    alt validation fail
        C-->>V: 400/409

    else hợp lệ
        C->>DB: insert BAOCAO_PORTAL
        opt submit có MaYeuCau
            C->>DB: update YEUCAUBAOCAO trạng thái
        end
        opt gửi thông báo người nhận
            C->>DB: tạo thông báo nhận báo cáo
        end
        C-->>V: lưu thành công
    end

    opt xuất file
        alt Excel
            V->>C: POST /report/export-excel
            C->>E: generate excel
            E-->>V: file .xlsx
        else PDF
            V->>C: POST /report/export-pdf
            C->>E: generate pdf
            E-->>V: file .pdf
        end
    end
```

## 10) Trung tâm thông báo + đánh dấu đã đọc
```mermaid
sequenceDiagram
    title 10) Trung tâm thông báo + đánh dấu đã đọc
    actor U as Người dùng
    participant V as Boundary: Giao diện thông báo
    participant C as Control: ThongBaoController
    participant DB as Entity: SQL Server

    U->>V: Mở danh sách thông báo
    V->>C: GET /thongbao?tab=all-or-unread
    C->>DB: query THONGBAO + THONGBAO_NHANVIEN theo scope
    alt không có dữ liệu
        C-->>V: danh sách rỗng
    else có dữ liệu
        C-->>V: danh sách + unreadCount
    end

    alt đánh dấu 1 thông báo
        U->>V: Đánh dấu đã đọc
        V->>C: POST /thongbao/{id}/mark-read
    else đánh dấu tất cả
        U->>V: Đánh dấu tất cả
        V->>C: POST /thongbao/mark-all-read
    end

    C->>DB: Resolve scope (Notifications.Receive)
    alt sai scope/không xác định phạm vi
        C-->>V: 400/403
    else hợp lệ
        C->>DB: ExecuteUpdate DaDoc=true
        C-->>V: số bản ghi đã cập nhật
    end
```




