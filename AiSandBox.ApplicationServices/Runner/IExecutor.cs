using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.ApplicationServices.Runner;

public interface IExecutor
{
    event Action<Guid>? GameStarted;
    event Action<Guid>? TurnExecuted;
    event Action<Guid, ESandboxStatus>? ExecutionFinished;

    void Run();
}

