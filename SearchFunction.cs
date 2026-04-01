using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using SodickDataLake.Functions.Services;
using SodickDataLake.Services;
using System.Net;
using System.Text.Json;

namespace SodickDataLake;

public sealed class SearchFunction
{
    private readonly ManualSearchService _searchService;

    public SearchFunction(ManualSearchService searchService)
    {
        _searchService = searchService;
    }

    [Function("Search")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "Search")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);

        var q = query["q"] ?? query["query"];
        var searchJsonPath = query["searchJsonPath"] ?? "processed/demo/v1/manual.search.json";
        var pdfPath = query["pdfPath"] ?? "raw/demo/v1/manual.pdf";
        var top = int.TryParse(query["top"], out var parsedTop) ? parsedTop : 10;

        if (string.IsNullOrWhiteSpace(q) && req.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(body))
            {
                var payload = JsonSerializer.Deserialize<SearchRequestPayload>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                q ??= payload?.Query;
                searchJsonPath = payload?.SearchJsonPath ?? searchJsonPath;
                pdfPath = payload?.PdfPath ?? pdfPath;
                top = payload?.Top ?? top;
            }
        }

        if (string.IsNullOrWhiteSpace(q))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("Please provide q or query.", cancellationToken);
            return badRequest;
        }

        var result = await _searchService.SearchAsync(q, searchJsonPath, pdfPath, top, cancellationToken);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");

        await response.WriteStringAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = true
        }), cancellationToken);

        return response;
    }

    private sealed class SearchRequestPayload
    {
        public string? Query { get; set; }
        public string? SearchJsonPath { get; set; }
        public string? PdfPath { get; set; }
        public int? Top { get; set; }
    }
}