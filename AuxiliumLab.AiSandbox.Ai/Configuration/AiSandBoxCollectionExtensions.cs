using AuxiliumLab.AiSandbox.SharedBaseTypes.ValueObjects.StartupSettings;
using Microsoft.Extensions.DependencyInjection;

namespace AuxiliumLab.AiSandbox.Ai.Configuration;

public static class AiSandboxCollectionExtensions
{
    public static IServiceCollection AddAiSandboxServices(
        this IServiceCollection services,
        ExecutionMode executionMode)
    {
        services.AddSingleton<Sb3AlgorithmTypeProvider>();

        if (executionMode != ExecutionMode.Training)
        {
            // RandomActions is the default IAiActions for all non-training modes
            services.AddScoped<IAiActions, RandomActions>();
        }
        // In training mode, Sb3Actions instances are created manually by RunTraining
        // (one per executor/gym). IAiActions is NOT resolved from DI for training.

        return services;
    }
}

