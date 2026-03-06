using System.Net;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;

public sealed class DocsPdfFunction
{
    private readonly IConfiguration _cfg;
    public DocsPdfFunction(IConfiguration cfg) => _cfg = cfg;

    [Function("DocsPdf")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "docs/{docId}/{version}/pdf")] HttpRequestData req,
        string docId,
        string version)
    {
        try
        {
            // ADLS path convention for PoC:
            // raw/{docId}/{version}/manual.pdf
            var blobPath = $"raw/{docId}/{version}/manual.pdf";

            var storageUrl = _cfg["StorageAccountBlobUrl"] ?? throw new InvalidOperationException("Missing StorageAccountBlobUrl");
            var containerName = _cfg["BookContainer"] ?? throw new InvalidOperationException("Missing BookContainer");

            var blobService = new BlobServiceClient(new Uri(storageUrl), new DefaultAzureCredential());
            var container = blobService.GetBlobContainerClient(containerName);
            var blob = container.GetBlobClient(blobPath);

            if (!await blob.ExistsAsync())
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync($"PDF not found: {blobPath}");
                return notFound;
            }

            // For PoC: stream whole file (range requests can be added later)
            var download = await blob.DownloadStreamingAsync();

            var resp = req.CreateResponse(HttpStatusCode.OK);
            resp.Headers.Add("Content-Type", "application/pdf");
            resp.Headers.Add("Cache-Control", "private, max-age=60");

            // Wichtig für iframe embedding in D365:
            resp.Headers.Add(
              "Content-Security-Policy",
              "frame-ancestors " +
              "https://make.powerapps.com " +
              "https://*.powerapps.com " +
              "https://*.apps.powerapps.com " +
              "https://*.dynamics.com " +
              "https://*.crm*.dynamics.com;"
            );
            // Optional, hilft manchmal bei eingebetteten Ressourcen:
            resp.Headers.Add("Cross-Origin-Resource-Policy", "cross-origin");

            // Wichtig: NICHT setzen / vermeiden:
            // X-Frame-Options: SAMEORIGIN  (würde embedding killen)
            await download.Value.Content.CopyToAsync(resp.Body);
            return resp;
        }
        catch (Exception ex)
        {
            var resp = req.CreateResponse(HttpStatusCode.InternalServerError);
            await resp.WriteStringAsync(ex.ToString());
            return resp;
        }
    }
}