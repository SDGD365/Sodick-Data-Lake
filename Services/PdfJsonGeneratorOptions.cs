namespace SodickMastermind.Services;

public sealed class PdfJsonGeneratorOptions
{
    public string SourceContainerName { get; set; } = "books";
    public string DefaultOutputFolder { get; set; } = "processed";
}
