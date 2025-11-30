using AiSandBox.Domain.Agents.Factories;
using AiSandBox.Domain.Agents.Services.Vision;
using AiSandBox.Domain.Playgrounds.Builders;
using AiSandBox.Domain.Playgrounds.Factories;
using Microsoft.Extensions.DependencyInjection;

namespace AiSandBox.Domain.Configuration;

public static class DomainServiceCollectionExtensions
{
    public static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        services.AddTransient<IPlaygroundBuilder, PlaygroundBuilder>();
        services.AddTransient<IPlaygroundFactory, PlaygroundFactory>();

        services.AddTransient<IEnemyFactory, EnemyFactory>();
        services.AddTransient<IHeroFactory, HeroFactory>();
        services.AddTransient<IVisibilityService, VisibilityService>();

        return services;
    }
}

