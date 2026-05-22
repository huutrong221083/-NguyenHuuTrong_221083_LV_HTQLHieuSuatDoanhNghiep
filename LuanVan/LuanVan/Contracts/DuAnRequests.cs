namespace LuanVan.Contracts;

public class UpsertDuAnRequest
{
    public string TenDuAn { get; set; } = string.Empty;
    public string? MoTa { get; set; }
    public DateTime? NgayBatDau { get; set; }
    public DateTime? NgayKetThuc { get; set; }
    public int? TrangThai { get; set; }
    public int? MaPhongBan { get; set; }
}

public class AssignEmployeeRequest
{
    public int MaNhanVien { get; set; }
    public string? VaiTro { get; set; }
}

public class AssignTeamRequest
{
    public int MaNhom { get; set; }
    public bool TuDongThemThanhVienNhom { get; set; }
}

public class AssignDepartmentRequest
{
    public int MaPhongBan { get; set; }
}
