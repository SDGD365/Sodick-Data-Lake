namespace SodickMastermind.Models;

public sealed class AskRequest
{
    public string DocId { get; set; } = "demo";
    public string Version { get; set; } = "v1";
    public string Question { get; set; } = "";
    public string Lang { get; set; } = "auto"; // auto | de | en
}

