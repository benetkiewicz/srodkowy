using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Srodkowy.Functions.Configuration;
using Srodkowy.Functions.Persistence;
using Srodkowy.Functions.Services;

var builder = FunctionsApplication.CreateBuilder(args);
var firecrawlTimeoutSeconds = GetIntSetting(builder.Configuration, "Firecrawl:TimeoutSeconds", 60);
var firecrawlRequestsPerMinute = GetIntSetting(builder.Configuration, "Firecrawl:RequestsPerMinute", FirecrawlOptions.DefaultRequestsPerMinute);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

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

        if (int.TryParse(GetSetting(builder.Configuration, "Ingestion:MinContentLength"), out var minContentLength))
        {
            options.MinContentLength = minContentLength;
        }
    });

builder.Services.AddDbContextFactory<SrodkowyDbContext>(options =>
    options.UseSqlServer(GetRequiredSetting(builder.Configuration, "Database:ConnectionString")));

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

builder.Services.AddScoped<IngestionService>();

builder.Build().Run();

static string GetRequiredSetting(IConfiguration configuration, string key) =>
    ResolveSetting(configuration, key) ?? throw new InvalidOperationException($"Missing configuration value '{key}'.");

static string? GetSetting(IConfiguration configuration, string key) =>
    configuration[key] ?? configuration[$"Values:{key}"];

static int GetIntSetting(IConfiguration configuration, string key, int fallback) =>
    int.TryParse(GetSetting(configuration, key), out var value) ? value : fallback;

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
