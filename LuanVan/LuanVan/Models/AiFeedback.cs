using System;

namespace LuanVan.Models;

public class AiFeedback
{
    public int MaFeedback { get; set; }
    public int? MaDanhGia { get; set; }
    public int? MaDuDoan { get; set; }
    public int? MaNhanVien { get; set; }
    public int? DoChinhXac { get; set; }
    public int? MucHuuIch { get; set; }
    public bool? DungSai { get; set; }
    public string? NoiDung { get; set; }
    public string? HanhDongDeXuat { get; set; }
    public DateTime? NgayPhanHoi { get; set; }
}