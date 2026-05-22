namespace LuanVan.Contracts;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; }
    public int Size { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}
