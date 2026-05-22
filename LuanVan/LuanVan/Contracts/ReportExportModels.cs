using System.Collections.Generic;

namespace LuanVan.Contracts;

public class ReportContentDto
{
    public string? Completed { get; set; }
    public string? Ongoing { get; set; }
    public string? Challenges { get; set; }
    public string? Support { get; set; }
}
