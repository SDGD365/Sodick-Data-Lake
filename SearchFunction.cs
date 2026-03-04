using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;

public sealed class SearchFunction
{
    private readonly IConfiguration _cfg;
    public SearchFunction(IConfiguration cfg) => _cfg = cfg;

    [Function("Search")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "search")] HttpRequestData req)
    {
        try
        {
            var q = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var docId = q["docId"] ?? "demo";
            var version = q["version"] ?? "v1";
            var query = q["q"] ?? "";
            var lang = (q["lang"] ?? "de").ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(query))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Missing q");
                return bad;
            }

            var chunksPath = $"processed/{docId}/{version}/chunks.jsonl";

            var storageUrl = _cfg["StorageAccountBlobUrl"]!;
            var containerName = _cfg["BookContainer"]!;
            var blobService = new BlobServiceClient(new Uri(storageUrl), new DefaultAzureCredential());
            var blob = blobService.GetBlobContainerClient(containerName).GetBlobClient(chunksPath);

            if (!await blob.ExistsAsync())
            {
                var nf = req.CreateResponse(HttpStatusCode.NotFound);
                await nf.WriteStringAsync($"chunks.jsonl not found: {chunksPath}");
                return nf;
            }

            var download = await blob.DownloadContentAsync();
            var text = download.Value.Content.ToString();

            var hits = new List<SearchHit>();
            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                Chunk? chunk;
                try { chunk = JsonSerializer.Deserialize<Chunk>(line); }
                catch { continue; }

                if (chunk == null) continue;
                if (!string.Equals(chunk.Lang, lang, StringComparison.OrdinalIgnoreCase)) continue;

                var score = ScoreContains(chunk.Text, query);
                if (score <= 0) continue;

                hits.Add(new SearchHit
                {
                    ChunkId = chunk.ChunkId,
                    Page = chunk.Page,
                    Score = score,
                    Snippet = MakeSnippet(chunk.Text, query, 240),
                    PdfPath = chunk.PdfPath
                });
            }

            var top = hits
                .OrderByDescending(h => h.Score)
                .ThenBy(h => h.Page)
                .Take(5)
                .ToList();

            var resp = req.CreateResponse(HttpStatusCode.OK);
            resp.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await resp.WriteStringAsync(JsonSerializer.Serialize(top));
            return resp;
        }
        catch (Exception ex)
        {
            var resp = req.CreateResponse(HttpStatusCode.InternalServerError);
            await resp.WriteStringAsync(ex.ToString());
            return resp;
        }
    }

    private static double ScoreContains(string text, string query)
    {
        var t = text.ToLowerInvariant();
        var q = query.ToLowerInvariant();

        // super simpel: count occurrences
        int count = 0, idx = 0;
        while ((idx = t.IndexOf(q, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += q.Length;
        }
        return count;
    }

    private static string MakeSnippet(string text, string query, int maxLen)
    {
        var idx = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return text.Length <= maxLen ? text : text[..maxLen] + "…";

        var start = Math.Max(0, idx - 80);
        var end = Math.Min(text.Length, start + maxLen);
        var snippet = text[start..end];
        if (start > 0) snippet = "…" + snippet;
        if (end < text.Length) snippet += "…";
        return snippet;
    }
}