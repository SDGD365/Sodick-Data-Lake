using System.Text.Json;

namespace SodickDataLake.Models;

public sealed class SearchApiResponse
{
    public string Answer { get; set; } = string.Empty;
    public List<SearchHit> Results { get; set; } = new();

    public string CitationsJson =>
        JsonSerializer.Serialize(
            Results.Select(r => new
            {
                r.FileName,
                r.PageNumber
            }));
}