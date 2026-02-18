using AiSandBox.Ai;
using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.ApplicationServices.Runner;

public interface IExecutor
{
    Task RunAsync(Guid sandboxId = default);

    Task TestRunWithPreconditionsAsync();
}