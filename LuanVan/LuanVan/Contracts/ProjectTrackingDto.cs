namespace LuanVan.Contracts;

public class ProjectTrackingDto
{
    public int TongCongViec { get; set; }
    public int CongViecHoanThanh { get; set; }
    public int CongViecTreHan { get; set; }
    public double PhanTramHoanThanh { get; set; }
    public string PhanLoaiTienDo { get; set; } = string.Empty;
    public ProjectAiWarningDto? AiCanhBao { get; set; }
}

public class ProjectAiWarningDto
{
    public double TyLeRuiRoTreHan { get; set; }
    public int SoNhanVienRuiRoCao { get; set; }
    public string MucCanhBao { get; set; } = "Thấp";
    public DateTime? NgayTreDuKien { get; set; }
}
