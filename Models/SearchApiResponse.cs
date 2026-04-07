namespace SodickMastermind.Models;

public sealed class SearchApiResponse
{
    public string Answer { get; set; } = string.Empty;
    public List<SearchHit> Results { get; set; } = new();
    public string CitationsJson { get; set; } = string.Empty;
}