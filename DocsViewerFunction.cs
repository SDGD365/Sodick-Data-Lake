using System.Net;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

public sealed class DocsViewerFunction
{
    [Function("DocsViewer")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "viewer/{docId}/{version}")] HttpRequestData req,
        string docId,
        string version)
    {
        const string allowOrigin = "*";

        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var optionsResp = req.CreateResponse(HttpStatusCode.NoContent);
            ApplyCorsHeaders(optionsResp, allowOrigin);
            return optionsResp;
        }

        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var pageText = query["page"];
        var page = int.TryParse(pageText, out var p) && p > 0 ? p : 1;

        var baseUrl = $"{req.Url.Scheme}://{req.Url.Host}";
        if (!req.Url.IsDefaultPort)
        {
            baseUrl += $":{req.Url.Port}";
        }

        var pdfUrl =
            $"{baseUrl}/api/docs/{Uri.EscapeDataString(docId)}/{Uri.EscapeDataString(version)}/pdf#page={page}";

        var html = $$"""
<!doctype html>
<html lang="de">
<head>
  <meta charset="utf-8" />
  <meta http-equiv="X-UA-Compatible" content="IE=edge" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>PDF Viewer</title>
  <style>
    html, body {
      margin: 0;
      padding: 0;
      width: 100%;
      height: 100%;
      overflow: hidden;
      background: #f3f2f1;
      font-family: Segoe UI, Arial, sans-serif;
    }

    .shell {
      display: flex;
      flex-direction: column;
      width: 100%;
      height: 100%;
    }

    .topbar {
      flex: 0 0 auto;
      padding: 8px 12px;
      font-size: 12px;
      color: #444;
      background: #ffffff;
      border-bottom: 1px solid #ddd;
    }

    .viewer {
      flex: 1 1 auto;
      width: 100%;
      height: 100%;
      border: 0;
      background: #fff;
    }

    .hint {
      color: #666;
    }
  </style>
</head>
<body>
  <div class="shell">
    <div class="topbar">
      Dokument: <strong>{{System.Net.WebUtility.HtmlEncode(docId)}}</strong>,
      Version: <strong>{{System.Net.WebUtility.HtmlEncode(version)}}</strong>,
      Seite: <strong>{{page}}</strong>
      <span class="hint">– Falls nichts angezeigt wird, bitte die PDF-URL direkt testen.</span>
    </div>

    <iframe
      class="viewer"
      src="{{System.Net.WebUtility.HtmlEncode(pdfUrl)}}"
      allow="fullscreen"
      referrerpolicy="no-referrer">
    </iframe>
  </div>
</body>
</html>
""";

        var resp = req.CreateResponse(HttpStatusCode.OK);
        ApplyCorsHeaders(resp, allowOrigin);
        resp.Headers.Add("Content-Type", "text/html; charset=utf-8");
        resp.Headers.Add("Cache-Control", "no-store");
        resp.Headers.Add(
            "Content-Security-Policy",
            "default-src 'self' https: data: blob:; " +
            "frame-src 'self' https: blob:; " +
            "style-src 'self' 'unsafe-inline' https:; " +
            "img-src 'self' data: blob: https:; " +
            "frame-ancestors " +
            "https://make.powerapps.com " +
            "https://*.powerapps.com " +
            "https://*.apps.powerapps.com " +
            "https://*.dynamics.com " +
            "https://*.crm*.dynamics.com;"
        );
        resp.Headers.Add("Cross-Origin-Resource-Policy", "cross-origin");

        await resp.WriteStringAsync(html, Encoding.UTF8);
        return resp;
    }

    private static void ApplyCorsHeaders(HttpResponseData resp, string allowOrigin)
    {
        resp.Headers.Add("Access-Control-Allow-Origin", allowOrigin);
        resp.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
        resp.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Range");
    }
}