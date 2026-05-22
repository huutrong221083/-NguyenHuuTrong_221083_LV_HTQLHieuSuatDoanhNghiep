using System;

namespace LuanVan.Contracts;

/// <summary>
/// DTO cho danh sách công việc của nhân viên
/// </summary>
public class EmployeeTaskDto
{
    public int MaCongViec { get; set; }
    public string? TenCongViec { get; set; }
    public string? MoTa { get; set; }
    public int? MaDuAn { get; set; }
    public string? TenDuAn { get; set; }
    public DateTime? NgayBatDau { get; set; }
    public DateTime? HanHoanThanh { get; set; }
    public decimal TienDoPhanTram { get; set; }
    public string? TenTrangThai { get; set; }
    public string? TenDoUuTien { get; set; }
}

/// <summary>
/// DTO cho yêu cầu báo cáo từ quản lý
/// </summary>
public class ReportRequestDto
{
    public int MaYeuCau { get; set; }
    public string? TieuDe { get; set; }
    public string? MoTa { get; set; }
    public string? Priority { get; set; }
    public DateTime? HanChot { get; set; }
    public string? TrangThai { get; set; }
    public DateTime? NgayTao { get; set; }
    public string? NguoiYeuCau { get; set; } // Tên người yêu cầu
}

/// <summary>
/// Request DTO cho lưu nháp báo cáo
/// </summary>
public class SaveReportDraftRequest
{
    public string? TenBaoCao { get; set; }
    public string? LoaiBaoCao { get; set; } // daily, weekly, monthly, kpi, project, work, urgent
    public string? NguoiTao { get; set; }
    public DateTime? NgayTao { get; set; }
    public DateTime? NgayBatDau { get; set; }
    public DateTime? NgayKetThuc { get; set; }
    public int? MaDuAn { get; set; }
    public List<int>? CongViecDuocChon { get; set; }
    public string? NguoiNhanUserId { get; set; }
    public string? NguoiNhanBaoCao { get; set; }
    public string? NoiDung { get; set; } // JSON content
    public List<string>? TaiLieu { get; set; } // File names
}

/// <summary>
/// Request DTO cho gửi báo cáo
/// </summary>
public class SubmitReportRequest
{
    public int MaYeuCau { get; set; } // MaYeuCau nếu là respond to request
    public string? TenBaoCao { get; set; }
    public string? LoaiBaoCao { get; set; }
    public DateTime? NgayBatDau { get; set; }
    public DateTime? NgayKetThuc { get; set; }
    public int? MaDuAn { get; set; }
    public List<int>? CongViecDuocChon { get; set; }
    public string? NguoiNhanUserId { get; set; }
    public string? NguoiNhanBaoCao { get; set; }
    public string? NoiDung { get; set; } // JSON content
    public List<string>? TaiLieu { get; set; }
}

/// <summary>
/// Response DTO cho lấy danh sách công việc + yêu cầu
/// </summary>
public class ReportPageLoadDto
{
    public List<EmployeeTaskDto> MyTasks { get; set; } = new();
    public List<ReportRequestDto> MyRequests { get; set; } = new();
    public List<EmployeeDto> Managers { get; set; } = new();
    public PersonalKpiSidebarDto PersonalKpi { get; set; } = new();
}

/// <summary>
/// KPI cá nhân hiển thị ở sidebar
/// </summary>
public class PersonalKpiSidebarDto
{
    public decimal DiemHienTai { get; set; }
    public string? XepLoai { get; set; }
    public double? XuHuongPhanTram { get; set; }
    public DateTime? NgayCapNhatGanNhat { get; set; }
    public List<KpiHistoryItemDto> LichSu { get; set; } = new();
}

public class KpiHistoryItemDto
{
    public int Thang { get; set; }
    public int Nam { get; set; }
    public decimal DiemSo { get; set; }
    public string? XepLoai { get; set; }
}

/// <summary>
/// DTO cho Employee (để chọn người nhận)
/// </summary>
public class EmployeeDto
{
    public int MaNhanVien { get; set; }
    public string? HoTen { get; set; }
    public string? AspNetUserId { get; set; }
}

public static class ReportWorkflowStatus
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
    public const string Overdue = "Overdue";
}

public class ReportReviewRequest
{
    public int MaBaoCao { get; set; }
    public string? Note { get; set; }
}

public class CreateReportRequestDto
{
    public string? NguoiNhanUserId { get; set; }
    public string? TieuDe { get; set; }
    public string? MoTa { get; set; }
    public string? Priority { get; set; }
    public DateTime? HanChot { get; set; }
}
