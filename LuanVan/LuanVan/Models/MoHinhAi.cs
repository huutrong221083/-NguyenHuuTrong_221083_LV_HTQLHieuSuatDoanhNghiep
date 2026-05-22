using System;
using System.Collections.Generic;
using LuanVan.Data;

namespace LuanVan.Models;

public class MoHinhAi
{
    public int MaModel { get; set; }
    public string? TenModel { get; set; }
    public string? Version { get; set; }
    public DateTime? NgayTrain { get; set; }

    public ICollection<DuDoanAi> DuDoanAis { get; set; } = new List<DuDoanAi>();
    public ICollection<MoHinhDuLieuAi> MoHinhDuLieuAis { get; set; } = new List<MoHinhDuLieuAi>();
}

