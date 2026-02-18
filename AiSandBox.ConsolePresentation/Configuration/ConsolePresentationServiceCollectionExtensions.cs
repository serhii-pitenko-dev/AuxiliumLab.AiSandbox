using AiSandBox.ConsolePresentation;
using AiSandBox.ConsolePresentation.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiSandBox.ConsolePresentation.Configuration;

public static class ConsolePresentationServiceCollectionExtensions
{
    public static IServiceCollection AddConsolePresentationServices
        (this IServiceCollection services, 
         IConfiguration configuration,
         IConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.AddJsonFile("Settings.json", optional: false, reloadOnChange: true);
        services.Configure<ConsoleSettings>(configuration.GetSection("ConsoleSettings"));

        services.AddSingleton<IConsoleRunner, ConsoleRunner>();

        return services;
    }
}