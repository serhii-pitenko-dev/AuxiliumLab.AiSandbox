using AiSandBox.Ai;
using AiSandBox.ApplicationServices.Commands.Playground;
using AiSandBox.ApplicationServices.Commands.Playground.CreatePlayground;
using AiSandBox.ApplicationServices.Queries.Maps;
using AiSandBox.ApplicationServices.Queries.Maps.GetAffectedCells;
using AiSandBox.ApplicationServices.Queries.Maps.GetMapLayout;
using AiSandBox.ApplicationServices.Runner;
using AiSandBox.ApplicationServices.Runner.LogsDto;
using AiSandBox.ApplicationServices.Runner.LogsDto.Performance;
using AiSandBox.ApplicationServices.Runner.TestPreconditionSet;
using AiSandBox.ApplicationServices.Saver.Persistence.Sandbox.Mappers;
using AiSandBox.ApplicationServices.Saver.Persistence.Sandbox.States;
using AiSandBox.Infrastructure.FileManager;
using Microsoft.Extensions.DependencyInjection;

namespace AiSandBox.ApplicationServices.Configuration;

public static class ApplicationServicesCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Map Commands
        services.AddScoped<ICreatePlaygroundCommandHandler, CreatePlaygroundCommandHandler>();
        services.AddScoped<IPlaygroundCommandsHandleService, PlaygroundCommandsHandleService>();


        // Map Queries
        services.AddScoped<IMapQueriesHandleService, MapQueriesHandleService>();
        services.AddScoped<IMapLayout, GetMapLayoutHandle>();
        services.AddScoped<IAffectedCells, GetAffectedCellsHandle>();

        services.AddSingleton<IFileDataManager<MapLayoutResponse>, FileDataManager<MapLayoutResponse>>();
        services.AddSingleton<IFileDataManager<StandardPlaygroundState>, FileDataManager<StandardPlaygroundState>>();
        services.AddSingleton<IFileDataManager<RawDataLog>, FileDataManager<RawDataLog>>();

        #if PERFORMANCE_ANALYSIS
            #if PERFORMANCE_DETAILED_ANALYSIS
                services.AddSingleton<IFileDataManager<TurnExecutionPerformance>, FileDataManager<TurnExecutionPerformance>>();
            #endif
                services.AddSingleton<IFileDataManager<SandboxExecutionPerformance>, FileDataManager<SandboxExecutionPerformance>>();
        #else
                services.AddSingleton<IFileDataManager<TurnExecutionPerformance>, NullFileDataManager<TurnExecutionPerformance>>();
                services.AddSingleton<IFileDataManager<SandboxExecutionPerformance>, NullFileDataManager<SandboxExecutionPerformance>>();
        #endif

        services.AddSingleton<IStandardPlaygroundMapper, StandardPlaygroundMapper>();

        services.AddScoped<IExecutorForPresentation, ExecutorForPresentation>();
        services.AddScoped<IStandardExecutor, StandardExecutor>();

        services.AddSingleton<ITestPreconditionData, TestPreconditionData>();

        return services;
    }
}

