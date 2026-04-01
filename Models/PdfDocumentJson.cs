using SodickDataLake.Models;

namespace SodickDataLake.Functions.Models;

public sealed class PdfDocumentJson
{
    public string FileName { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public List<PdfPageJson> Pages { get; set; } = new();
}