# Van de cua du an LV2026 va huong giai quyet

Ngay cap nhat: 2026-04-27

## 1. Tong quan hien trang

Du an da co cau truc backend kha ro (ASP.NET Core + EF Core + Identity), domain day du (Nhan su, Cong viec, KPI, AI). Tuy nhien van con cac van de quan trong o 4 nhom:

- Phan quyen va bao mat API chua dong deu giua cac module.
- Tinh nhat quan du lieu va logic nghiep vu (phan cong, KPI, tien do) con mot so lo hong.
- Xu ly loi va quan sat he thong con theo huong "fallback 200" o nhieu noi.
- Chua co bo kiem thu tu dong (unit/integration) de khoa chat thay doi.

---

## 2. Van de theo tung chuc nang

## 2.1 Dang nhap, tai khoan, phan quyen

### Van de hien tai

1. Module KPI khong gan role restriction cho cac endpoint ghi du lieu (tao/sua/xoa KPI catalog, tinh KPI).
- Bang chung: `Controllers/Api/KpiController.cs` (khong co `[Authorize]` hoac role policy o class/method).
- Tac dong: moi tai khoan da dang nhap deu co the can thiep KPI catalog neu route duoc frontend goi.

2. Module Phong ban va Ky nang chi yeu cau dang nhap, chua rang buoc role cho thao tac tao/sua/xoa.
- Bang chung: `Controllers/Api/PhongBanController.cs`, `Controllers/Api/KyNangController.cs`.
- Tac dong: Employee co the tao/sua/xoa du lieu master neu biet endpoint.

3. Module Nhom khong co khai bao authorize truc tiep; dang phu thuoc fallback policy toan app.
- Bang chung: `Controllers/Api/NhomController.cs`.
- Tac dong: kho bao tri, de vo tinh mo endpoint neu sau nay doi cau hinh policy.

4. Mapping role o portal dung bo role key legacy (`hr`, `teamlead`) khong trung role thuc te (`Admin/Manager/Employee`).
- Bang chung: `Controllers/PortalController.cs`.
- Tac dong: de gay roi logic giao dien, kho mo rong khi doi role matrix.

### Huong giai quyet

- Ap dung ma tran quyen thong nhat theo module (CRUD + report + admin action).
- Gan role/policy ro rang cho moi endpoint ghi du lieu:
  - KPI catalog/calculate: `Admin, Manager` (hoac policy rieng).
  - PhongBan/KyNang/Nhom create-update-delete: it nhat `Admin, Manager`.
- Giu fallback policy nhung khong phu thuoc fallback cho endpoint quan trong.
- Chuan hoa role key giao dien theo role thuc te hoac chuyen sang permission-driven menu.

---

## 2.2 Quan ly nhan vien

### Van de hien tai

1. Kiem tra validate da kha day du (CCCD, SDT, email, uniqueness), nhung chua thay test de bao dam khong hoi quy.
- Bang chung: `Controllers/Api/NhanVienController.cs` co `ValidateNhanVienRequest` day du regex va check trung.
- Tac dong: moi thay doi sau nay de vo tinh pha validation.

2. Dong bo sang Identity (email/phone/lock status) dang thuc hien trong luong nghiep vu, neu loi co the fail ca giao dich cap nhat nhan vien.
- Bang chung: `SyncIdentityContactInfo`, `SyncIdentityLockStatus` duoc goi ngay trong transaction flow.
- Tac dong: tang do nhay cam khi he thong auth gap su co.

### Huong giai quyet

- Bo sung test cho cac rule validate nhan vien (email/cccd/sdt/aspnetuserid uniqueness).
- Tach lop dong bo Identity theo huong:
  - Cach 1: van transaction chung nhung bat va log loi co kiem soat.
  - Cach 2: outbox/event de retry dong bo sau commit.

---

## 2.3 Quan ly phong ban, nhom, ky nang

### Van de hien tai

1. Chua co role restriction ro cho CRUD (nhu muc 2.1).
2. Thieu audit log o mot so module master data (PhongBan, KyNang, Nhom).
- Bang chung: controller khong goi `IAuditLogService`.
- Tac dong: kho truy vet ai da sua du lieu cau hinh.

3. NhomController chua validate sau nghiep vu sau khi tao/cap nhat thanh vien:
- Chua rang buoc scope manager.
- Chua co transaction bao quanh thao tac tao nhom + them truong nhom.
- Bang chung: `Controllers/Api/NhomController.cs`.

### Huong giai quyet

- Them policy role/scoped check cho Nhom/PhongBan/KyNang.
- Them audit log cho create/update/delete.
- Gom thao tac tao nhom + tao truong nhom vao transaction.
- Bo sung rule truong nhom phai thuoc dung phong ban/pham vi quan ly (neu nghiep vu yeu cau).

---

## 2.4 Quan ly du an

### Van de hien tai

1. Tinh tien do du an hien tai tinh trong app memory sau khi lay nhieu tap du lieu.
- Bang chung: `Controllers/Api/DuAnController.cs` (`taskRows`, `progressRows`, `ToListAsync` roi tinh average).
- Tac dong: ton RAM/CPU khi du lieu lon.

2. Quan ly link `DuAnPhongBan` da co bo sung tu dong, nhung thieu bo quy tac explicit ve bo scope khi project thuoc nhieu phong ban.
- Bang chung: `EnsureProjectPhongBanLinkAsync`.

### Huong giai quyet

- Chuyen mot phan tong hop sang query projection SQL (group by tai DB).
- Dinh nghia ro rule scope khi project co nhieu phong ban (manager nao duoc sua gi).
- Bo sung test permission cho truong hop project da-phong-ban.

---

## 2.5 Quan ly cong viec, phan cong, tien do

### Van de hien tai

1. Co endpoint cap nhat qua han thu cong (`/admin/tasks/update-overdue`) thay vi co co che job dinh ky.
- Bang chung: `Controllers/Api/CongViecController.cs`.
- Tac dong: du lieu qua han phu thuoc thao tac thu cong cua admin.

2. Validation deadline dang dua tren `DateTime.Today`.
- Bang chung: `ValidateCongViecRequest` trong `CongViecController`.
- Tac dong: co the phat sinh edge case theo timezone/khung gio khi can quan ly den muc gio-phut.

3. Xoa phan cong nhan vien chua chan truong hop da co tien do lich su lien quan den assignment context.
- Bang chung: `Controllers/Api/PhanCongNhanVienController.cs` (DeleteAssignment xoa truc tiep assignment).
- Tac dong: mat ngu canh ai dang phu trach task khi da co cap nhat tien do.

4. Endpoint cap nhat tien do cho phep role Admin/Manager/Employee, nhung logic lai ep `request.MaNhanVien` phai trung actor (hoac 0).
- Bang chung: `Controllers/Api/TienDoController.cs`.
- Tac dong: role cao hon khong thuc su cap nhat thay duoc, de gay nham ve hanh vi API.

### Huong giai quyet

- Chuyen cap nhat qua han sang background job (HostedService/Hangfire/Quartz).
- Chot quy tac thoi gian: neu can deadline theo ngay thi giu date-only; neu can theo gio, doi sang datetime full va timezone ro rang.
- Khi xoa assignment:
  - Chan xoa neu da co tien do/nhat ky.
  - Hoac soft-delete assignment + luu ly do.
- Tach endpoint cap nhat tien do:
  - Employee update cho chinh minh.
  - Manager/Admin update thay (co audit log ro nguoi thao tac va nguoi bi tac dong).

---

## 2.6 KPI va danh gia hieu suat

### Van de hien tai

1. Formula KPI hien tai de tao ket qua khong on dinh theo thoi gian tinh lai.
- Bang chung: `Services/KpiService.cs` dung `DateTime.Today` de xac dinh tre han trong qua trinh tinh KPI ky thang/nam.
- Tac dong: cung 1 ky danh gia co the cho diem khac nhau neu tinh lai vao ngay khac.

2. Co nguy co race condition khi tinh KPI dong thoi.
- Bang chung: `KpiService` doc existing, xoa duplicate, roi insert/update trong cung flow; unique index co the bi cham khi 2 request song song.
- Tac dong: loi DB hoac ket qua khong xac dinh khi tai cao.

3. Module KPI chua giong nhau ve text xep loai (`Tot/Trung binh/Kem` va bien the co dau o noi khac).
- Bang chung: `KpiController`, `DashboardController`.
- Tac dong: khong nhat quan hien thi UI/report.

### Huong giai quyet

- Dong bang quy tac tinh KPI theo ky:
  - Tinh theo snapshot du lieu cua ky (khong phu thuoc ngay hien tai).
  - Hoac luu cot trang thai tre-han tai thoi diem chot ky.
- Tang an toan tranh tranh chap:
  - Serializable transaction hoac lock theo (MaKpi, thang, nam).
  - Catch `DbUpdateException` va retry co kiem soat.
- Chuan hoa enum xep loai KPI dung chung toan he thong.

---

## 2.7 AI du doan va goi y

### Van de hien tai

1. Build canh bao compiler CS8629 (nullable value type may be null).
- Bang chung: `Services/AiFeatureBuilderService.cs` (dotnet build canh bao tai line 159).
- Tac dong: nguy co null runtime o mot so path du lieu edge-case.

2. Xu ly loi model tra truc tiep message exception ve client.
- Bang chung: `Services/AiPredictionService.cs` tra `Fail($"Model ... loi: {ex.Message}")`.
- Tac dong: lo thong tin noi bo, kho chuan hoa thong diep loi API.

3. Label map performance con hard-code don gian, default chung.
- Bang chung: `LabelToScore` trong `AiPredictionService`.
- Tac dong: kho mo rong khi doi taxonomy danh gia.

### Huong giai quyet

- Sua canh bao nullable, bo sung guard clause va test cho data null.
- Tach thong diep loi noi bo va thong diep tra ra ngoai:
  - Client nhan ma loi nghiep vu/on dinh.
  - Server log chi tiet stack trace.
- Dua mapping label-score vao cau hinh/bang du lieu de de quan tri.

---

## 2.8 Dashboard, thong bao, nhat ky

### Van de hien tai

1. Nhieu endpoint bat exception roi tra `200 OK` voi message fallback.
- Bang chung:
  - `Controllers/Api/SystemController.cs`
  - `Controllers/Api/NhatKyHoatDongController.cs`
  - `Controllers/Api/ThongBaoController.cs`
- Tac dong: frontend kho nhan biet that bai that su, giam kha nang giam sat loi.

2. Dashboard co xu huong query nang (nhieu ToListAsync + xu ly memory).
- Bang chung: `Controllers/Api/DashboardController.cs`.
- Tac dong: hieu nang giam khi du lieu lon.

3. Nhat ky hoat dong cho Manager xem log rong, chua thay scope theo phong ban.
- Bang chung: `Controllers/Api/NhatKyHoatDongController.cs`.
- Tac dong: co the vuot pham vi quan ly mong muon.

### Huong giai quyet

- Chuan hoa loi API:
  - Loi he thong: tra 5xx.
  - Loi nghiep vu: tra 4xx.
  - Khong tra 200 cho fallback khi that bai xu ly.
- Toi uu dashboard bang query tong hop o DB + cache theo key role/scope.
- Ap dung scope log cho Manager theo phong ban hoac phan quyen chi tiet.

---

## 2.9 Audit log va kha nang quan sat

### Van de hien tai

1. Audit log dang ghi truc tiep trong luong xu ly chinh.
- Bang chung: nhieu controller goi `LogByUserIdAsync` ngay truoc commit.
- Tac dong: neu log gap loi DB co the anh huong nghiep vu chinh.

2. Chua thay bo health check, metrics, tracing, va bo test tu dong.
- Bang chung: khong co test project (ngoai smoke runner), khong thay endpoint health/metrics.

### Huong giai quyet

- Tach audit log theo outbox/queue hoac least-impact pattern.
- Them:
  - Health checks (`/health`).
  - Structured logging + correlation id.
  - Integration tests cho endpoint quan trong (auth, KPI, task progress).
  - Unit tests cho service tinh KPI va AI feature builder.

---

## 3. Lo trinh de xuat (uu tien)

## Uu tien 1 (can lam ngay)

- Khoa quyen ghi du lieu cho KPI/PhongBan/KyNang/Nhom.
- Chuan hoa status code (bo fallback 200 cho loi he thong).
- Chan race condition khi tinh KPI.

## Uu tien 2 (ngan han)

- Chuyen overdue update sang background job.
- Hoan thien quy tac xoa assignment khi da co tien do.
- Sua canh bao nullable trong AI feature builder.

## Uu tien 3 (trung han)

- Toi uu query dashboard/du an.
- Chuan hoa enum/nhan xep loai KPI.
- Bo sung test tu dong + quan sat he thong.

---

## 4. Diem manh dang co

- Da co Identity + cookie security co cau hinh tuong doi day du.
- Da co policy cho mot so module nhay cam (CongViec, AccountManagement, RoleClaims).
- Du lieu nghiep vu phong phu, mo hinh domain ro rang.
- Da co unique index cho ket qua KPI va du doan AI.
- Validation module NhanVien kha tot (email/cccd/sdt/uniqueness).

---

## 5. Ket luan

Du an da dat nen tang tot de phat trien thanh he thong quan ly hieu suat hoan chinh. Muc uu tien hien tai la khoa chat phan quyen endpoint ghi du lieu, on dinh logic KPI theo ky, va chuan hoa xu ly loi de he thong van hanh an toan, de theo doi va de mo rong.