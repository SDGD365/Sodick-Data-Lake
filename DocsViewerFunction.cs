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
        var page = int.TryParse(pageText, out var parsedPage) && parsedPage > 0 ? parsedPage : 1;

        var baseUrl = $"{req.Url.Scheme}://{req.Url.Host}";
        if (!req.Url.IsDefaultPort)
        {
            baseUrl += $":{req.Url.Port}";
        }

        var pdfUrl =
            $"{baseUrl}/api/docs/{Uri.EscapeDataString(docId)}/{Uri.EscapeDataString(version)}/pdf";

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
      background: #f3f2f1;
      font-family: Segoe UI, Arial, sans-serif;
      overflow: hidden;
    }

    .shell {
      display: flex;
      flex-direction: column;
      width: 100%;
      height: 100%;
    }

    .topbar {
      flex: 0 0 auto;
      display: flex;
      gap: 12px;
      align-items: center;
      padding: 8px 12px;
      font-size: 12px;
      color: #444;
      background: #fff;
      border-bottom: 1px solid #ddd;
    }

    .viewer-host {
      flex: 1 1 auto;
      overflow: auto;
      padding: 16px;
      box-sizing: border-box;
    }

    .page-wrap {
      display: flex;
      justify-content: center;
    }

    canvas {
      background: white;
      box-shadow: 0 2px 10px rgba(0,0,0,.12);
      max-width: 100%;
      height: auto;
    }

    .muted {
      color: #666;
    }

    .error {
      color: #b00020;
      white-space: pre-wrap;
      padding: 12px;
      background: #fff;
      border: 1px solid #f1b5b5;
    }

    button {
      padding: 4px 10px;
      border: 1px solid #ccc;
      background: #fff;
      cursor: pointer;
    }

    button:disabled {
      opacity: .5;
      cursor: default;
    }
  </style>

<script src="https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.min.js"></script></head>
<body>
  <div class="shell">
    <div class="topbar">
      <strong>{{System.Net.WebUtility.HtmlEncode(docId)}}</strong>
      <span>Version: <strong>{{System.Net.WebUtility.HtmlEncode(version)}}</strong></span>
      <button id="prevBtn">Zurück</button>
      <button id="nextBtn">Weiter</button>
      <span>Seite <strong id="pageNum">{{page}}</strong> / <strong id="pageCount">?</strong></span>
      <span class="muted" id="status">Lade PDF...</span>
    </div>

    <div class="viewer-host">
      <div class="page-wrap">
        <canvas id="pdfCanvas"></canvas>
      </div>
      <div id="errorBox" class="error" style="display:none;"></div>
    </div>
  </div>

  <script>
pdfjsLib.GlobalWorkerOptions.workerSrc =
"https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.worker.min.js";

    const pdfUrl = {{System.Text.Json.JsonSerializer.Serialize(pdfUrl)}};
    let pageNum = {{page}};
    let pdfDoc = null;
    let rendering = false;
    let pendingPage = null;

    const canvas = document.getElementById("pdfCanvas");
    const ctx = canvas.getContext("2d");
    const pageNumEl = document.getElementById("pageNum");
    const pageCountEl = document.getElementById("pageCount");
    const statusEl = document.getElementById("status");
    const errorBox = document.getElementById("errorBox");
    const prevBtn = document.getElementById("prevBtn");
    const nextBtn = document.getElementById("nextBtn");

    function showError(message) {
      errorBox.style.display = "block";
      errorBox.textContent = message;
      statusEl.textContent = "Fehler";
    }

    function updateButtons() {
      prevBtn.disabled = !pdfDoc || pageNum <= 1;
      nextBtn.disabled = !pdfDoc || pageNum >= pdfDoc.numPages;
    }

    async function renderPage(num) {
      rendering = true;
      statusEl.textContent = "Render Seite " + num + "...";

      try {
        const page = await pdfDoc.getPage(num);

        const unscaledViewport = page.getViewport({ scale: 1.0 });
        const hostWidth = Math.max(document.querySelector(".viewer-host").clientWidth - 40, 300);
        const scale = hostWidth / unscaledViewport.width;
        const viewport = page.getViewport({ scale });

        canvas.width = Math.floor(viewport.width);
        canvas.height = Math.floor(viewport.height);

        await page.render({
          canvasContext: ctx,
          viewport: viewport
        }).promise;

        pageNumEl.textContent = String(num);
        pageCountEl.textContent = String(pdfDoc.numPages);
        statusEl.textContent = "Bereit";
        updateButtons();
      } catch (err) {
        showError("Fehler beim Rendern der Seite:\\n" + (err?.stack || err));
      } finally {
        rendering = false;
        if (pendingPage !== null) {
          const next = pendingPage;
          pendingPage = null;
          renderPage(next);
        }
      }
    }

    function queueRenderPage(num) {
      if (rendering) {
        pendingPage = num;
      } else {
        renderPage(num);
      }
    }

    function goToPage(num) {
      if (!pdfDoc) return;
      if (num < 1 || num > pdfDoc.numPages) return;
      pageNum = num;
      queueRenderPage(pageNum);
    }

    prevBtn.addEventListener("click", () => goToPage(pageNum - 1));
    nextBtn.addEventListener("click", () => goToPage(pageNum + 1));

    window.addEventListener("resize", () => {
      if (pdfDoc) {
        queueRenderPage(pageNum);
      }
    });

    (async function init() {
      try {
        statusEl.textContent = "Lade Dokument...";
        const loadingTask = pdfjsLib.getDocument({
          url: pdfUrl,
          withCredentials: false
        });

        pdfDoc = await loadingTask.promise;

        if (pageNum > pdfDoc.numPages) {
          pageNum = pdfDoc.numPages;
        }
        if (pageNum < 1) {
          pageNum = 1;
        }

        pageCountEl.textContent = String(pdfDoc.numPages);
        updateButtons();
        await renderPage(pageNum);
      } catch (err) {
        showError("Fehler beim Laden der PDF:\\n" + (err?.stack || err));
      }
    })();
  </script>
</body>
</html>
""";

        var resp = req.CreateResponse(HttpStatusCode.OK);
        ApplyCorsHeaders(resp, allowOrigin);
        resp.Headers.Add("Content-Type", "text/html; charset=utf-8");
        resp.Headers.Add("Cache-Control", "no-store");
        resp.Headers.Add(
            "Content-Security-Policy",
            "default-src 'self' https: data: blob: 'unsafe-inline'; " +
            "script-src 'self' https: 'unsafe-inline'; " +
            "style-src 'self' 'unsafe-inline' https:; " +
            "img-src 'self' data: blob: https:; " +
            "connect-src 'self' https:; " +
            "worker-src 'self' blob: https:; " +
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