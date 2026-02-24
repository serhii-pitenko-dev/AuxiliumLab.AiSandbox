using AiSandBox.Ai;
using AiSandBox.Domain.Statistics.Result;
using AiSandBox.Infrastructure.Configuration.Preconditions;

namespace AiSandBox.ApplicationServices.Executors;

public interface IStandardExecutor : IExecutor
{
    /// <summary>Runs a simulation and returns the captured outcome using the injected default configuration.</summary>
    Task<ParticularRun> RunAndCaptureAsync();

    /// <summary>Runs a simulation and returns the captured outcome using the supplied <paramref name="sandBoxConfiguration"/>.</summary>
    Task<ParticularRun> RunAndCaptureAsync(SandBoxConfiguration sandBoxConfiguration);
}
