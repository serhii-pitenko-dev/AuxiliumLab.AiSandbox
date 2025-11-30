using AiSandBox.Ai.AgentActions;
using Microsoft.Extensions.DependencyInjection;

namespace AiSandBox.Ai.Configuration;

public static class AiSandBoxCollectionExtensions
{
    public static IServiceCollection AddAiSandBoxServices(this IServiceCollection services)
    {
        services.AddTransient<IAiActions, RandomActions>();

        return services;
    }
}

