using AuxiliumLab.AiSandbox.Ai;
using AuxiliumLab.AiSandbox.ApplicationServices.Commands.Playground;
using AuxiliumLab.AiSandbox.ApplicationServices.Commands.Playground.CreatePlayground;
using AuxiliumLab.AiSandbox.ApplicationServices.Executors;
using AuxiliumLab.AiSandbox.ApplicationServices.Queries.Maps;
using AuxiliumLab.AiSandbox.ApplicationServices.Queries.Maps.GetAffectedCells;
using AuxiliumLab.AiSandbox.ApplicationServices.Queries.Maps.GetMapLayout;
using AuxiliumLab.AiSandbox.ApplicationServices.Runner.LogsDto;
using AuxiliumLab.AiSandbox.ApplicationServices.Runner.LogsDto.Performance;
using AuxiliumLab.AiSandbox.ApplicationServices.Runner.TestPreconditionSet;
using AuxiliumLab.AiSandbox.ApplicationServices.Saver.Persistence.Sandbox.Mappers;
using AuxiliumLab.AiSandbox.ApplicationServices.Saver.Persistence.Sandbox.States;
using AuxiliumLab.AiSandbox.Domain.Statistics.Result;
using AuxiliumLab.AiSandbox.Infrastructure.FileManager;
using Microsoft.Extensions.DependencyInjection;

namespace AuxiliumLab.AiSandbox.ApplicationServices.Configuration;

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
        services.AddSingleton<IFileDataManager<GeneralBatchRunInformation>, FileDataManager<GeneralBatchRunInformation>>();

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

        services.AddTransient<IExecutorFactory, ExecutorFactory>();
        services.AddTransient<IExecutorForPresentation>(provider => provider.GetRequiredService<IExecutorFactory>().CreateExecutorForPresentation());
        services.AddTransient<IStandardExecutor>(provider => provider.GetRequiredService<IExecutorFactory>().CreateStandardExecutor());

        services.AddSingleton<ITestPreconditionData, TestPreconditionData>();

        return services;
    }
}

