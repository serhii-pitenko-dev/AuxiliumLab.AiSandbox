using AiSandBox.ConsolePresentation;
using AiSandBox.ConsolePresentation.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiSandBox.ConsolePresentation.Configuration;

public static class ConsolePresentationServiceCollectionExtensions
{
    /// <summary>
    /// Registers console presentation services and adds Settings.json to the configuration pipeline.
    /// Use this overload when the caller owns an <see cref="IConfigurationBuilder"/>
    /// (e.g. <c>WebApplicationBuilder.Configuration</c>).
    /// </summary>
    public static IServiceCollection AddConsolePresentationServices
        (this IServiceCollection services, 
         IConfiguration configuration,
         IConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.AddJsonFile("Settings.json", optional: false, reloadOnChange: true);
        return services.AddConsolePresentationServices(configuration);
    }

    /// <summary>
    /// Registers console presentation services. Settings.json must already have been
    /// added to configuration before calling this overload (e.g. via
    /// <c>Host.CreateDefaultBuilder(...).ConfigureAppConfiguration(...)</c>).
    /// </summary>
    public static IServiceCollection AddConsolePresentationServices
        (this IServiceCollection services,
         IConfiguration configuration)
    {
        services.Configure<ConsoleSettings>(configuration.GetSection("ConsoleSettings"));
        services.AddSingleton<IConsoleRunner, ConsoleRunner>();
        return services;
    }
}