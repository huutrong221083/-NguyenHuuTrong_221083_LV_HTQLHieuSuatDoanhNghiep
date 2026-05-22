%%{init: { "theme": "neutral", "flowchart": { "curve": "linear" } }}%%
classDiagram
direction LR
class AppDbContext <<infrastructure>> {
  +ChucVus : DbSet<ChucVu>
  +NhanViens : DbSet<NhanVien>
  +PhongBans : DbSet<PhongBan>
  +Nhoms : DbSet<Nhom>
  +DuAns : DbSet<DuAn>
  +CongViecs : DbSet<CongViec>
  +KetQuaKpis : DbSet<KetQuaKpi>
  +DuDoanAis : DbSet<DuDoanAi>
  +BaoCaos : DbSet<BaoCao>
  +YeuCauBaoCaos : DbSet<YeuCauBaoCao>
  #OnModelCreating(modelBuilder: ModelBuilder) : void
  -ConfigureIdentity(modelBuilder: ModelBuilder) : void
}

class ApplicationUser <<class>> {
  -NhanVien : NhanVien?
}

class AiBusinessKpiRun <<class>> {
  -MaBusinessKpi : int
  -MaModel : int
  -MaPhienBan : int?
  -LoaiMoHinh : string
  -TuNgay : DateTime?
  -DenNgay : DateTime?
  -NgayTao : DateTime
  -TongDuDoan : int
  -TongTacDong : int
  -InterventionRate : decimal?
  -UserAcceptanceRate : decimal?
  -UtilityScore : decimal?
  -GhiChu : string?
}

class AiDanhGiaChiTiet <<class>> {
  -MaDanhGiaChiTiet : int
  -MaDanhGia : int
  -MaDuDoan : int?
  -MaNhanVien : int?
  -MaCongViec : int?
  -MaDuAn : int?
  -GiatriDuDoanSo : decimal?
  -GiatriThucTeSo : decimal?
  -NhanDuDoan : string?
  -NhanThucTe : string?
  -SoSaiLech : decimal?
  -DungNhan : bool?
  -DungSo : bool?
  -DoTinCay : decimal?
  -GhiChu : string?
}

class AiDanhGiaRun <<class>> {
  -MaDanhGia : int
  -MaModel : int
  -MaPhienBan : int?
  -LoaiMoHinh : string
  -TuNgay : DateTime?
  -DenNgay : DateTime?
  -NgayDanhGia : DateTime?
  -TongBanGhi : int
  -TongDung : int
  -TongSai : int
  -Mae : decimal?
  -Rmse : decimal?
  -Accuracy : decimal?
  -PrecisionScore : decimal?
  -RecallScore : decimal?
  -F1Score : decimal?
  -GhiChu : string?
  -MoHinhAi : MoHinhAi
}

class AiEvaluationHostedService <<service>> {
  +<no-public-members-captured>() : void
}

class AiEvaluationService <<service>> {
  +RunEvaluationAsync(int maModel, string loaiMoHinh, DateTime? tuNgay, DateTime? denNgay, string? positiveLabel = null, CancellationToken cancellationToken = default) : Task<int>
}

class AiFeatureStore <<class>> {
  -MaFeature : int
  -MaModel : int?
  -MaNhanVien : int?
  -MaCongViec : int?
  -MaDuAn : int?
  -FeatureName : string
  -FeatureValue : string?
  -FeatureType : string?
  -SourceTable : string?
  -SourceKey : string?
  -VersionTag : string?
  -DongChot : DateTime?
}

class AiFeedback <<class>> {
  -MaFeedback : int
  -MaDanhGia : int?
  -MaDuDoan : int?
  -MaNhanVien : int?
  -DoChinhXac : int?
  -MucHuuIch : int?
  -DungSai : bool?
  -NoiDung : string?
  -HanhDongDeXuat : string?
  -NgayPhanHoi : DateTime?
  +SubmitFeedback(request: AiFeedbackRequestDto) : Task<ActionResult<ApiResponse<AiFeedbackDto>>>
  +GetForecastFeedback(taskIds: string?) : Task<ActionResult<ApiResponse<List<AiForecastFeedbackItemDto>>>>
}

class AiNhatKyCanThiep <<class>> {
  -MaCanThiep : int
  -MaDanhGia : int?
  -MaDuDoan : int?
  -MaNhanVien : int?
  -NguoiCanThiep : int?
  -ActionType : string?
  -ActionSource : string?
  -Reason : string?
  -OldValue : string?
  -NewValue : string?
  -NguonCanThiep : string?
  -NgayCanThiep : DateTime?
  -SoLanChinhSua : int?
  +GetInterventionLog(...) : Task<ActionResult<ApiResponse<List<AiInterventionLogDto>>>>
  +CreateInterventionLog(...) : Task<ActionResult<ApiResponse<AiInterventionLogDto>>>
}

class AuditLogService <<service>> {
  +LogByNhanVienIdAsync(int? maNhanVien, string action, CancellationToken cancellationToken = default) : Task<bool>
  +LogByUserIdAsync(string? userId, string action, CancellationToken cancellationToken = default) : Task<bool>
}

class BaoCao <<class>> {
  -MaBaoCao : int
  -TenBaoCao : string?
  -LoaiBaoCao : string?
  -MaDuAn : int?
  -MaPhongBan : int?
  -NguoiTao : string?
  -NgayTao : DateTime?
  -NgayCapNhat : DateTime?
  -NgayBatDau : DateTime?
  -NgayKetThuc : DateTime?
  -DinhDang : string?
  -TrangThai : string?
  -NoiDung : string?
  -IsDeleted : bool
  -NguoiTaoNavigation : ApplicationUser?
  -DuAn : DuAn?
  -PhongBan : PhongBan?
  -BaoCaoChiTiets : ICollection<BaoCaoChiTiet>
  +LoadPage() : Task<ActionResult<ApiResponse<ReportPageLoadDto>>>
  +SaveDraft(request: SaveReportDraftRequest) : Task<ActionResult<ApiResponse<object>>>
  +Submit(request: SubmitReportRequest) : Task<ActionResult<ApiResponse<object>>>
  +List(...) : Task<IActionResult>
  +Detail(id: int) : Task<ActionResult<ApiResponse<BaoCaoDto>>>
  +Delete(id: int) : Task<IActionResult>
}

class BaoCaoChiTiet <<class>> {
  -MaBaoCaoChiTiet : int
  -MaBaoCao : int
  -TieuDe : string?
  -DuLieu : string?
  -ThuTu : int?
  -BaoCao : BaoCao
}

class ChangePasswordViewModel <<class>> {
  -CurrentPassword : string
  -NewPassword : string
  -ConfirmPassword : string
}

class ChucVu <<class>> {
  -MaChucVu : int
  -TenChucVu : string?
  -NhanViens : ICollection<NhanVien>
}

class CongViec <<class>> {
  -MaCongViec : int
  -MaDuAn : int
  -MaCongViecCha : int?
  -TenCongViec : string?
  -MoTa : string?
  -NgayBatDau : DateTime?
  -HanHoanThanh : DateTime?
  -MaTrangThai : int?
  -MaDoUuTien : int?
  -MaDoKho : int?
  -DiemCongViec : decimal?
  -PhanTramHoanThanh : decimal?
  -NgayTao : DateTime?
  -NguoiTao : string?
  -NgayCapNhat : DateTime?
  -NguoiCapNhat : string?
  -DaXoa : bool?
  -DuAn : virtual DuAn
  -DoUuTien : virtual DoUuTien
  -DoKho : virtual DoKho
  -CongViecCha : virtual CongViec
  -CongViecCon : virtual ICollection<CongViec>
  -PhanCongNhanViens : virtual ICollection<PhanCongNhanVien>
  -PhanCongNhoms : virtual ICollection<PhanCongNhom>
  -PhanCongPhongBans : virtual ICollection<PhanCongPhongBan>
  -NhatKyCongViecs : virtual ICollection<NhatKyCongViec>
  -CongViecKyNangs : virtual ICollection<CongViecKyNang>
  -TienDoCongViecs : virtual ICollection<TienDoCongViec>
  +GetTasks(...) : Task<ActionResult<ApiResponse<PagedResult<TaskListItemDto>>>>
  +GetTask(id: int) : Task<ActionResult<ApiResponse<TaskDetailDto>>>
  +CreateTask(request: CreateUpdateTaskRequest) : Task<ActionResult<ApiResponse<object>>>
  +UpdateTask(id: int, request: CreateUpdateTaskRequest) : Task<ActionResult<ApiResponse<object>>>
  +DeleteTask(id: int) : Task<ActionResult<ApiResponse<object>>>
  +UpdateStatus(id: int, request: UpdateTaskStatusRequest) : Task<ActionResult<ApiResponse<object>>>
  +UpdateProgress(request: UpdateProgressRequest) : Task<ActionResult<ApiResponse<object>>>
  +AssignEmployee(request: TaskAssignmentRequest) : Task<ActionResult<ApiResponse<object>>>
  +AssignTeam(id: int, request: TaskAssignmentRequest) : Task<ActionResult<ApiResponse<object>>>
  +AssignDepartment(id: int, request: TaskAssignmentRequest) : Task<ActionResult<ApiResponse<object>>>
}

class CongViecKyNang <<class>> {
  -MaCongViec : int
  -MaKyNang : int
  -CongViec : virtual CongViec
  -KyNang : virtual KyNang
}

class DanhMucKpi <<class>> {
  -MaKpi : int
  -MaLoaiKpi : int
  -TenKpi : string?
  -TrongSoGoc : decimal?
  -TrangThai : int?
  -LoaiKpi : LoaiKpi
  -KetQuaKpis : ICollection<KetQuaKpi>
  -KpiNhanViens : ICollection<KpiNhanVien>
  -KpiNhoms : ICollection<KpiNhom>
  -KpiDuAns : ICollection<KpiDuAn>
  -KpiPhongBans : ICollection<KpiPhongBan>
  +GetCatalog(...) : Task<ActionResult<ApiResponse<KpiCatalogListDto>>>
  +GetCatalogDetail(id: int) : Task<ActionResult<ApiResponse<KpiCatalogDetailDto>>>
  +CreateCatalog(request: SaveKpiCatalogRequest) : Task<ActionResult<ApiResponse<KpiCatalogItemDto>>>
  +UpdateCatalog(id: int, request: SaveKpiCatalogRequest) : Task<ActionResult<ApiResponse<KpiCatalogItemDto>>>
  +DeleteCatalog(id: int) : Task<ActionResult<ApiResponse<object>>>
}

class DeXuatKpi <<class>> {
  -MaDeXuat : int
  -MaKpi : int?
  -MaLoaiKpi : int?
  -NguoiDeXuat : int
  -NguoiDuyet : int?
  -NguoiCapNhat : int?
  -LoaiDeXuat : string
  -MaNhanVienApDung : int?
  -MaNhomApDung : int?
  -MaPhongBanApDung : int?
  -MaDuAnApDung : int?
  -TuNgay : DateTime
  -DenNgay : DateTime?
  -TrongSoDeXuat : decimal
  -TenKpiDeXuat : string?
  -MoTaKpiDeXuat : string?
  -LyDo : string?
  -TrangThai : string
  -PhanHoiAdmin : string?
  -GhiChu : string?
  -NgayTao : DateTime
  -NgayCapNhat : DateTime?
  -NgayDuyet : DateTime?
  -DanhMucKpi : DanhMucKpi?
  -LoaiKpi : LoaiKpi?
  -NhanVienDeXuat : NhanVien
  -NhanVienDuyet : NhanVien?
  -NhanVienCapNhat : NhanVien?
  -NhanVienApDung : NhanVien?
  -NhomApDung : Nhom?
  -PhongBanApDung : PhongBan?
  -DuAnApDung : DuAn?
  +GetProposals(trangThai: string?) : Task<ActionResult<ApiResponse<List<KpiProposalDto>>>>
  +CreateProposal(request: SaveKpiProposalRequest) : Task<ActionResult<ApiResponse<KpiProposalDto>>>
  +UpdateProposal(id: int, request: SaveKpiProposalRequest) : Task<ActionResult<ApiResponse<KpiProposalDto>>>
  +ReviewProposal(id: int, request: ReviewKpiProposalRequest) : Task<ActionResult<ApiResponse<KpiProposalDto>>>
}

class DoKho <<class>> {
  -MaDoKho : int
  -TenDoKho : string?
  -HeSo : decimal
  -IsActive : bool
  -CongViecs : ICollection<CongViec>
}

class DoUuTien <<class>> {
  -MaDoUuTien : int
  -TenDoUuTien : string?
  -HeSo : decimal
  -IsActive : bool
  -CongViecs : ICollection<CongViec>
}

class DuAn <<class>> {
  -MaDuAn : int
  -TenDuAn : string?
  -MoTa : string?
  -NgayBatDau : DateTime?
  -NgayKetThuc : DateTime?
  -TrangThai : int?
  -KpiDuAns : ICollection<KpiDuAn>
  -CongViecs : ICollection<CongViec>
  -DuAnNhanViens : ICollection<DuAnNhanVien>
  -DuAnNhoms : ICollection<DuAnNhom>
  -DuAnPhongBans : ICollection<DuAnPhongBan>
  +GetDuAns(...) : Task<ActionResult<ApiResponse<PagedResult<DuAnListItemDto>>>>
  +GetDuAnById(id: int) : Task<ActionResult<ApiResponse<DuAnDetailDto>>>
  +GetDuAnList() : Task<ActionResult<ApiResponse<List<DuAnListItemDto>>>>
  +CreateDuAn(request: UpsertDuAnRequest) : Task<ActionResult<ApiResponse<DuAnDetailDto>>>
  +UpdateDuAn(id: int, request: UpsertDuAnRequest) : Task<ActionResult<ApiResponse<DuAnDetailDto>>>
  +DeleteDuAn(id: int) : Task<ActionResult<ApiResponse<object>>>
  +AssignEmployeeToProject(id: int, request: AssignEmployeeRequest) : Task<ActionResult<ApiResponse<object>>>
  +AssignTeamToProject(id: int, request: AssignTeamRequest) : Task<ActionResult<ApiResponse<object>>>
  +AssignDepartmentToProject(id: int, request: AssignDepartmentRequest) : Task<ActionResult<ApiResponse<object>>>
}

class DuAnNhanVien <<class>> {
  -MaDuAn : int
  -MaNhanVien : int
  -VaiTro : string?
  -NgayThamGia : DateTime?
  -NgayRoi : DateTime?
  -TrangThai : byte?
  -DuAn : DuAn
  -NhanVien : NhanVien
}

class DuAnNhom <<class>> {
  -MaDuAn : int
  -MaNhom : int
  -NgayThamGia : DateTime?
  -TrangThai : byte?
  -DuAn : DuAn
  -Nhom : Nhom
}

class DuAnPhongBan <<class>> {
  -MaDuAn : int
  -MaPhongBan : int
  -NgayThamGia : DateTime?
  -TrangThai : byte?
  -DuAn : DuAn
  -PhongBan : PhongBan
}

class DuDoanAi <<class>> {
  -MaDuDoan : int
  -MaNhanVien : int
  -MaModel : int
  -thang : int?
  -nam : int?
  -ModelName : string?
  -DiemDuDoan : decimal?
  -XacSuatTreHan : decimal?
  -InputData : string?
  -OutputData : string?
  -Actor : string?
  -DeXuatCaiThien : string?
  -GoiYNguonLuc : string?
  -ThoiGianDuDoan : DateTime?
  -NhanVien : NhanVien
  -MoHinhAi : MoHinhAi
  +PredictDelay(request: PredictDelayRequest) : Task<ActionResult<ApiResponse<PredictDelayResultDto>>>
  +ClassifyPerformance(request: ClassifyPerformanceRequest) : Task<ActionResult<ApiResponse<ClassifyPerformanceResultDto>>>
  +SuggestEmployee(request: SuggestEmployeeRequest) : Task<ActionResult<ApiResponse<List<SuggestEmployeeItem>>>>
  +GetHistory(top: int) : Task<ActionResult<ApiResponse<AiMonitoringSnapshotDto>>>
}

class DuLieuAi <<class>> {
  -MaDuLieu : int
  -MaNhanVien : int
  -SoCongViecHoanThanh : int?
  -SoCongViecTreHan : int?
  -ThoiGianTrungBinh : decimal?
  -KpiTrungBinh : decimal?
  -NhanVien : NhanVien
  -MoHinhDuLieuAis : ICollection<MoHinhDuLieuAi>
}

class EmailService <<service>> {
  +SendPasswordResetEmailAsync(string recipientEmail, string recipientName, string resetLink, CancellationToken cancellationToken = default) : Task
  +SendSystemNotificationEmailAsync(string recipientEmail, string recipientName, string subject, string htmlBody, CancellationToken cancellationToken = default) : Task
}

class ErrorViewModel <<class>> {
  -RequestId : string?
}

class ForgotPasswordViewModel <<class>> {
  -Email : string
  -IsSubmitted : bool
}

class KetQuaKpi <<class>> {
  -MaKetQua : int
  -MaNhanVien : int
  -MaKpi : int
  -DiemSo : decimal?
  -thang : int?
  -nam : int?
  -NhanVien : NhanVien
  -DanhMucKpi : DanhMucKpi
  +Calculate(request: KpiCalculateRequest) : Task<ActionResult<ApiResponse<KpiCalculateResult>>>
  +CalculateAll(request: CalculateAllKpiRequest) : Task<ActionResult<ApiResponse<CalculateAllKpiResult>>>
  +GetByNhanVien(...) : Task<ActionResult<ApiResponse<KpiNhanVienDto>>>
  +GetByPhongBan(...) : Task<ActionResult<ApiResponse<KpiPhongBanDto>>>
}

class KetQuaKpiTong <<class>> {
  -MaKetQuaTong : int
  -MaNhanVien : int
  -Thang : int
  -Nam : int
  -DiemTong : decimal
  -XepLoai : string?
  -SoKpiThanhPhan : int
  -NgayTinh : DateTime
  -NhanVien : NhanVien
}

class KpiCalculateRequest <<class>> {
  -thang : int
  -nam : int
  -MaKpi : int?
  -MaPhongBan : int?
  -MaNhanVien : int?
}

class KpiCalculateResult <<class>> {
  -thang : int
  -nam : int
  -MaKpi : int
  -TongNhanVien : int
  -TongTaskTrongKy : int
  -SoBanGhiTaoMoi : int
  -SoBanGhiCapNhat : int
  -SoBanGhiTongTaoMoi : int
  -SoBanGhiTongCapNhat : int
}

class KpiDuAn <<class>> {
  -MaKpi : int
  -MaDuAn : int
  -TrongSoApDung : decimal
  -TuNgay : DateTime?
  -DenNgay : DateTime?
  -NgayKetThucApDung : DateTime?
  -IsActive : bool
  -TrangThai : byte?
  -GhiChu : string?
  -DanhMucKpi : DanhMucKpi
  -DuAn : DuAn
}

class KpiNhanVien <<class>> {
  -MaKpi : int
  -MaNhanVien : int
  -TrongSoApDung : decimal
  -TuNgay : DateTime?
  -DenNgay : DateTime?
  -NgayKetThucApDung : DateTime?
  -IsActive : bool
  -TrangThai : byte?
  -GhiChu : string?
  -DanhMucKpi : DanhMucKpi
  -NhanVien : NhanVien
}

class KpiNhom <<class>> {
  -MaKpi : int
  -MaNhom : int
  -TrongSoApDung : decimal
  -TuNgay : DateTime?
  -DenNgay : DateTime?
  -NgayKetThucApDung : DateTime?
  -IsActive : bool
  -TrangThai : byte?
  -GhiChu : string?
  -DanhMucKpi : DanhMucKpi
  -Nhom : Nhom
}

class KpiPhongBan <<class>> {
  -MaKpi : int
  -MaPhongBan : int
  -TrongSoApDung : decimal
  -TuNgay : DateTime?
  -DenNgay : DateTime?
  -NgayKetThucApDung : DateTime?
  -IsActive : bool
  -TrangThai : byte?
  -GhiChu : string?
  -DanhMucKpi : DanhMucKpi
  -PhongBan : PhongBan
}

class KpiService <<service>> {
  +CalculateAsync(KpiCalculateRequest request) : Task<KpiCalculateResult>
  -MaCongViec : int
  -MaNhanVien : int
  -MaDoKho : int?
  -HeSoDoKho : decimal?
  -MaTrangThai : int?
  -HanHoanThanh : DateTime
  -DiemCongViec : decimal?
  -MaKpi : int
  -TrongSoApDung : decimal
  -HeSoLoaiKpi : decimal
  -MaLoaiKpi : int
  -TenLoaiKpi : string?
}

class KpiXepLoai <<class>> {
  -Id : int
  -Code : string
  -Label : string
  -MoTa : string?
  -MinScore : decimal
  -MaxScore : decimal
  -ColorHex : string
  -SortOrder : int
  -IsActive : bool
  -IsSystem : bool
  -CreatedAt : DateTime
  -UpdatedAt : DateTime?
}

class KyNang <<class>> {
  -MaKyNang : int
  -TenKyNang : string?
  -MoTa : string?
  -TrangThai : int?
  -KyNangNhanViens : ICollection<KyNangNhanVien>
  -CongViecKyNangs : ICollection<CongViecKyNang>
}

class KyNangNhanVien <<class>> {
  -MaKyNang : int
  -MaNhanVien : int
  -CapDo : int?
  -NgayDatDuoc : DateTime?
  -SoDuAnDaDung : int?
  -KyNang : KyNang
  -NhanVien : NhanVien
}

class LoaiKpi <<class>> {
  -MaLoaiKpi : int
  -TenLoaiKpi : string?
  -HeSo : decimal
  -IsActive : bool
  -DanhMucKpis : ICollection<DanhMucKpi>
}

class LoaiThongBao <<class>> {
  -MaLoai : int
  -TenLoai : string?
  -ThongBaos : ICollection<ThongBao>
}

class LoginViewModel <<class>> {
  -UserNameOrEmail : string
  -Password : string
  -RememberMe : bool
  -ReturnUrl : string?
}

class MoHinhAi <<class>> {
  -MaModel : int
  -TenModel : string?
  -Version : string?
  -NgayTrain : DateTime?
  -DuDoanAis : ICollection<DuDoanAi>
  -MoHinhDuLieuAis : ICollection<MoHinhDuLieuAi>
  +GetModels() : Task<ActionResult<ApiResponse<List<AiModelItemDto>>>>
  +TrainModel(request: TrainModelRequest) : Task<ActionResult<ApiResponse<object>>>
  +GetModelLogs(top: int) : Task<ActionResult<ApiResponse<List<AiLogItemDto>>>>
  +GetModelPerf(maModel: int, top: int) : Task<ActionResult<ApiResponse<List<AiModelPerfPointDto>>>>
}

class MoHinhDuLieuAi <<class>> {
  -MaModel : int
  -MaDuLieu : int
  -MucDich : string?
  -NgaySuDung : DateTime?
  -MetricChinh : decimal?
  -MoHinhAi : MoHinhAi
  -DuLieuAi : DuLieuAi
}

class NhanVien <<class>> {
  -MaNhanVien : int
  -MaPhongBan : int?
  -PhoMaPhongBan : int?
  -HoTen : string?
  -NgaySinh : DateTime?
  -Cccd : string?
  -DiaChi : string?
  -GioiTinh : string?
  -Email : string?
  -Sdt : string?
  -NgayVaoLam : DateTime?
  -TrangThai : int?
  -MaChucVu : int?
  -AspNetUserId : string?
  -AspNetUser : ApplicationUser?
  -ChucVu : ChucVu?
  -PhongBanQuanLy : PhongBan?
  -PhongBanPhoTrach : PhongBan?
  -DuDoanAis : ICollection<DuDoanAi>
  -DuLieuAis : ICollection<DuLieuAi>
  -KetQuaKpis : ICollection<KetQuaKpi>
  -KetQuaKpiTongs : ICollection<KetQuaKpiTong>
  -KpiNhanViens : ICollection<KpiNhanVien>
  -KyNangNhanViens : ICollection<KyNangNhanVien>
  -NhatKyHoatDongs : ICollection<NhatKyHoatDong>
  -NhatKyCongViecs : ICollection<NhatKyCongViec>
  -PhanCongNhanViens : ICollection<PhanCongNhanVien>
  -ThanhVienNhoms : ICollection<ThanhVienNhom>
  -ThongBaoNhanViens : ICollection<ThongBaoNhanVien>
  -PhongBans : ICollection<PhongBan>
  -DuAnNhanViens : ICollection<DuAnNhanVien>
  -NhomTruongNhoms : ICollection<Nhom>
  +GetCurrentNhanVien() : Task<ActionResult<ApiResponse<NhanVienListItemDto>>>
  +GetNhanViens(...) : Task<ActionResult<ApiResponse<PagedResult<NhanVienListItemDto>>>>
  +GetNhanVienById(id: int) : Task<ActionResult<ApiResponse<NhanVienDetailDto>>>
  +CreateNhanVien(request: CreateNhanVienRequest) : Task<ActionResult<ApiResponse<NhanVienDetailDto>>>
  +UpdateNhanVien(id: int, request: UpdateNhanVienRequest) : Task<ActionResult<ApiResponse<NhanVienDetailDto>>>
  +UpdateStatus(id: int, request: UpdateStatusRequest) : Task<ActionResult<ApiResponse<object>>>
  +SoftDeleteNhanVien(id: int) : Task<ActionResult<ApiResponse<object>>>
  +AddSkill(id: int, request: AddSkillRequest) : Task<ActionResult<ApiResponse<object>>>
  +UpdateSkill(id: int, skillId: int, request: UpdateSkillRequest) : Task<ActionResult<ApiResponse<object>>>
  +RemoveSkill(id: int, skillId: int) : Task<ActionResult<ApiResponse<object>>>
}

class NhatKyCongViec <<class>> {
  -MaNhatKy : int
  -MaCongViec : int
  -MaNhanVien : int
  -PhanTramHoanThanh : decimal?
  -NgayCapNhat : DateTime?
  -GhiChu : string?
  -CongViec : virtual CongViec
  -NhanVien : virtual NhanVien
}

class NhatKyHoatDong <<class>> {
  -MaNhatKyHoatDong : int
  -MaNhanVien : int
  -HanhDong : string?
  -DoiTuong : string?
  -DuLieuCu : string?
  -DuLieuMoi : string?
  -ThoiGian : DateTime?
  -Ip : string?
  -TrangThai : string?
  -NhanVien : NhanVien
}

class Nhom <<class>> {
  -MaNhom : int
  -TenNhom : string?
  -NgayTao : DateTime?
  -TruongNhom : int?
  -NhanVienTruongNhom : NhanVien?
  -ThanhVienNhoms : ICollection<ThanhVienNhom>
  -KpiNhoms : ICollection<KpiNhom>
  -DuAnNhoms : ICollection<DuAnNhom>
  -PhanCongNhoms : ICollection<PhanCongNhom>
}

class PhanCongNhanVien <<class>> {
  -MaPhaCong : int
  -MaCongViec : int
  -MaNhanVien : int
  -NgayBatDauDuKien : DateTime?
  -NgayKetThucdukien : DateTime?
  -NgayBatDauThucTe : DateTime?
  -NgayKetThucThucTe : DateTime?
  -PhanTramHoanThanh : decimal?
  -TrangThai : int?
  -CongViec : virtual CongViec
  -NhanVien : virtual NhanVien
}

class PhanCongNhom <<class>> {
  -MaCongViec : int
  -MaNhom : int
  -NgayGiao : DateTime?
  -TrangThai : int?
  -CongViec : virtual CongViec
  -Nhom : virtual Nhom
}

class PhanCongPhongBan <<class>> {
  -MaPhongBan : int
  -MaCongViec : int
  -NgayPhanCong : DateTime?
  -TrangThai : int?
  -CongViec : virtual CongViec
  -PhongBan : virtual PhongBan
}

class PhongBan <<class>> {
  -MaPhongBan : int
  -TenPhongBan : string?
  -MoTa : string?
  -MaTruongPhong : int?
  -TruongPhong : NhanVien?
  -NhanVienQuanLys : ICollection<NhanVien>
  -NhanVienPhoTrachs : ICollection<NhanVien>
  -KpiPhongBans : ICollection<KpiPhongBan>
  -DuAnPhongBans : ICollection<DuAnPhongBan>
  -PhanCongPhongBans : ICollection<PhanCongPhongBan>
}

class ResetPasswordViewModel <<class>> {
  -Email : string
  -Token : string
  -NewPassword : string
  -ConfirmPassword : string
  -IsCompleted : bool
}

class ThanhVienNhom <<class>> {
  -MaNhanVien : int
  -MaNhom : int
  -NgayGiaNhap : DateTime?
  -VaiTroTrongNhom : string?
  -NhanVien : NhanVien
  -Nhom : Nhom
}

class ThongBao <<class>> {
  -MaThongBao : int
  -MaLoai : int
  -NoiDung : string?
  -ThoiGian : DateTime?
  -LoaiThongBao : LoaiThongBao
  -ThongBaoNhanViens : ICollection<ThongBaoNhanVien>
  +GetList(...) : Task<ActionResult<ApiResponse<ThongBaoListDto>>>
  +GetSummary(...) : Task<ActionResult<ApiResponse<ThongBaoSummaryDto>>>
  +MarkAllRead(request: ThongBaoBulkRequest?) : Task<ActionResult<ApiResponse<object>>>
  +MarkRead(maThongBao: int, request: ThongBaoBulkRequest?) : Task<ActionResult<ApiResponse<object>>>
  +Delete(maThongBao: int, maNhanVien: int?) : Task<ActionResult<ApiResponse<object>>>
}

class ThongBaoNhanVien <<class>> {
  -MaNhanVien : int
  -MaThongBao : int
  -DaDoc : bool?
  -NhanVien : NhanVien
  -ThongBao : ThongBao
}

class TienDoCongViec <<class>> {
  -MaTienDo : int
  -MaCongViec : int
  -PhanTramHoanThanh : decimal?
  -TrangThaiHienTai : int?
  -NgayCapNhat : DateTime?
  -TrangThaiPheDuyet : string?
  -NguoiPheDuyet : int?
  -NgayPheDuyet : DateTime?
  -LyDoTuChoi : string?
  -CongViec : virtual CongViec
  -NguoiPheDuyetNavigation : virtual NhanVien
}

class YeuCauBaoCao <<class>> {
  -MaYeuCau : int
  -NguoiYeuCau : string?
  -NguoiNhanYeuCau : string?
  -TieuDe : string?
  -MoTa : string?
  -Priority : string?
  -HanChot : DateTime?
  -TrangThai : string?
  -NgayTao : DateTime?
  -NgayCapNhat : DateTime?
  -IsDeleted : bool
  -NguoiYeuCauNavigation : ApplicationUser?
  -NguoiNhanYeuCauNavigation : ApplicationUser?
  +CreateRequest(request: CreateReportRequestDto) : Task<ActionResult<ApiResponse<object>>>
  +RequestList() : Task<ActionResult<ApiResponse<List<ReportRequestDto>>>>
  +RequestDetail(id: int) : Task<ActionResult<ApiResponse<ReportRequestDto>>>
  +RequestCancel(id: int) : Task<ActionResult<ApiResponse<object>>>
}

class YeuCauCapNhatHoSo <<class>> {
  -MaYeuCau : int
  -MaNhanVien : int
  -TrangThai : string
  -DanhSachTruong : string?
  -DuLieuCuJson : string?
  -DuLieuMoiJson : string?
  -LyDoGui : string?
  -LyDoTuChoi : string?
  -GhiChuDuyet : string?
  -NguoiTao : int?
  -NguoiDuyet : int?
  -NguoiCapNhat : int?
  -NgayTao : DateTime?
  -NgayDuyet : DateTime?
  -NgayCapNhat : DateTime?
  -IpTao : string?
  -IpDuyet : string?
  -IsDeleted : bool
  -NhanVien : NhanVien?
  -NhanVienDuyet : NhanVien?
}

ApplicationUser --|> IdentityUser
AppDbContext --|> IdentityDbContext
ApplicationUser "1" --> "0..1" NhanVien : NhanVien

AppDbContext ..> NhanVien
AppDbContext ..> CongViec
AppDbContext ..> DuAn
AppDbContext ..> KetQuaKpi
AppDbContext ..> DuDoanAi

AiPredictionService ..> IAiPredictionService
AiEvaluationService ..> IAiEvaluationService
EmailService ..> IEmailService
AuditLogService ..> IAuditLogService
KpiService ..> IKpiService
