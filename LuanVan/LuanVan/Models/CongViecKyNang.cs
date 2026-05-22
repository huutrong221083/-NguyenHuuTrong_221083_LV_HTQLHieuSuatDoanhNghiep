namespace LuanVan.Models;

public class CongViecKyNang
{
    public int MaCongViec { get; set; }
    public int MaKyNang { get; set; }

    // Navigation properties
    public virtual CongViec CongViec { get; set; }
    public virtual KyNang KyNang { get; set; }
}
