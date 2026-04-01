namespace SodickDataLake.Models;

public sealed class ManualSearchItem
{
    public string Id { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public string Text { get; set; } = string.Empty;
    public string SearchText { get; set; } = string.Empty;
}