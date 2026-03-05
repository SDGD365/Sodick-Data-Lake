using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
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
            var baseUrl = $"{req.Url.Scheme}://{req.Url.Host}{(req.Url.IsDefaultPort ? "" : ":" + req.Url.Port)}";
            var searchUrl = $"{baseUrl}/api/search?docId={ask.DocId}&version={ask.Version}&lang={lang}&q={Uri.EscapeDataString(ask.Question)}";

            using var http = new HttpClient();
            var searchJson = await http.GetStringAsync(searchUrl);
            var hits = JsonSerializer.Deserialize<List<SearchHit>>(searchJson) ?? new();

            // PoC answer (ohne LLM): kurze Zusammenfassung aus Top-Hits
            // Später ersetzt du das durch echtes RAG (Azure OpenAI).
            var answer = BuildPoCAnswer(ask.Question, hits);

            var citations = hits.Select(h => new Citation
            {
                Page = h.Page,
                Title = $"Seite {h.Page}",
                // Jump-to-page is client-side via #page=
                ViewerUrl = $"{baseUrl}/api/docs/{ask.DocId}/{ask.Version}/pdf#page={h.Page}",
                Snippet = h.Snippet
            }).ToList();

            var respObj = new AskResponse { Answer = answer, Citations = citations };
            var resp = req.CreateResponse(HttpStatusCode.OK);
            resp.Headers.Add("Content-Type", "application/json; charset=utf-8");
            resp.Headers.Add("x-poc-source", "ask-v1");
            await resp.WriteStringAsync(JsonSerializer.Serialize(respObj));
            return resp;
        }
        catch (Exception ex)
        {
            var resp = req.CreateResponse(HttpStatusCode.InternalServerError);
            await resp.WriteStringAsync(ex.ToString());
            return resp;
        }
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
        var parts = top.Select(h => $"- {h.Snippet} (Seite {h.Page})");
        return $"PoC-Antwort (aus gefundenen Stellen):\n\n{string.Join("\n", parts)}";
    }
}