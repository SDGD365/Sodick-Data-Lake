using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using SodickDataLake.Functions.Models;
using SodickDataLake.Models;
using UglyToad.PdfPig;

namespace SodickDataLake.Services;

public sealed class PdfJsonGeneratorService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly PdfJsonGeneratorOptions _options;

    public PdfJsonGeneratorService(
        BlobServiceClient blobServiceClient,
        PdfJsonGeneratorOptions options)
    {
        _blobServiceClient = blobServiceClient;
        _options = options;
    }

    public async Task<GeneratePdfJsonResponse> GenerateAsync(
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
            return new GeneratePdfJsonResponse
            {
                Success = false,
                SourcePath = sourcePath,
                Message = "Source PDF was not found."
            };
        }

        targetPath ??= BuildDefaultTargetPath(sourcePath);

        BlobDownloadStreamingResult download;
        try
        {
            download = await sourceBlob.DownloadStreamingAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            return new GeneratePdfJsonResponse
            {
                Success = false,
                SourcePath = sourcePath,
                TargetPath = targetPath,
                Message = $"Could not download source PDF: {ex.Message}"
            };
        }

        using var sourceStream = download.Content;
        using var memoryStream = new MemoryStream();
        await sourceStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        PdfDocumentJson jsonDocument;
        try
        {
            jsonDocument = ExtractPdf(memoryStream, Path.GetFileName(sourcePath));
        }
        catch (Exception ex)
        {
            return new GeneratePdfJsonResponse
            {
                Success = false,
                SourcePath = sourcePath,
                TargetPath = targetPath,
                Message = $"Could not extract text from PDF: {ex.Message}"
            };
        }

        var json = JsonSerializer.Serialize(jsonDocument, new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = true
        });

        var targetBlob = container.GetBlobClient(targetPath);

        using var jsonStream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        await targetBlob.UploadAsync(
            jsonStream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "application/json; charset=utf-8"
                }
            },
            cancellationToken);

        return new GeneratePdfJsonResponse
        {
            Success = true,
            SourcePath = sourcePath,
            TargetPath = targetPath,
            OutputBlobUrl = targetBlob.Uri.ToString(),
            Message = "JSON generated successfully.",
            PageCount = jsonDocument.PageCount
        };
    }

    private string BuildDefaultTargetPath(string sourcePath)
    {
        var normalized = sourcePath.Replace("\\", "/").TrimStart('/');

        if (normalized.StartsWith("raw/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = _options.DefaultOutputFolder + "/" + normalized.Substring(4);
        }
        else
        {
            normalized = _options.DefaultOutputFolder + "/" + normalized;
        }

        return Path.ChangeExtension(normalized, ".json")!.Replace("\\", "/");
    }

    private static PdfDocumentJson ExtractPdf(Stream pdfStream, string sourceFileName)
    {
        using var document = PdfDocument.Open(pdfStream);

        var result = new PdfDocumentJson
        {
            FileName = sourceFileName,
            PageCount = document.NumberOfPages,
            GeneratedAtUtc = DateTime.UtcNow
        };

        for (int pageNumber = 1; pageNumber <= document.NumberOfPages; pageNumber++)
        {
            var page = document.GetPage(pageNumber);

            result.Pages.Add(new PdfPageJson
            {
                PageNumber = pageNumber,
                Text = NormalizeText(page.Text)
            });
        }

        return result;
    }

    private static string NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return text
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Trim();
    }
}