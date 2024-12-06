using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using OpenAI;
using Qdrant.Client;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Configuration.AddUserSecrets<Program>();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.AddQdrantClient("vectordb");
builder.Services.AddSingleton(new OpenAIClient(builder.Configuration.GetValue<string>("OpenAI_KEY")));

builder.Services.AddEmbeddingGenerator<string, Embedding<float>>(services =>
    services.GetRequiredService<OpenAIClient>().AsEmbeddingGenerator(modelId: "text-embedding-3-small"))
        .UseLogging()
        .UseOpenTelemetry();

// Register HttpClient
builder.Services.AddHttpClient();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Endpoint to echo back the string received in the body
app.MapPost("/embedding", async (EmbeddingRequest embeddingRequest, [FromServices] IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator) =>
{
    if (embeddingRequest == null || string.IsNullOrEmpty(embeddingRequest.input))
    {
        return Results.BadRequest("Invalid request");
    }

    var embedding = await embeddingGenerator.GenerateEmbeddingVectorAsync(embeddingRequest.input);

    var response = new { result = embedding };
    return Results.Json(response);
})
.WithName("GetEmbedding")
.WithOpenApi();

app.MapPost("/generatedb", async ([FromServices] IHttpClientFactory httpClientFactory, [FromServices] IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, [FromServices] QdrantClient qdrant) =>
{
    var tempDownloadPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    var tempExtractPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    try
    {
        // Download the zip file
        var httpClient = httpClientFactory.CreateClient();
        var zipData = await httpClient.GetByteArrayAsync("https://github.com/dotnet/docs-aspire/archive/refs/heads/main.zip");
        await File.WriteAllBytesAsync(tempDownloadPath, zipData);

        // Extract the zip file
        System.IO.Compression.ZipFile.ExtractToDirectory(tempDownloadPath, tempExtractPath);

        // Perform operations with the extracted files here
        var allMarkdowns = Directory.GetFiles(tempExtractPath, "*.md", SearchOption.AllDirectories);
        var store = new QdrantVectorStore(qdrant);
        var sections = store.GetCollection<ulong, Paragraph>("sections");
        var exists = await sections.CollectionExistsAsync();
        await sections.CreateCollectionIfNotExistsAsync();

        if (!exists)
        {
            ulong i = 0;
            foreach (var markdown in allMarkdowns)
            {
                var content = File.ReadAllText(markdown);
                var markdownSections = MyRegex().Split(content).Where(s => !string.IsNullOrEmpty(s));
                var docSections = markdownSections.Where(s => !string.IsNullOrEmpty(s)).Select(s => new Paragraph { Key = i++, ContainingDoc = markdown, Content = s, Vector = embeddingGenerator.GenerateEmbeddingVectorAsync(s).GetAwaiter().GetResult() });
                docSections.ToList().ForEach(async section => await sections.UpsertAsync(section));
            }
        }
    }
    finally
    {
        // Clean up temporary files
        if (Directory.Exists(tempExtractPath))
        {
            Directory.Delete(tempExtractPath, true);
        }

        if (File.Exists(tempDownloadPath))
        {
            File.Delete(tempDownloadPath);
        }
    }

    return Results.Ok("Download and extraction completed successfully.");
});

app.Run();

record EmbeddingRequest(string input);

class Paragraph
{
    [VectorStoreRecordKey]
    public ulong Key { get; set; }

    [VectorStoreRecordData]
    public string? ContainingDoc { get; set; }

    [VectorStoreRecordData]
    public string? Content { get; set; }

    [VectorStoreRecordVector(1536, DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float> Vector { get; set; }
}

partial class Program
{
    [GeneratedRegex(@"^##\s+(?=\w)", RegexOptions.Multiline)]
    private static partial Regex MyRegex();
}
