using AiSandBox.Domain.Playgrounds;
using AiSandBox.Domain.State;
using AiSandBox.Domain.Statistics.Entities;
using AiSandBox.Infrastructure.Configuration.Preconditions;
using AiSandBox.Infrastructure.FileManager;
using AiSandBox.Infrastructure.MemoryManager;
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
        services.AddSingleton<IMemoryDataManager<InitialPreconditions>, MemoryDataManager<InitialPreconditions>>();
        services.AddSingleton<IMemoryDataManager<AgentStatistics>, MemoryDataManager<AgentStatistics>>();

        // Add File Data Managers - Changed from Map to Sandbox
        services.AddSingleton<IFileDataManager<StandardPlayground>, FileDataManager<StandardPlayground>>();
        services.AddSingleton<IFileDataManager<AgentStatistics>, FileDataManager<AgentStatistics>>();
        services.AddSingleton<IFileDataManager<PlayGroundStatistics>, FileDataManager<PlayGroundStatistics>>();
        services.AddSingleton<IFileDataManager<StandardPlayground>, FileDataManager<StandardPlayground>>();


        return services;
    }
}

