# Hệ thống quản lý hiệu suất doanh nghiệp tích hợp AI

Đây là dự án luận văn xây dựng hệ thống web hỗ trợ doanh nghiệp quản lý nhân sự, dự án, công việc, KPI, báo cáo và phân tích hiệu suất bằng AI.

Mục tiêu của hệ thống là giúp người quản lý theo dõi tình hình thực hiện công việc, đánh giá hiệu suất nhân viên, tổng hợp báo cáo và nhận diện sớm các rủi ro như công việc có nguy cơ trễ hạn.

## Tổng quan chức năng

- Quản lý đăng nhập, vai trò và phân quyền người dùng.
- Quản lý nhân viên, phòng ban và nhóm làm việc.
- Quản lý dự án, công việc, tiến độ và deadline.
- Duyệt tiến độ công việc.
- Thiết lập, theo dõi và đánh giá KPI.
- Tạo, quản lý và xuất báo cáo.
- Dashboard tổng quan tình hình hoạt động.
- Tích hợp AI để dự đoán nguy cơ trễ hạn và phân loại hiệu suất nhân viên.

## Công nghệ chính

- ASP.NET Core 8 MVC.
- Entity Framework Core.
- SQL Server.
- ASP.NET Identity, role/claim-based authorization.
- Razor View, HTML, CSS, JavaScript.
- Python FastAPI.
- scikit-learn.

## Cấu trúc chính

```text
Luanvan2026
├─ LuanVan/                 Source web ASP.NET Core
├─ python-ai-service/       Dịch vụ AI FastAPI
├─ docs/                    Tài liệu kỹ thuật và thực nghiệm
├─ CSDL_SQL_LV.sql        Script cơ sở dữ liệu
├─ HUONG_DAN_CAI_DAT_DU_AN.md
└─ README.md
```

## Cách bắt đầu

Đọc file hướng dẫn chi tiết:

```text
HUONG_DAN_CAI_DAT_DU_AN.md
```

Tóm tắt các bước chính:

1. Cài .NET SDK 8, SQL Server và Python 3.10 trở lên.
2. Tạo database `LV2026` và chạy script `CSDL_SQL_30_5.sql`.
3. Chạy web app ASP.NET Core trong thư mục `LuanVan`.
4. Chạy AI service trong thư mục `python-ai-service`.
5. Mở hệ thống tại `http://localhost:5231`.

## Tài liệu liên quan

- `HUONG_DAN_CAI_DAT_DU_AN.md`: hướng dẫn cài đặt và sử dụng dự án.
- `docs/ai-python-contract-v1.md`: mô tả API giữa web app và AI service.
- `CSDL_SQL_30_5.sql`: script cơ sở dữ liệu chính.

## Ghi chú

Khi nộp source, không cần nộp các thư mục build/cache như `bin/`, `obj/`, `.vs/`, `.venv/`, `artifacts/`, `logs/` và `wwwroot/uploads/`.
