using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using SodickMastermind.Services;

namespace SodickMastermind;

public sealed class GeneratePdfJsonFunction
{
    private readonly PdfJsonGeneratorService _service;

    public GeneratePdfJsonFunction(PdfJsonGeneratorService service)
    {
        _service = service;
    }

    [Function("GeneratePdfJson")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "GeneratePdfJson")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var sourcePath = query["sourcePath"];
        var targetPath = query["targetPath"];

        if (string.IsNullOrWhiteSpace(sourcePath) && req.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(body))
            {
                var payload = JsonSerializer.Deserialize<RequestPayload>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                sourcePath ??= payload?.SourcePath;
                targetPath ??= payload?.TargetPath;
            }
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("Please provide sourcePath.", cancellationToken);
            return badRequest;
        }

        var result = await _service.GenerateAsync(sourcePath, targetPath, cancellationToken);

        var response = req.CreateResponse(result.Success ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");

        await response.WriteStringAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = true
        }), cancellationToken);

        return response;
    }

    private sealed class RequestPayload
    {
        public string? SourcePath { get; set; }
        public string? TargetPath { get; set; }
    }
}
