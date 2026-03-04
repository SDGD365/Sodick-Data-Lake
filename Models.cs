using System.Text.Json.Serialization;

public sealed class Chunk
{
    [JsonPropertyName("chunkId")] public string ChunkId { get; set; } = "";
    [JsonPropertyName("page")] public int Page { get; set; }
    [JsonPropertyName("lang")] public string Lang { get; set; } = "de";
    [JsonPropertyName("text")] public string Text { get; set; } = "";
    [JsonPropertyName("pdfPath")] public string PdfPath { get; set; } = "";
}

public sealed class SearchHit
{
    public string ChunkId { get; set; } = "";
    public int Page { get; set; }
    public double Score { get; set; }
    public string Snippet { get; set; } = "";
    public string PdfPath { get; set; } = "";
}

public sealed class AskRequest
{
    public string DocId { get; set; } = "demo";
    public string Version { get; set; } = "v1";
    public string Question { get; set; } = "";
    public string Lang { get; set; } = "auto"; // auto | de | en
}

public sealed class Citation
{
    public int Page { get; set; }
    public string Title { get; set; } = "";
    public string ViewerUrl { get; set; } = "";
    public string Snippet { get; set; } = "";
}

public sealed class AskResponse
{
    public string Answer { get; set; } = "";
    public List<Citation> Citations { get; set; } = new();
}