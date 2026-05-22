using System;

namespace LuanVan.Models;

public class AiNhatKyCanThiep
{
    public int MaCanThiep { get; set; }
    public int? MaDanhGia { get; set; }
    public int? MaDuDoan { get; set; }
    public int? MaNhanVien { get; set; }
    public int? NguoiCanThiep { get; set; }
    public string? ActionType { get; set; }
    public string? ActionSource { get; set; }
    public string? Reason { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? NguonCanThiep { get; set; }
    public DateTime? NgayCanThiep { get; set; }
    public int? SoLanChinhSua { get; set; }
}
