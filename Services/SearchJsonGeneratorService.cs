using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using SodickDataLake.Functions.Models;
using SodickDataLake.Services;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SodickDataLake.Functions.Services;

public sealed class SearchJsonGeneratorService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly PdfJsonGeneratorOptions _options;

    public SearchJsonGeneratorService(
        BlobServiceClient blobServiceClient,
        PdfJsonGeneratorOptions options)
    {
        _blobServiceClient = blobServiceClient;
        _options = options;
    }

    public async Task<GenerateSearchJsonResponse> GenerateAsync(
        string sourcePath,
        string? targetPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("sourcePath is required.", nameof(sourcePath));

        var container = _blobServiceClient.GetBlobContainerClient(_options.SourceContainerName);
        var sourceBlob = container.GetBlobClient(sourcePath);

        if (!await sourceBlob.ExistsAsync(cancellationToken))
        {
            return new GenerateSearchJsonResponse
            {
                Success = false,
                SourcePath = sourcePath,
                Message = "Source JSON was not found."
            };
        }

        targetPath ??= BuildDefaultTargetPath(sourcePath);

        PdfDocumentJson? pdfDocument;
        try
        {
            var download = await sourceBlob.DownloadContentAsync(cancellationToken);
            var json = download.Value.Content.ToString();

            pdfDocument = JsonSerializer.Deserialize<PdfDocumentJson>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            return new GenerateSearchJsonResponse
            {
                Success = false,
                SourcePath = sourcePath,
                TargetPath = targetPath,
                Message = $"Could not read source JSON: {ex.Message}"
            };
        }

        if (pdfDocument is null)
        {
            return new GenerateSearchJsonResponse
            {
                Success = false,
                SourcePath = sourcePath,
                TargetPath = targetPath,
                Message = "Source JSON could not be deserialized."
            };
        }

        var baseName = Path.GetFileNameWithoutExtension(pdfDocument.FileName);
        var items = new List<SearchPageItem>();

        foreach (var page in pdfDocument.Pages)
        {
            var text = page.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var normalizedSearchText = NormalizeForSearch(text);
            if (string.IsNullOrWhiteSpace(normalizedSearchText))
                continue;

            items.Add(new SearchPageItem
            {
                Id = $"{baseName}-p{page.PageNumber}",
                FileName = pdfDocument.FileName,
                PageNumber = page.PageNumber,
                Text = text,
                SearchText = normalizedSearchText
            });
        }

        var outputJson = JsonSerializer.Serialize(items, new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = true
        });

        var targetBlob = container.GetBlobClient(targetPath);

        using var outputStream = new MemoryStream(Encoding.UTF8.GetBytes(outputJson));

        await targetBlob.UploadAsync(
            outputStream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "application/json; charset=utf-8"
                }
            },
            cancellationToken);

        return new GenerateSearchJsonResponse
        {
            Success = true,
            SourcePath = sourcePath,
            TargetPath = targetPath,
            OutputBlobUrl = targetBlob.Uri.ToString(),
            Message = "Search JSON generated successfully.",
            ItemCount = items.Count
        };
    }

    private static string NormalizeForSearch(string text)
    {
        var s = text.ToLowerInvariant();

        s = s.Replace("\r\n", " ");
        s = s.Replace("\n", " ");
        s = s.Replace("\r", " ");

        s = s.Replace("-", " ");
        s = s.Replace("/", " ");
        s = s.Replace("\\", " ");

        s = s.Replace("(", " ");
        s = s.Replace(")", " ");
        s = s.Replace("[", " ");
        s = s.Replace("]", " ");
        s = s.Replace("{", " ");
        s = s.Replace("}", " ");

        s = s.Replace(".", " ");
        s = s.Replace(",", " ");
        s = s.Replace(":", " ");
        s = s.Replace(";", " ");
        s = s.Replace("!", " ");
        s = s.Replace("?", " ");
        s = s.Replace("\"", " ");
        s = s.Replace("'", " ");

        s = Regex.Replace(s, @"\s+", " ").Trim();

        return s;
    }

    private static string BuildDefaultTargetPath(string sourcePath)
    {
        var path = sourcePath.Replace("\\", "/");
        if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return path[..^5] + ".search.json";
        }

        return path + ".search.json";
    }
}