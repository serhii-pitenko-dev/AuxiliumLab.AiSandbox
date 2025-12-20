using AiSandBox.Ai.AgentActions;
using AiSandBox.ApplicationServices.Commands.Playground;
using AiSandBox.Common.MessageBroker;
using AiSandBox.Domain.Playgrounds;
using AiSandBox.Domain.State;
using AiSandBox.Domain.Statistics.Entities;
using AiSandBox.Infrastructure.Configuration.Preconditions;
using AiSandBox.Infrastructure.FileManager;
using AiSandBox.Infrastructure.MemoryManager;
using AiSandBox.SharedBaseTypes.GlobalEvents;
using Microsoft.Extensions.Options;

namespace AiSandBox.ApplicationServices.Runner;

public class ExecutorForSimulation : Executor, IExecutorForSimulation
{
    public event Action<Guid, GlobalEvent>? OnGlobalEventRaised;

    public ExecutorForSimulation(
        IPlaygroundCommandsHandleService mapCommands,
        IMemoryDataManager<StandardPlayground> sandboxRepository,
        IAiActions aiActions,
        IOptions<SandBoxConfiguration> configuration,
        IMemoryDataManager<PlayGroundStatistics> statisticsMemoryRepository,
        IFileDataManager<PlayGroundStatistics> statisticsFileRepository,
        IFileDataManager<StandardPlayground> playgroundFileRepository,
        IFileDataManager<PlaygroundHistoryData> playgroundHistoryDataFileRepository,
        IMessageBroker messageBroker) : base(mapCommands, sandboxRepository, aiActions, configuration, statisticsMemoryRepository, statisticsFileRepository, playgroundFileRepository, playgroundHistoryDataFileRepository, messageBroker)
    {
    }

    protected override void OnGlobalEventInvoked(GlobalEvent globalEvent)
    {
        OnGlobalEventRaised?.Invoke(_playground.Id, globalEvent);
    }
}

