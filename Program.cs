using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SodickDataLake.Functions.Services;
using SodickDataLake.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
.ConfigureServices(services =>
{
    var connectionString = Environment.GetEnvironmentVariable("PdfSourceStorageConnectionString")
                          ?? throw new InvalidOperationException("PdfSourceStorageConnectionString is missing.");

    var containerName = Environment.GetEnvironmentVariable("PdfSourceContainer") ?? "books";
    var outputFolder = Environment.GetEnvironmentVariable("PdfJsonDefaultOutputFolder") ?? "processed";

    services.AddSingleton(new BlobServiceClient(connectionString));

    services.AddSingleton(new PdfJsonGeneratorOptions
    {
        SourceContainerName = containerName,
        DefaultOutputFolder = outputFolder
    });

    services.AddSingleton<PdfJsonGeneratorService>();
    services.AddSingleton<SearchJsonGeneratorService>();
    services.AddSingleton<ManualSearchService>();
})
    .Build();

host.Run();