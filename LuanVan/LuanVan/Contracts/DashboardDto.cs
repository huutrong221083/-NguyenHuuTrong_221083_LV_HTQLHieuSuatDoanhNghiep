using System.Collections.Generic;

namespace LuanVan.Contracts;

public class DashboardResponseDto
{
    public DashboardTongQuanDto TongQuan { get; set; } = new();
    public List<DashboardPhongBanKpiDto> KpiTheoPhongBan { get; set; } = new();
    public List<DashboardNhanVienKpiDto> TopNhanVien { get; set; } = new();
    public List<DashboardTaskTreHanDto> TaskTreHan { get; set; } = new();
    public bool IsPersonalDashboard { get; set; }
    public DashboardNhanVienContextDto? NhanVienContext { get; set; }
    public DashboardScopeContextDto ScopeContext { get; set; } = new();
    public DashboardFilterOptionsDto Filters { get; set; } = new();
    public List<DashboardSummaryCardDto> SummaryCards { get; set; } = new();
    public DashboardTaskStatusDistributionDto TaskStatusDistribution { get; set; } = new();
    public DashboardRiskDto Risk { get; set; } = new();
    public DashboardKpiTrendDto KpiTrend { get; set; } = new();
    public List<DashboardUrgentTaskDto> UrgentTasks { get; set; } = new();
    public List<DashboardAttentionEmployeeDto> AttentionEmployees { get; set; } = new();
    public List<DashboardWorkloadDto> TeamWorkload { get; set; } = new();
    public DashboardPendingApprovalsDto PendingApprovals { get; set; } = new();
    public List<DashboardInsightDto> Insights { get; set; } = new();
    public string InsightSource { get; set; } = "fallback";
    public List<DashboardTimelineItemDto> ActivityTimeline { get; set; } = new();
    public List<DashboardProjectHealthDto> ProjectHealth { get; set; } = new();
}

public class DashboardTongQuanDto
{
    public int SoNhanVien { get; set; }
    public int SoDuAnDangChay { get; set; }
    public int SoTaskDangLam { get; set; }
    public int SoTaskHoanThanh { get; set; }
    public int SoTaskTreHan { get; set; }
    public decimal KpiTrungBinh { get; set; }
}

public class DashboardPhongBanKpiDto
{
    public int MaPhongBan { get; set; }
    public string? TenPhongBan { get; set; }
    public decimal Kpi { get; set; }
}

public class DashboardNhanVienKpiDto
{
    public int MaNhanVien { get; set; }
    public string? HoTen { get; set; }
    public decimal Kpi { get; set; }
}

public class DashboardTaskTreHanDto
{
    public int MaCongViec { get; set; }
    public string? TenTask { get; set; }
    public List<string> NhanViens { get; set; } = new();
    public DateTime? Deadline { get; set; }
    public int SoNgayTre { get; set; }
}

public class DashboardNhanVienContextDto
{
    public int MaNhanVien { get; set; }
    public string? HoTen { get; set; }
    public int? MaPhongBan { get; set; }
    public string? TenPhongBan { get; set; }
    public decimal KpiCaNhan { get; set; }
    public int SoCanhBaoChuaDoc { get; set; }
    public List<string> NhomThamGia { get; set; } = new();
    public List<DashboardAlertDto> CanhBaoCaNhan { get; set; } = new();
}

public class DashboardAlertDto
{
    public int MaThongBao { get; set; }
    public string? NoiDung { get; set; }
    public string? Loai { get; set; }
    public DateTime? ThoiGian { get; set; }
    public bool DaDoc { get; set; }
}

public class DashboardScopeContextDto
{
    public string RoleKey { get; set; } = "employee";
    public string ManagerType { get; set; } = "none";
    public int? MaNhanVien { get; set; }
    public int? MaPhongBan { get; set; }
    public int? MaNhom { get; set; }
    public int? MaDuAn { get; set; }
    public string? ScopeName { get; set; }
    public string DataSource { get; set; } = "real";
}

public class DashboardFilterOptionsDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public List<DashboardFilterItemDto> Departments { get; set; } = new();
    public List<DashboardFilterItemDto> Teams { get; set; } = new();
    public List<DashboardFilterItemDto> Projects { get; set; } = new();
    public int? SelectedDepartmentId { get; set; }
    public int? SelectedTeamId { get; set; }
    public int? SelectedProjectId { get; set; }
    public string DataSource { get; set; } = "real";
}

public class DashboardFilterItemDto
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class DashboardSummaryCardDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = "0";
    public string DataSource { get; set; } = "real";
}

public class DashboardTaskStatusDistributionDto
{
    public int ChuaBatDau { get; set; }
    public int DangThucHien { get; set; }
    public int ChoDuyet { get; set; }
    public int HoanThanh { get; set; }
    public int QuaHan { get; set; }
    public int TongTask { get; set; }
    public string DataSource { get; set; } = "real";
}

public class DashboardRiskDto
{
    public decimal Score { get; set; }
    public string Level { get; set; } = "Thấp";
    public string Label { get; set; } = "Mức rủi ro";
    public string Source { get; set; } = "fallback";
    public string Message { get; set; } = string.Empty;
    public string DataSource { get; set; } = "fallback";
}

public class DashboardKpiTrendDto
{
    public decimal PreviousValue { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal DeltaPercent { get; set; }
    public List<string> Labels { get; set; } = new();
    public List<decimal> Values { get; set; } = new();
    public string DataSource { get; set; } = "real";
}

public class DashboardUrgentTaskDto
{
    public int MaCongViec { get; set; }
    public string TenCongViec { get; set; } = string.Empty;
    public string NguoiPhuTrach { get; set; } = string.Empty;
    public string DuAn { get; set; } = string.Empty;
    public DateTime? Deadline { get; set; }
    public decimal TienDo { get; set; }
    public string TrangThai { get; set; } = string.Empty;
    public string PriorityReason { get; set; } = string.Empty;
    public string DataSource { get; set; } = "real";
}

public class DashboardAttentionEmployeeDto
{
    public int MaNhanVien { get; set; }
    public string HoTen { get; set; } = string.Empty;
    public decimal Kpi { get; set; }
    public int TaskTre { get; set; }
    public int TaskDangLam { get; set; }
    public string MucTai { get; set; } = "Bình thường";
    public string CanhBao { get; set; } = "Ổn định";
    public string DataSource { get; set; } = "derived";
}

public class DashboardWorkloadDto
{
    public int MaNhanVien { get; set; }
    public string HoTen { get; set; } = string.Empty;
    public int TaskDangLam { get; set; }
    public string MucTai { get; set; } = "Bình thường";
    public string DataSource { get; set; } = "real";
}

public class DashboardPendingApprovalsDto
{
    public int TienDoChoDuyet { get; set; }
    public int KpiChoDuyet { get; set; }
    public int BaoCaoChoXem { get; set; }
    public int DeXuatHeThongChoXuLy { get; set; }
    public string DataSource { get; set; } = "real";
}

public class DashboardInsightDto
{
    public string Type { get; set; } = "info";
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string DataSource { get; set; } = "fallback";
}

public class DashboardTimelineItemDto
{
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public DateTime? Time { get; set; }
    public string DataSource { get; set; } = "real";
}

public class DashboardProjectHealthDto
{
    public int MaDuAn { get; set; }
    public string TenDuAn { get; set; } = string.Empty;
    public decimal TienDo { get; set; }
    public decimal Kpi { get; set; }
    public DateTime? Deadline { get; set; }
    public int TaskTre { get; set; }
    public string Risk { get; set; } = "Thấp";
    public string DataSource { get; set; } = "derived";
}
