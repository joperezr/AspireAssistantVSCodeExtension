using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

var builder = DistributedApplication.CreateBuilder(args);

// Register HttpClient in the DI container
builder.Services.AddHttpClient();

var key = builder.AddParameter("apikey", secret: true);

var vectorDB = builder.AddQdrant("vectordb", apiKey: key, grpcPort: 6334, httpPort: 6333)
    .WithDataBindMount("./qdrant_data")
    .WithLifetime(ContainerLifetime.Persistent);

var rb = builder.AddProject<Projects.EmbeddingApi>("embeddingapi")
    .WaitFor(vectorDB)
    .WithReference(vectorDB);

rb.WithCommand(
        name: "initialize-db",
        displayName: "Initialize Vector DB",
        executeCommand: context => OnInitializeVectorDBAsync(rb, context),
        updateState: OnUpdateResourceState,
        iconName: "AnimalRabbitOff",
        iconVariant: IconVariant.Filled);


builder.Build().Run();
ResourceCommandState OnUpdateResourceState(UpdateCommandStateContext context)
{
    var logger = context.ServiceProvider.GetRequiredService<ILogger<Program>>();

    if (logger.IsEnabled(LogLevel.Information))
    {
        logger.LogInformation(
            "Initializing Vector Database: {ResourceSnapshot}",
            context.ResourceSnapshot);
    }

    return context.ResourceSnapshot.HealthStatus is HealthStatus.Healthy
        ? ResourceCommandState.Enabled
        : ResourceCommandState.Disabled;
}

async Task<ExecuteCommandResult> OnInitializeVectorDBAsync(IResourceBuilder<ProjectResource> rb, ExecuteCommandContext context)
{
    var connectionString = rb.GetEndpoint("https").Url;

    // Resolve IHttpClientFactory from the service provider
    var httpClientFactory = context.ServiceProvider.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient();

    var response = await httpClient.PostAsync($"{connectionString}/generatedb", null);
    response.EnsureSuccessStatusCode();

    return CommandResults.Success();
}
