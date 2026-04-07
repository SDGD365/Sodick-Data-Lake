namespace SodickMastermind.Models;

public sealed class Citation
{
    public int Page { get; set; }
    public string Title { get; set; } = "";
    public string ViewerUrl { get; set; } = "";
    public string Snippet { get; set; } = "";
}
