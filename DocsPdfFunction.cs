using System.Net;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;

public sealed class DocsPdfFunction
{
    private readonly IConfiguration _cfg;

    public DocsPdfFunction(IConfiguration cfg)
    {
        _cfg = cfg;
    }

    [Function("DocsPdf")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "docs/{docId}/{version}/pdf")] HttpRequestData req,
        string docId,
        string version)
    {
        // Für den Test erst einmal großzügig:
        const string allowOrigin = "*";

        try
        {
            // OPTIONS / Preflight
            if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                var optionsResp = req.CreateResponse(HttpStatusCode.NoContent);
                ApplyCorsHeaders(optionsResp, allowOrigin);
                return optionsResp;
            }

            var blobPath = $"raw/demo/{version}/{docId}.pdf";

            var storageUrl = _cfg["StorageAccountBlobUrl"]
                ?? throw new InvalidOperationException("Missing StorageAccountBlobUrl");

            var containerName = _cfg["BookContainer"]
                ?? throw new InvalidOperationException("Missing BookContainer");

            var blobService = new BlobServiceClient(new Uri(storageUrl), new DefaultAzureCredential());
            var container = blobService.GetBlobContainerClient(containerName);
            var blob = container.GetBlobClient(blobPath);

            if (!await blob.ExistsAsync())
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                ApplyCorsHeaders(notFound, allowOrigin);
                await notFound.WriteStringAsync($"PDF not found: {blobPath}");
                return notFound;
            }

            var download = await blob.DownloadStreamingAsync();

            var resp = req.CreateResponse(HttpStatusCode.OK);
            ApplyCorsHeaders(resp, allowOrigin);

            resp.Headers.Add("Content-Type", "application/pdf");
            resp.Headers.Add("Content-Disposition", $"inline; filename=\"{docId}.pdf\"");
            resp.Headers.Add("Cache-Control", "private, max-age=60");
            resp.Headers.Add("Accept-Ranges", "bytes");
            resp.Headers.Add("Access-Control-Expose-Headers", "Accept-Ranges, Content-Length, Content-Range");

            // Für Embedding in Power Apps / D365
            resp.Headers.Add(
                "Content-Security-Policy",
                "frame-ancestors " +
                "https://make.powerapps.com " +
                "https://*.powerapps.com " +
                "https://*.apps.powerapps.com " +
                "https://*.dynamics.com " +
                "https://*.crm*.dynamics.com;"
            );

            resp.Headers.Add("Cross-Origin-Resource-Policy", "cross-origin");

            await download.Value.Content.CopyToAsync(resp.Body);
            return resp;
        }
        catch (Exception ex)
        {
            var errorResp = req.CreateResponse(HttpStatusCode.InternalServerError);
            ApplyCorsHeaders(errorResp, allowOrigin);
            await errorResp.WriteStringAsync(ex.ToString());
            return errorResp;
        }
    }

    private static void ApplyCorsHeaders(HttpResponseData resp, string allowOrigin)
    {
        resp.Headers.Add("Access-Control-Allow-Origin", allowOrigin);
        resp.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
        resp.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Range");
    }
}