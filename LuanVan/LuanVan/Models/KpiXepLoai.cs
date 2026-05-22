namespace LuanVan.Models;

public class KpiXepLoai
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? MoTa { get; set; }
    public decimal MinScore { get; set; }
    public decimal MaxScore { get; set; }
    public string ColorHex { get; set; } = "#64748B";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSystem { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
