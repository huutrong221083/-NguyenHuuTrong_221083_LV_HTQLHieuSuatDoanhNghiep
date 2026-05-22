namespace LuanVan.Contracts;

public class ProjectTaskDto
{
    public int MaCongViec { get; set; }
    public string TenCongViec { get; set; } = string.Empty;
    public DateTime? HanHoanThanh { get; set; }
    public int MaTrangThai { get; set; }
    public List<string> NguoiThucHien { get; set; } = new();
    public double PhanTramHoanThanh { get; set; }
    public bool LaTreHan { get; set; }
}
