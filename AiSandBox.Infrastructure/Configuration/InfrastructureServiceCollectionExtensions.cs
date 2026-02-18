using AiSandBox.Domain.Playgrounds;
using AiSandBox.Domain.Statistics.Entities;
using AiSandBox.Infrastructure.Configuration.Preconditions;
using AiSandBox.Infrastructure.FileManager;
using AiSandBox.Infrastructure.MemoryManager;
using AiSandBox.SharedBaseTypes.AiContract.Dto;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiSandBox.Infrastructure.Configuration;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SandBoxConfiguration>(configuration.GetSection("SandBox"));

        // Changed from Map to Sandbox
        services.AddSingleton<IMemoryDataManager<StandardPlayground>, MemoryDataManager<StandardPlayground>>();
        services.AddSingleton<IMemoryDataManager<PlayGroundStatistics>, MemoryDataManager<PlayGroundStatistics>>();
        services.AddSingleton<IMemoryDataManager<AgentStatistics>, MemoryDataManager<AgentStatistics>>();
        services.AddSingleton<IMemoryDataManager<AgentStateForAIDecision>, MemoryDataManager<AgentStateForAIDecision>>();

        // Add File Data Managers
        services.AddSingleton<IFileDataManager<StandardPlayground>, FileDataManager<StandardPlayground>>();
        services.AddSingleton<IFileDataManager<AgentStatistics>, FileDataManager<AgentStatistics>>();
        services.AddSingleton<IFileDataManager<PlayGroundStatistics>, FileDataManager<PlayGroundStatistics>>();
        services.AddSingleton<IFileDataManager<AgentStateForAIDecision>, FileDataManager<AgentStateForAIDecision>>();

        return services;
    }
}

