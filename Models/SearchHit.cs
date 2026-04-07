namespace SodickMastermind.Models;

public sealed class SearchHit
{
    public string FileName { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public string Snippet { get; set; } = string.Empty;
    public int Score { get; set; }
    public string PdfPath { get; set; } = string.Empty;
    public string JsonPath { get; set; } = string.Empty;
}