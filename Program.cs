using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
  .ConfigureFunctionsWebApplication()
  .ConfigureServices(services =>
  {
      services.Configure<JsonSerializerOptions>(o =>
      {
          o.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
          o.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
          o.PropertyNameCaseInsensitive = true;
      });
  })
  .Build();

host.Run();