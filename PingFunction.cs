using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

public sealed class PingFunction
{
    private readonly ILogger<AskFunction> _logger;

    public PingFunction(ILogger<AskFunction> logger)
    {
        _logger = logger;
    }

    [Function("Ping")]
    public static HttpResponseData Run(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ping")] HttpRequestData req)
    {
        var resp = req.CreateResponse(HttpStatusCode.OK);
        resp.Headers.Add("Content-Type", "application/json");
        resp.WriteString("{\"ok\":true}");
        return resp;
    } 
}