using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Srodkowy.Functions.Configuration;

namespace Srodkowy.Functions.Services.Ai;

public static class OpenAiServiceCollectionExtensions
{
    public static IServiceCollection AddOpenAiClients(this IServiceCollection services)
    {
        services.AddKeyedSingleton<IChatClient>("cleanup", (serviceProvider, _) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<OpenAiOptions>>().Value;
            var observability = serviceProvider.GetRequiredService<IOptions<ObservabilityOptions>>().Value;

            return new ChatClientBuilder(new OpenAI.Chat.ChatClient(options.CleanupModel, GetRequiredApiKey(options)).AsIChatClient())
                .UseOpenTelemetry(
                    sourceName: ObservabilityOptions.CleanupChatSourceName,
                    configure: settings => settings.EnableSensitiveData = observability.EnableSensitiveData)
                .Build();
        });

        services.AddSingleton<IChatClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<OpenAiOptions>>().Value;
            var observability = serviceProvider.GetRequiredService<IOptions<ObservabilityOptions>>().Value;

            return new ChatClientBuilder(new OpenAI.Chat.ChatClient(options.ChatModel, GetRequiredApiKey(options)).AsIChatClient())
                .UseOpenTelemetry(
                    sourceName: ObservabilityOptions.ChatSourceName,
                    configure: settings => settings.EnableSensitiveData = observability.EnableSensitiveData)
                .Build();
        });

        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<OpenAiOptions>>().Value;
            var observability = serviceProvider.GetRequiredService<IOptions<ObservabilityOptions>>().Value;

            return new EmbeddingGeneratorBuilder<string, Embedding<float>>(new OpenAI.Embeddings.EmbeddingClient(options.EmbeddingModel, GetRequiredApiKey(options)).AsIEmbeddingGenerator())
                .UseOpenTelemetry(
                    sourceName: ObservabilityOptions.EmbeddingSourceName,
                    configure: settings => settings.EnableSensitiveData = observability.EnableSensitiveData)
                .Build();
        });

        return services;
    }

    private static string GetRequiredApiKey(OpenAiOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return options.ApiKey;
        }

        throw new InvalidOperationException("Missing configuration value 'OpenAi:ApiKey'.");
    }
}
