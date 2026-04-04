using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;
using Srodkowy.Functions.Configuration;
using Srodkowy.Functions.Persistence;
using Srodkowy.Functions.Services;
using Srodkowy.Functions.Services.Ai;

var builder = FunctionsApplication.CreateBuilder(args);
var firecrawlTimeoutSeconds = GetIntSetting(builder.Configuration, "Firecrawl:TimeoutSeconds", 60);
var firecrawlRequestsPerMinute = GetIntSetting(builder.Configuration, "Firecrawl:RequestsPerMinute", FirecrawlOptions.DefaultRequestsPerMinute);

builder.ConfigureFunctionsWebApplication();

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource(
            ObservabilityOptions.ArticlePreparationSourceName,
            ObservabilityOptions.ClusteringSourceName,
            ObservabilityOptions.CleanupChatSourceName,
            ObservabilityOptions.ChatSourceName,
            ObservabilityOptions.EmbeddingSourceName);
        tracing.AddHttpClientInstrumentation();
        tracing.AddSqlClientInstrumentation();
    })
    .UseFunctionsWorkerDefaults()
    .UseAzureMonitorExporter(options =>
    {
        var authString = builder.Configuration["APPLICATIONINSIGHTS_AUTHENTICATION_STRING"];
        if (TryExtractClientId(authString, out var clientId))
        {
            options.Credential = new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(clientId));
        }
    });

builder.Services
    .AddOptions<FirecrawlOptions>()
    .Configure(options =>
    {
        options.ApiKey = GetSetting(builder.Configuration, "Firecrawl:ApiKey") ?? string.Empty;
        options.BaseUrl = GetSetting(builder.Configuration, "Firecrawl:BaseUrl") ?? FirecrawlOptions.DefaultBaseUrl;
        options.TimeoutSeconds = firecrawlTimeoutSeconds;
        options.RequestsPerMinute = firecrawlRequestsPerMinute;
    });

builder.Services
    .AddOptions<IngestionOptions>()
    .Configure(options =>
    {
        if (int.TryParse(GetSetting(builder.Configuration, "Ingestion:MaxCandidateLinksPerSource"), out var maxCandidateLinksPerSource))
        {
            options.MaxCandidateLinksPerSource = maxCandidateLinksPerSource;
        }

        if (int.TryParse(GetSetting(builder.Configuration, "Ingestion:MaxArticlesPerSource"), out var maxArticlesPerSource))
        {
            options.MaxArticlesPerSource = maxArticlesPerSource;
        }

        if (int.TryParse(GetSetting(builder.Configuration, "Ingestion:MinCandidateTitleLength"), out var minCandidateTitleLength))
        {
            options.MinCandidateTitleLength = minCandidateTitleLength;
        }

    });

builder.Services
    .AddOptions<OpenAiOptions>()
    .Configure(options =>
    {
        options.ApiKey = GetSetting(builder.Configuration, "OpenAi:ApiKey") ?? string.Empty;
        options.CleanupModel = GetSetting(builder.Configuration, "OpenAi:CleanupModel") ?? OpenAiOptions.DefaultCleanupModel;
        options.ChatModel = GetSetting(builder.Configuration, "OpenAi:ChatModel") ?? OpenAiOptions.DefaultChatModel;
        options.EmbeddingModel = GetSetting(builder.Configuration, "OpenAi:EmbeddingModel") ?? OpenAiOptions.DefaultEmbeddingModel;
    });

builder.Services
    .AddOptions<CleanupOptions>()
    .Configure(options =>
    {
        if (int.TryParse(GetSetting(builder.Configuration, "Cleanup:BatchSize"), out var batchSize))
        {
            options.BatchSize = batchSize;
        }

        if (int.TryParse(GetSetting(builder.Configuration, "Cleanup:LookbackHours"), out var lookbackHours))
        {
            options.LookbackHours = lookbackHours;
        }

        if (int.TryParse(GetSetting(builder.Configuration, "Cleanup:MaxInputCharacters"), out var maxInputCharacters))
        {
            options.MaxInputCharacters = maxInputCharacters;
        }

        if (int.TryParse(GetSetting(builder.Configuration, "Cleanup:MinCleanedLength"), out var minCleanedLength))
        {
            options.MinCleanedLength = minCleanedLength;
        }

    });

builder.Services
    .AddOptions<EmbeddingOptions>()
    .Configure(options =>
    {
        if (int.TryParse(GetSetting(builder.Configuration, "Embedding:BatchSize"), out var batchSize))
        {
            options.BatchSize = batchSize;
        }

        if (int.TryParse(GetSetting(builder.Configuration, "Embedding:LookbackHours"), out var lookbackHours))
        {
            options.LookbackHours = lookbackHours;
        }

        if (int.TryParse(GetSetting(builder.Configuration, "Embedding:MaxInputCharacters"), out var maxInputCharacters))
        {
            options.MaxInputCharacters = maxInputCharacters;
        }
    });

builder.Services
    .AddOptions<ClusteringOptions>()
    .Configure(options =>
    {
        if (int.TryParse(GetSetting(builder.Configuration, "Clustering:LookbackHours"), out var lookbackHours))
        {
            options.LookbackHours = lookbackHours;
        }

        if (int.TryParse(GetSetting(builder.Configuration, "Clustering:MinQualityScore"), out var minQualityScore))
        {
            options.MinQualityScore = minQualityScore;
        }

        if (TryParseDouble(GetSetting(builder.Configuration, "Clustering:NearDuplicateSimilarity"), out var nearDuplicateSimilarity))
        {
            options.NearDuplicateSimilarity = nearDuplicateSimilarity;
        }

        if (TryParseDouble(GetSetting(builder.Configuration, "Clustering:PairSimilarityThreshold"), out var pairSimilarityThreshold))
        {
            options.PairSimilarityThreshold = pairSimilarityThreshold;
        }

        if (TryParseDouble(GetSetting(builder.Configuration, "Clustering:MergeSimilarityThreshold"), out var mergeSimilarityThreshold))
        {
            options.MergeSimilarityThreshold = mergeSimilarityThreshold;
        }

        if (int.TryParse(GetSetting(builder.Configuration, "Clustering:MaxPairTimespanHours"), out var maxPairTimespanHours))
        {
            options.MaxPairTimespanHours = maxPairTimespanHours;
        }

        if (int.TryParse(GetSetting(builder.Configuration, "Clustering:MaxClusterTimespanHours"), out var maxClusterTimespanHours))
        {
            options.MaxClusterTimespanHours = maxClusterTimespanHours;
        }

        if (int.TryParse(GetSetting(builder.Configuration, "Clustering:MaxClusterSize"), out var maxClusterSize))
        {
            options.MaxClusterSize = maxClusterSize;
        }

        if (int.TryParse(GetSetting(builder.Configuration, "Clustering:MaxClusters"), out var maxClusters))
        {
            options.MaxClusters = maxClusters;
        }

        if (bool.TryParse(GetSetting(builder.Configuration, "Clustering:ExcludeNeedsReview"), out var excludeNeedsReview))
        {
            options.ExcludeNeedsReview = excludeNeedsReview;
        }
    });

builder.Services
    .AddOptions<ObservabilityOptions>()
    .Configure(options =>
    {
        if (bool.TryParse(GetSetting(builder.Configuration, "Observability:EnableSensitiveData"), out var enableSensitiveData))
        {
            options.EnableSensitiveData = enableSensitiveData;
        }
    });

builder.Services.AddDbContextFactory<SrodkowyDbContext>(options =>
    SqlDbContextOptions.Configure(options, GetRequiredSetting(builder.Configuration, "Database:ConnectionString")));

builder.Services
    .AddHttpClient<FirecrawlClient>((serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<FirecrawlOptions>>().Value;

        client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = Timeout.InfiniteTimeSpan;
    })
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 1;
        options.Retry.ShouldHandle = static _ => ValueTask.FromResult(false);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(firecrawlTimeoutSeconds);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds((firecrawlTimeoutSeconds * 2) + 30);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds((firecrawlTimeoutSeconds * 2) + 10);
    });

builder.Services.AddOpenAiClients();
builder.Services.AddScoped<IngestionService>();
builder.Services.AddScoped<ArticleCleanupService>();
builder.Services.AddScoped<EmbeddingService>();
builder.Services.AddScoped<CandidateClusteringService>();

builder.Build().Run();

static string GetRequiredSetting(IConfiguration configuration, string key) =>
    ResolveSetting(configuration, key) ?? throw new InvalidOperationException($"Missing configuration value '{key}'.");

static string? GetSetting(IConfiguration configuration, string key) =>
    configuration[key] ?? configuration[$"Values:{key}"];

static int GetIntSetting(IConfiguration configuration, string key, int fallback) =>
    int.TryParse(GetSetting(configuration, key), out var value) ? value : fallback;

static bool TryParseDouble(string? value, out double parsed) =>
    double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out parsed);

static string? ResolveSetting(IConfiguration configuration, string key)
{
    var value = GetSetting(configuration, key);

    if (!string.IsNullOrWhiteSpace(value))
    {
        return value;
    }

    var settingsDirectory = FindSettingsDirectory();
    var localConfiguration = new ConfigurationBuilder()
        .SetBasePath(settingsDirectory)
        .AddJsonFile("local.settings.json", optional: true)
        .Build();

    return GetSetting(localConfiguration, key);
}

static string FindSettingsDirectory()
{
    var current = new DirectoryInfo(Directory.GetCurrentDirectory());

    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "local.settings.json")))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    return Directory.GetCurrentDirectory();
}

static bool TryExtractClientId(string? authenticationString, [NotNullWhen(true)] out string? clientId)
{
    clientId = null;
    if (string.IsNullOrWhiteSpace(authenticationString))
    {
        return false;
    }

    foreach (var part in authenticationString.Split(';'))
    {
        var trimmed = part.Trim();
        if (trimmed.StartsWith("ClientId=", StringComparison.OrdinalIgnoreCase))
        {
            clientId = trimmed["ClientId=".Length..];
            return !string.IsNullOrWhiteSpace(clientId);
        }
    }

    return false;
}
