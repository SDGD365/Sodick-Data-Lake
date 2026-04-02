using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SodickDataLake.Models;
using System.Net;
using System.Text.Json;

public sealed class AskFunction
{
    private readonly ILogger<AskFunction> _logger;

    public AskFunction(ILogger<AskFunction> logger)
    {
        _logger = logger;
    }

    [Function("Ask")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "qa/ask")] HttpRequestData req)
    {
        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation("Ask raw body: {Body}", body);
            var ask = JsonSerializer.Deserialize<AskRequest>(
                body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (ask == null || string.IsNullOrWhiteSpace(ask.Question))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync($"BadRequest. Raw body was: {body}");
                return bad;
            }

            var lang = DetectLang(ask.Question, ask.Lang);

            // Call our own search endpoint (internal URL is simplest for PoC)
            // In production you'd call Search as a method or move logic to a shared service.
            var baseUrl = Environment.GetEnvironmentVariable("PublicBaseUrl") ?? $"{req.Url.Scheme}://{req.Url.Host}{(req.Url.IsDefaultPort ? "" : ":" + req.Url.Port)}";
            var searchUrl = $"{baseUrl}/api/search?docId={ask.DocId}&version={ask.Version}&lang={lang}&q={Uri.EscapeDataString(ask.Question)}";

            using var http = new HttpClient();

            var searchKey = Environment.GetEnvironmentVariable("SearchFunctionKey");

            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, searchUrl);

            if (!string.IsNullOrWhiteSpace(searchKey))
            {
                requestMessage.Headers.Add("x-functions-key", searchKey);
            }

            using var searchResponseMessage = await http.SendAsync(requestMessage);
            searchResponseMessage.EnsureSuccessStatusCode();

            var searchJson = await searchResponseMessage.Content.ReadAsStringAsync(); var searchResponse = JsonSerializer.Deserialize<SearchApiResponse>(searchJson) ?? new();

            var hits = searchResponse.Results ?? new List<SearchHit>();

            // PoC answer (ohne LLM): kurze Zusammenfassung aus Top-Hits
            // Später ersetzt du das durch echtes RAG (Azure OpenAI).
            var answer = BuildPoCAnswer(ask.Question, hits);

            var citations = hits.Take(3).Select(h => new Citation
            {
                Page = h.PageNumber,
                Title = h.FileName,
                ViewerUrl = BuildViewerUrl(baseUrl, h.PdfPath, h.PageNumber),
                Snippet = h.Snippet
            }).ToList();

            var responseObj = new
            {
                Answer = answer,
                Citations = citations, // bleibt für Connector-Test/Swagger gut
                CitationsJson = JsonSerializer.Serialize(citations),
            };

            var resp = req.CreateResponse(HttpStatusCode.OK);

            resp.Headers.Add("Access-Control-Allow-Origin", "*");
            resp.Headers.Add(
              "Content-Security-Policy",
              "frame-ancestors " +
              "http://localhost:8181 " +
              "https://make.powerapps.com " +
              "https://*.powerapps.com " +
              "https://*.apps.powerapps.com " +
              "https://*.dynamics.com " +
              "https://*.crm*.dynamics.com;"
            ); resp.Headers.Add("Content-Disposition", "inline");
            // Optional, hilft manchmal bei eingebetteten Ressourcen:
            resp.Headers.Add("Cross-Origin-Resource-Policy", "cross-origin");

            await resp.WriteAsJsonAsync(responseObj);

            return resp;
        }
        catch (Exception ex)
        {
            var resp = req.CreateResponse(HttpStatusCode.InternalServerError);
            await resp.WriteStringAsync(ex.ToString());
            return resp;
        }
    }
    private static string BuildViewerUrl(string baseUrl, string pdfPath, int pageNumber)
    {
        var parts = pdfPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4)
            throw new InvalidOperationException($"Unexpected pdfPath format: {pdfPath}");

        var docId = Path.GetFileNameWithoutExtension(parts[^1]);
        var version = parts[^2];

        return
            $"{baseUrl}/api/viewer/" +
            $"{Uri.EscapeDataString(docId)}/{Uri.EscapeDataString(version)}?page={pageNumber}";
    }
    private static string DetectLang(string question, string lang)
    {
        lang = (lang ?? "auto").ToLowerInvariant();
        if (lang is "de" or "en") return lang;

        // Heuristik: Umlaute + typische Wörter
        var q = question.ToLowerInvariant();
        if (q.Contains('ä') || q.Contains('ö') || q.Contains('ü') || q.Contains(" der ") || q.Contains(" die ") || q.Contains(" das "))
            return "de";

        return "en";
    }

    private static string BuildPoCAnswer(string question, List<SearchHit> hits)
    {
        if (hits.Count == 0)
            return "Ich habe im Handbuch dazu keine passende Stelle gefunden (PoC-Suche).";

        var top = hits.Take(3).ToList();
        var parts = top.Select(h => $"- {h.Snippet} (Seite {h.PageNumber})");
        return $"PoC-Antwort (aus gefundenen Stellen):\n\n{string.Join("\n", parts)}";
    }
}