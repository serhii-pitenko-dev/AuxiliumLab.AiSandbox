using AiSandBox.SharedBaseTypes.GlobalEvents;

namespace AiSandBox.ApplicationServices.Runner;

public interface IExecutorForPresentation: IExecutor
{
    event Action<Guid, GlobalEventPresentation>? OnGlobalEventRaised;
}

