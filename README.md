# HTQL Hiệu Suất Doanh Nghiệp (LV2026)

Hệ thống quản lý hiệu suất doanh nghiệp phục vụ luận văn (2026), gồm:
- Web app ASP.NET Core MVC + Identity (RBAC/claims) để quản lý dự án, công việc, KPI, tiến độ…
- Python AI service (FastAPI) cung cấp các API dự đoán (ví dụ: dự đoán trễ deadline, phân loại hiệu suất nhân viên).

## Công nghệ
- Backend/Web: ASP.NET Core 8 (MVC), Entity Framework Core, SQL Server
- Auth: ASP.NET Identity + Cookie Auth + Policy/Claims
- AI service: FastAPI + scikit-learn (LinearRegression, RandomForest)

## Cấu trúc thư mục
- `LuanVan/` : Solution + source chính (ASP.NET Core)
  - `LuanVan/LuanVan/` : project web (controllers, views, services, migrations…)
- `python-ai-service/` : dịch vụ AI (FastAPI)
- `docs/` và các file `*.md` : tài liệu phân tích/thiết kế/usecase/sequence…
- `*.sql` : script DB/seed dữ liệu

## Yêu cầu môi trường
- .NET SDK 8.x
- SQL Server (khuyến nghị SQL Server Express)
- Python 3.10+ (khuyến nghị 3.11)

## Thiết lập & chạy nhanh

### 1) Database (SQL Server)
- Ứng dụng mặc định dùng database tên **LV2026**.
- Cấu hình connection string qua một trong các cách sau:
  - File cấu hình (không nên commit secret): `ConnectionStrings:DefaultConnection`
  - Biến môi trường: `ConnectionStrings__DefaultConnection`

Một số script tham khảo nằm ở các file `SQL_*.sql`, `Seed_*.sql`, hoặc trong `LuanVan/LuanVan/DataSQL.sql`.

### 2) Chạy Web app (ASP.NET Core)
Từ thư mục root:

```bash
dotnet restore
dotnet run --project LuanVan/LuanVan/LuanVan.csproj
```

Mặc định route chính: `/Portal/Dashboard`.

### 3) Chạy Python AI service

```bash
cd python-ai-service
python -m pip install -r requirements.txt
set AI_SERVICE_KEY=dev-ai-service-key
uvicorn main:app --reload --host 0.0.0.0 --port 8000
```

Web app gọi sang AI service theo cấu hình:
- `AiPython:BaseUrl` (mặc định `http://localhost:8000`)
- Header: `X-AI-Service-Key` phải trùng với `AI_SERVICE_KEY`

## Lưu ý bảo mật
- Không commit mật khẩu/secret (SMTP, token, API key production…). Repo này đã cấu hình `.gitignore` để bỏ qua `appsettings.Development.json`.

## Tài liệu
Một số tài liệu nghiệp vụ/kiểm thử nằm trong các file `*.md` ở root, ví dụ: `WORKFLOW_ANALYSIS_REPORT.md`.
