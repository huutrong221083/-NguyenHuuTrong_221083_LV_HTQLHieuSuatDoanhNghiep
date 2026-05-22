namespace LuanVan.Models;

public class TienDoCongViec
{
    //public string MaTienDo { get; set; }
    public int MaTienDo { get; set; }
    public int MaCongViec { get; set; }
    public decimal? PhanTramHoanThanh { get; set; }
    public int? TrangThaiHienTai { get; set; }
    public DateTime? NgayCapNhat { get; set; }

    // Approval fields
    public string? TrangThaiPheDuyet { get; set; } // "Chờ duyệt", "Đã duyệt", "Từ chối"
    public int? NguoiPheDuyet { get; set; } // MaNhanVien of approver
    public DateTime? NgayPheDuyet { get; set; }
    public string? LyDoTuChoi { get; set; } // Reason for rejection

    // Navigation properties
    public virtual CongViec CongViec { get; set; }
    public virtual NhanVien NguoiPheDuyetNavigation { get; set; } // FK to NhanVien
}
