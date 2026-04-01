using Azure.Storage.Blobs;
using SodickDataLake.Functions.Models;
using SodickDataLake.Models;
using SodickDataLake.Services;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SodickDataLake.Services;

public sealed class ManualSearchService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly PdfJsonGeneratorOptions _options;

    public ManualSearchService(
        BlobServiceClient blobServiceClient,
        PdfJsonGeneratorOptions options)
    {
        _blobServiceClient = blobServiceClient;
        _options = options;
    }

    public async Task<SearchApiResponse> SearchAsync(
        string query,
        string searchJsonPath,
        string pdfPath,
        int top = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new SearchApiResponse
            {
                Answer = string.Empty,
                Results = new List<SearchHit>()
            };
        }

        var container = _blobServiceClient.GetBlobContainerClient(_options.SourceContainerName);
        var blob = container.GetBlobClient(searchJsonPath);

        if (!await blob.ExistsAsync(cancellationToken))
        {
            return new SearchApiResponse
            {
                Answer = $"Search JSON not found: {searchJsonPath}"
            };
        }

        List<ManualSearchItem>? items;
        var download = await blob.DownloadContentAsync(cancellationToken);
        var json = download.Value.Content.ToString();

        items = JsonSerializer.Deserialize<List<ManualSearchItem>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (items is null || items.Count == 0)
        {
            return new SearchApiResponse
            {
                Answer = "No search items found."
            };
        }

        var normalizedQuery = Normalize(query);
        var queryTerms = SplitTerms(normalizedQuery);

        var hits = items
            .Select(item => new
            {
                Item = item,
                Score = Score(item.SearchText, queryTerms)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Item.PageNumber)
            .Take(Math.Max(1, top))
            .Select(x => new SearchHit
            {
                FileName = x.Item.FileName,
                PageNumber = x.Item.PageNumber,
                Snippet = BuildSnippet(x.Item.Text, queryTerms),
                Score = x.Score,
                PdfPath = pdfPath,
                JsonPath = searchJsonPath
            })
            .ToList();

        return new SearchApiResponse
        {
            Answer = string.Empty,
            Results = hits
        };
    }

    private static string Normalize(string text)
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

    private static readonly HashSet<string> AllowedShortTerms = ["lan","nc","md","hi"];

    private static List<string> SplitTerms(string normalizedQuery)
    {
        return normalizedQuery
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length >= 4 || AllowedShortTerms.Contains(t))
            .Distinct()
            .ToList();
    }

    private static HashSet<string> Tokenize(string normalizedText)
    {
        return normalizedText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length >= 2)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static int Score(string searchText, List<string> queryTerms)
    {
        if (string.IsNullOrWhiteSpace(searchText) || queryTerms.Count == 0)
            return 0;

        var tokens = Tokenize(searchText);
        var matchedTerms = 0;

        foreach (var term in queryTerms)
        {
            if (tokens.Contains(term))
            {
                matchedTerms++;
            }
        }

        if (matchedTerms == 0)
            return 0;

        var score = matchedTerms * 2;

        var fullPhrase = string.Join(" ", queryTerms);
        if (searchText.Contains(fullPhrase, StringComparison.OrdinalIgnoreCase))
        {
            score += 3;
        }

        return score;
    }
    private static string BuildSnippet(string text, List<string> queryTerms)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var compact = Regex.Replace(text, @"\s+", " ").Trim();

        foreach (var term in queryTerms)
        {
            var idx = compact.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var start = Math.Max(0, idx - 80);
                var length = Math.Min(240, compact.Length - start);
                var snippet = compact.Substring(start, length);

                if (start > 0) snippet = "..." + snippet;
                if (start + length < compact.Length) snippet += "...";

                return snippet;
            }
        }

        return compact.Length <= 240 ? compact : compact[..240] + "...";
    }
}