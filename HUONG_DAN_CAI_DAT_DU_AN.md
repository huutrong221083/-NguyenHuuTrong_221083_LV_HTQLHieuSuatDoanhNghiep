# HƯỚNG DẪN CÀI ĐẶT VÀ SỬ DỤNG DỰ ÁN

## 1. Giới thiệu

Dự án là hệ thống web quản lý hiệu suất hoạt động doanh nghiệp tích hợp AI. Hệ thống hỗ trợ quản lý nhân sự, phòng ban, nhóm làm việc, dự án, công việc, KPI, báo cáo và các chức năng phân tích AI phục vụ đánh giá hiệu suất.

Các chức năng chính:

- Đăng nhập, phân quyền người dùng theo vai trò và quyền truy cập.
- Quản lý nhân viên, phòng ban, nhóm làm việc.
- Quản lý dự án, công việc, tiến độ và duyệt tiến độ.
- Thiết lập, theo dõi và đánh giá KPI.
- Tạo báo cáo và xuất báo cáo.
- Dashboard tổng quan tình hình công việc, dự án, KPI.
- Tích hợp Python FastAPI để dự đoán nguy cơ trễ hạn và phân loại hiệu suất nhân viên.

## 2. Yêu cầu môi trường

Máy chạy thử cần cài đặt các phần mềm sau:

- Windows 10/11.
- .NET SDK 8.x.
- SQL Server hoặc SQL Server Express.
- SQL Server Management Studio (SSMS) để tạo và quản lý cơ sở dữ liệu.
- Python 3.10 trở lên.
- Visual Studio 2022 hoặc VS Code.
- Trình duyệt web: Chrome, Edge hoặc Firefox.

Phiên bản khuyến nghị:

- .NET SDK: 8.x.
- Python: 3.10 hoặc 3.11.
- SQL Server Express: bản mới ổn định.

## 3. Cấu trúc thư mục dự án

Thư mục gốc dự án:

```text
Luanvan2026
├─ LuanVan
│  ├─ LuanVan.sln
│  └─ LuanVan
│     ├─ Controllers
│     ├─ Contracts
│     ├─ Data
│     ├─ Migrations
│     ├─ Models
│     ├─ Services
│     ├─ Views
│     ├─ wwwroot
│     ├─ Program.cs
│     ├─ appsettings.json
│     └─ LuanVan.csproj
├─ python-ai-service
│  ├─ main.py
│  └─ requirements.txt
├─ docs
├─ CSDL_SQL_LV.sql
└─ README.md
```

Trong đó:

- `LuanVan/LuanVan`: mã nguồn chính của web app ASP.NET Core MVC.
- `python-ai-service`: dịch vụ AI viết bằng FastAPI.
- `docs`: tài liệu kỹ thuật, hợp đồng API và tài liệu thực nghiệm.
- `CSDL_SQL_30_5.sql`: script cơ sở dữ liệu chính dùng để tạo hoặc khôi phục dữ liệu demo.

## 4. Cài đặt cơ sở dữ liệu

### 4.1. Tạo database bằng SQL Server Management Studio

1. Mở SQL Server Management Studio.
2. Kết nối đến SQL Server local hoặc SQL Server Express.
3. Tạo database tên:

```text
LV2026
```

4. Mở file script:

```text
CSDL_SQL_30_5.sql
```

5. Chạy toàn bộ script để tạo bảng và dữ liệu cần thiết cho hệ thống.

Nếu trong script đã có lệnh tạo database, có thể chạy trực tiếp script theo đúng thứ tự trong SSMS.

### 4.2. Kiểm tra chuỗi kết nối

Mở file:

```text
LuanVan/LuanVan/appsettings.json
```

Kiểm tra cấu hình:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=TRONG\\SQLEXPRESS01;Database=LV2026;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

Nếu SQL Server trên máy khác tên instance, sửa lại phần `Server`.

Ví dụ dùng SQL Server Express mặc định:

```text
Server=localhost\SQLEXPRESS;Database=LV2026;Trusted_Connection=True;TrustServerCertificate=True
```

Ví dụ dùng SQL Server local không có instance:

```text
Server=localhost;Database=LV2026;Trusted_Connection=True;TrustServerCertificate=True
```

Lưu ý: không nên đưa mật khẩu hoặc thông tin bí mật vào file nộp. Có thể cấu hình chuỗi kết nối bằng biến môi trường:

```powershell
$env:ConnectionStrings__DefaultConnection="Server=localhost\SQLEXPRESS;Database=LV2026;Trusted_Connection=True;TrustServerCertificate=True"
```

## 5. Cài đặt và chạy dịch vụ AI Python

Dịch vụ AI dùng FastAPI và scikit-learn để cung cấp API dự đoán.

Mở PowerShell tại thư mục gốc dự án, sau đó chạy:

```powershell
cd python-ai-service
python -m venv .venv
.\.venv\Scripts\activate
python -m pip install --upgrade pip
python -m pip install -r requirements.txt
```

Thiết lập khóa nội bộ cho AI service:

```powershell
$env:AI_SERVICE_KEY="dev-ai-service-key"
```

Chạy dịch vụ AI:

```powershell
uvicorn main:app --reload --host 0.0.0.0 --port 8000
```

Sau khi chạy thành công, AI service hoạt động tại:

```text
http://localhost:8000
```

Web app gọi sang AI service thông qua cấu hình trong `appsettings.json`:

```json
{
  "AiPython": {
    "BaseUrl": "http://localhost:8000",
    "TimeoutSeconds": 10,
    "EnableFallback": true,
    "ApiKey": "dev-ai-service-key"
  }
}
```

Giá trị `AiPython:ApiKey` trong web app phải trùng với biến môi trường `AI_SERVICE_KEY` của Python service.

## 6. Cài đặt và chạy ứng dụng web

Mở PowerShell tại thư mục gốc dự án:

```powershell
cd LuanVan
```

Khôi phục package:

```powershell
dotnet restore
```

Build dự án:

```powershell
dotnet build
```

Chạy ứng dụng bằng profile HTTP:

```powershell
dotnet run --project LuanVan/LuanVan.csproj --launch-profile http
```

Sau khi chạy thành công, mở trình duyệt:

```text
http://localhost:5231
```

Nếu dùng profile HTTPS:

```powershell
dotnet run --project LuanVan/LuanVan.csproj --launch-profile https
```

Địa chỉ HTTPS:

```text
https://localhost:7271
```

## 7. Hướng dẫn sử dụng hệ thống

### 7.1. Đăng nhập hệ thống

1. Mở trình duyệt và truy cập:

```text
http://localhost:5231
```

2. Đăng nhập bằng tài khoản đã có trong cơ sở dữ liệu demo.
3. Sau khi đăng nhập, hệ thống chuyển vào khu vực Portal.

Trang chính của hệ thống:

```text
/Portal/Dashboard
```

### 7.2. Quản lý nhân sự và tổ chức

Người dùng có quyền phù hợp có thể thực hiện:

- Quản lý danh sách nhân viên.
- Cập nhật thông tin hồ sơ nhân viên.
- Quản lý phòng ban.
- Quản lý nhóm làm việc.
- Phân quyền và quản lý tài khoản.

### 7.3. Quản lý dự án và công việc

Các chức năng chính:

- Tạo và cập nhật dự án.
- Tạo công việc, giao việc cho nhân viên.
- Theo dõi trạng thái, tiến độ, độ ưu tiên và hạn hoàn thành.
- Cập nhật tiến độ công việc.
- Duyệt tiến độ nếu người dùng có quyền quản lý.

### 7.4. Quản lý và đánh giá KPI

Hệ thống hỗ trợ:

- Thiết lập KPI.
- Gán KPI cho nhân viên hoặc bộ phận.
- Theo dõi KPI cá nhân.
- Đánh giá hiệu suất.
- Tổng hợp kết quả phục vụ báo cáo.

### 7.5. Báo cáo và dashboard

Người dùng có quyền có thể:

- Xem dashboard tổng quan.
- Tạo báo cáo.
- Quản lý báo cáo.
- Xuất dữ liệu phục vụ theo dõi và đánh giá.

### 7.6. Chức năng AI

Khi Python AI service đang chạy, web app có thể gọi các API AI để:

- Dự đoán số ngày có nguy cơ trễ hạn của công việc.
- Phân loại hiệu suất nhân viên.
- Hiển thị cảnh báo hoặc gợi ý trong các màn hình phân tích AI.

Nếu Python AI service không chạy, hệ thống vẫn có thể dùng cơ chế fallback nếu `AiPython:EnableFallback` đang bật.

## 8. Kiểm thử nhanh API AI

### 8.1. Kiểm tra thông tin model

```powershell
curl.exe http://localhost:8000/model/info
```

### 8.2. Dự đoán trễ hạn công việc

Endpoint:

```text
POST http://localhost:8000/predict/task-delay
```

Header:

```text
X-AI-Service-Key: dev-ai-service-key
```

Ví dụ body:

```json
{
  "correlation_id": "ai-20260522-0001",
  "training_rows": [
    {
      "estimated_hours": 40,
      "spent_hours": 45,
      "progress_percent": 70,
      "priority_score": 3,
      "difficulty_score": 4,
      "days_until_deadline": 2,
      "late_days": 1
    },
    {
      "estimated_hours": 20,
      "spent_hours": 18,
      "progress_percent": 90,
      "priority_score": 2,
      "difficulty_score": 2,
      "days_until_deadline": 5,
      "late_days": 0
    }
  ],
  "input_features": {
    "estimated_hours": 32,
    "spent_hours": 20,
    "progress_percent": 60,
    "priority_score": 3,
    "difficulty_score": 4,
    "days_until_deadline": 1
  }
}
```

### 8.3. Phân loại hiệu suất nhân viên

Endpoint:

```text
POST http://localhost:8000/predict/performance
```

Header:

```text
X-AI-Service-Key: dev-ai-service-key
```

Kết quả trả về gồm nhãn hiệu suất như `LOW`, `NORMAL`, `GOOD`, `EXCELLENT` và độ tin cậy dự đoán.

## 9. Các lỗi thường gặp và cách xử lý

### 9.1. Không kết nối được database

Nguyên nhân thường gặp:

- Chưa tạo database `LV2026`.
- SQL Server chưa chạy.
- Sai tên server hoặc instance.
- Chuỗi kết nối trong `appsettings.json` chưa đúng.

Cách xử lý:

- Mở SSMS kiểm tra SQL Server và database.
- Chạy lại script `CSDL_SQL_30_5.sql`.
- Sửa `ConnectionStrings:DefaultConnection`.

### 9.2. Không chạy được web app

Nguyên nhân thường gặp:

- Chưa cài .NET SDK 8.
- Chưa restore package.
- Port `5231` hoặc `7271` đang bị ứng dụng khác sử dụng.
- Database chưa sẵn sàng.

Cách xử lý:

```powershell
dotnet --version
dotnet restore
dotnet build
dotnet run --project LuanVan/LuanVan.csproj --launch-profile http
```

Nếu port bị trùng, có thể sửa trong:

```text
LuanVan/LuanVan/Properties/launchSettings.json
```

### 9.3. Không chạy được Python AI service

Nguyên nhân thường gặp:

- Chưa cài Python.
- Chưa cài thư viện trong `requirements.txt`.
- Chưa thiết lập `AI_SERVICE_KEY`.
- Port `8000` đang bị sử dụng.

Cách xử lý:

```powershell
python --version
python -m pip install -r requirements.txt
$env:AI_SERVICE_KEY="dev-ai-service-key"
uvicorn main:app --reload --host 0.0.0.0 --port 8000
```

### 9.4. Web app gọi AI bị lỗi Unauthorized

Nguyên nhân:

- `AiPython:ApiKey` trong `appsettings.json` không trùng với `AI_SERVICE_KEY`.

Cách xử lý:

- Kiểm tra `AiPython:ApiKey`.
- Kiểm tra biến môi trường khi chạy Python service.
- Khởi động lại web app và Python service sau khi sửa cấu hình.

### 9.5. Không gửi được email

Nguyên nhân thường gặp:

- Chưa cấu hình SMTP.
- Thiếu mật khẩu ứng dụng.
- Tài khoản email chặn đăng nhập từ ứng dụng.

Cách xử lý:

- Kiểm tra cấu hình `Smtp` trong `appsettings.json`.
- Không đưa mật khẩu email thật vào file nộp.
- Với Gmail, nên dùng App Password thay vì mật khẩu tài khoản chính.

## 10. Gợi ý chuẩn bị trước khi nộp

Trước khi nộp source code, nên loại bỏ các thư mục build/cache có thể sinh lại:

```text
bin/
obj/
.vs/
.venv/
artifacts/
logs/
wwwroot/uploads/
```

Không nên nộp các file cấu hình local chứa thông tin riêng:

```text
appsettings.Development.json
*.csproj.user
*.lscache
```

Các file nên giữ:

```text
LuanVan/LuanVan.sln
LuanVan/LuanVan/
python-ai-service/
docs/
CSDL_SQL_30_5.sql
README.md
HUONG_DAN_CAI_DAT_DU_AN.md
```

## 11. Kết luận

Sau khi cài đặt đúng .NET SDK, SQL Server, Python và các thư viện cần thiết, hệ thống có thể chạy tại `http://localhost:5231`. Người dùng đăng nhập vào Portal để quản lý nhân sự, dự án, công việc, KPI, báo cáo và sử dụng các chức năng AI. Python AI service chạy riêng tại `http://localhost:8000` và được web app gọi thông qua khóa nội bộ `X-AI-Service-Key`.
