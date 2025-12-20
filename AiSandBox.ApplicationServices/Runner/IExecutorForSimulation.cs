using AiSandBox.SharedBaseTypes.GlobalEvents;

namespace AiSandBox.ApplicationServices.Runner;

public interface IExecutorForSimulation : IExecutor
{
    event Action<Guid, GlobalEvent>? OnGlobalEventRaised;
}

