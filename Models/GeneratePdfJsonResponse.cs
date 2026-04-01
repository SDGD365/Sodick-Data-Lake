namespace SodickDataLake.Models;

public sealed class GeneratePdfJsonResponse
{
    public bool Success { get; set; }
    public string? SourcePath { get; set; }
    public string? TargetPath { get; set; }
    public string? OutputBlobUrl { get; set; }
    public string? Message { get; set; }
    public int? PageCount { get; set; }
}