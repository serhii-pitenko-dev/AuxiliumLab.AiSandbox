using AiSandBox.Ai;
using AiSandBox.SharedBaseTypes.ValueObjects;
using AiSandBox.Infrastructure.Configuration.Preconditions;

namespace AiSandBox.ApplicationServices.Executors;

public interface IExecutor
{
    Task RunAsync(Guid sandboxId = default, SandBoxConfiguration sandBoxConfiguration = default);

    Task TestRunWithPreconditionsAsync();
}
