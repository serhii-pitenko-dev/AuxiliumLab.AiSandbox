using AiSandBox.ApplicationServices.Commands.Playground;
using AiSandBox.ApplicationServices.Commands.Playground.CreatePlayground;
using AiSandBox.ApplicationServices.Commands.Playground.InitializePlaygroundFromFile;
using AiSandBox.ApplicationServices.Orchestrators;
using AiSandBox.ApplicationServices.Queries.Maps;
using AiSandBox.ApplicationServices.Queries.Maps.GetMapInitialPeconditions;
using AiSandBox.ApplicationServices.Queries.Maps.GetMapLayout;
using AiSandBox.ApplicationServices.Runner;
using AiSandBox.Domain.State;
using AiSandBox.Infrastructure.FileManager;
using Microsoft.Extensions.DependencyInjection;

namespace AiSandBox.ApplicationServices.Configuration;

public static class ApplicationServicesCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Map Commands
        services.AddScoped<ICreatePlaygroundCommandHandler, CreatePlaygroundCommandHandler>();
        services.AddScoped<IInitializePlaygroundFromFileCommandHandler, InitializePlaygroundFromFileCommandHandler>();
        services.AddScoped<IPlaygroundCommandsHandleService, PlaygroundCommandsHandleService>();

        // Map Queries
        services.AddScoped<IMapQueriesHandleService, MapQueriesHandleService>();
        services.AddScoped<IInitialPreconditions, InitialPreconditionsHandle>();
        services.AddScoped<IMapLayout, GetMapLayoutHandle>();


        services.AddScoped<IExecutor, Executor>();
        services.AddScoped<ITurnFinalizator, TurnFinalizator>();

        services.AddSingleton<IFileDataManager<MapLayoutResponse>, FileDataManager<MapLayoutResponse>>();
        services.AddSingleton<IFileDataManager<PlaygroundHistoryData>, FileDataManager<PlaygroundHistoryData>>();

        return services;
    }
}

