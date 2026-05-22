using System;

namespace LuanVan.Contracts;

public class TaskListItemDto
{
    public int MaCongViec { get; set; }
    public string? TenCongViec { get; set; }
    public string? MoTa { get; set; }
    public int MaDuAn { get; set; }
    public string? TenDuAn { get; set; }
    public int? MaCongViecCha { get; set; }
    public string? TenCongViecCha { get; set; }
    public int MaDoUuTien { get; set; }
    public string? TenDoUuTien { get; set; }
    public int MaDoKho { get; set; }
    public string? TenDoKho { get; set; }
    public int MaTrangThai { get; set; }
    public string? TenTrangThai { get; set; }
    public DateTime? NgayBatDau { get; set; }
    public DateTime? HanHoanThanh { get; set; }
    public decimal? DiemCongViec { get; set; }
    public decimal TienDoPhanTram { get; set; }
    public List<TaskAssigneeDto> NguoiDuocGiao { get; set; } = new();
    public List<TaskTeamDto> NhomDuocGiao { get; set; } = new();
    public List<TaskDepartmentDto> PhongBanDuocGiao { get; set; } = new();
    public List<TaskChildDto> CongViecCon { get; set; } = new();
    public List<TaskActivityDto> HoatDongCongViecs { get; set; } = new();
    public List<TaskActivityDto> LichSuTienDo { get; set; } = new();
    public List<TaskCommentDto> BinhLuans { get; set; } = new();
}

public class TaskDetailDto : TaskListItemDto
{
}

public class TaskAssigneeDto
{
    public int MaNhanVien { get; set; }
    public string? HoTen { get; set; }
}

public class TaskTeamDto
{
    public int MaNhom { get; set; }
    public string? TenNhom { get; set; }
}

public class TaskDepartmentDto
{
    public int MaPhongBan { get; set; }
    public string? TenPhongBan { get; set; }
}

public class TaskChildDto
{
    public int MaCongViec { get; set; }
    public string? TenCongViec { get; set; }
    public int MaTrangThai { get; set; }
    public DateTime? HanHoanThanh { get; set; }
    public decimal TienDoPhanTram { get; set; }
}

public class TaskActivityDto
{
    public int Id { get; set; }
    public string? Loai { get; set; }
    public string? NoiDung { get; set; }
    public string? GhiChu { get; set; }
    public decimal? PhanTramHoanThanh { get; set; }
    public string? TrangThaiPheDuyet { get; set; }
    public int? NguoiPheDuyet { get; set; }
    public string? HoTenNguoiPheDuyet { get; set; }
    public DateTime? NgayPheDuyet { get; set; }
    public string? LyDoTuChoi { get; set; }
    public DateTime? NgayTao { get; set; }
    public DateTime? NgayCapNhat { get; set; }
    public int? MaNhanVien { get; set; }
    public string? HoTenNhanVien { get; set; }
}

public class TaskCommentDto
{
    public int MaBinhLuan { get; set; }
    public int MaCongViec { get; set; }
    public int MaNhanVien { get; set; }
    public string? HoTenNhanVien { get; set; }
    public string? NoiDung { get; set; }
    public DateTime? NgayTao { get; set; }
}

public class CreateUpdateTaskRequest
{
    public string? TenCongViec { get; set; }
    public string? MoTa { get; set; }
    public int? MaDuAn { get; set; }
    public int? MaCongViecCha { get; set; }
    public int? MaDoKho { get; set; }
    public int? MaDoUuTien { get; set; }
    public DateTime? NgayBatDau { get; set; }
    public DateTime? Deadline { get; set; }
    public int? MaTrangThai { get; set; }
    public decimal? DiemCongViec { get; set; }
}

public class UpdateTaskStatusRequest
{
    public int MaTrangThai { get; set; }
}

public class TaskAssignmentRequest
{
    public int? MaCongViec { get; set; }
    public int? MaNhanVien { get; set; }
    public int? MaNhom { get; set; }
    public int? MaPhongBan { get; set; }
}

public class UpdateProgressRequest
{
    public int MaCongViec { get; set; }
    public int MaNhanVien { get; set; }
    public decimal PhanTramHoanThanh { get; set; }
    public string? GhiChu { get; set; }
}

public class ToggleChildCompletionRequest
{
    public bool IsDone { get; set; }
    public int? MaNhanVien { get; set; }
    public string? GhiChu { get; set; }
}

public class CreateCommentRequest
{
    public string? NoiDung { get; set; }
}

// ========== Progress Approval DTOs ==========

public class ProgressUpdateDto
{
    public int MaTienDo { get; set; }
    public int MaCongViec { get; set; }
    public string? TenCongViec { get; set; }
    public int MaDuAn { get; set; }
    public string? TenDuAn { get; set; }
    public decimal? PhanTramHoanThanh { get; set; }
    public int? TrangThaiHienTai { get; set; }
    public DateTime? NgayCapNhat { get; set; }
    public string? TrangThaiPheDuyet { get; set; } // "Chờ duyệt", "Đã duyệt", "Từ chối"
    public int? NguoiPheDuyet { get; set; }
    public string? HoTenNguoiPheDuyet { get; set; }
    public DateTime? NgayPheDuyet { get; set; }
    public string? LyDoTuChoi { get; set; }
}

public class ApproveProgressRequest
{
    public int MaTienDo { get; set; }
    public string? GhiChu { get; set; }
}

public class RejectProgressRequest
{
    public int MaTienDo { get; set; }
    public string? LyDoTuChoi { get; set; }
}
