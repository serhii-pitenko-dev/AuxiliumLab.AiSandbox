using AiSandBox.Ai.AgentActions;
using AiSandBox.ApplicationServices.Commands.Playground;
using AiSandBox.ApplicationServices.Runner.Logs.Presentation;
using AiSandBox.Common.MessageBroker;
using AiSandBox.Domain.Agents.Entities;
using AiSandBox.Domain.Playgrounds;
using AiSandBox.Domain.State;
using AiSandBox.Domain.Statistics.Entities;
using AiSandBox.Infrastructure.Configuration.Preconditions;
using AiSandBox.Infrastructure.FileManager;
using AiSandBox.Infrastructure.MemoryManager;
using AiSandBox.SharedBaseTypes.GlobalEvents;
using AiSandBox.SharedBaseTypes.GlobalEvents.Actions.Agent;
using AiSandBox.SharedBaseTypes.ValueObjects;
using Microsoft.Extensions.Options;

namespace AiSandBox.ApplicationServices.Runner;

public class ExecutorForPresentation: Executor, IExecutorForPresentation
{
    public event Action<Guid, GlobalEventPresentation>? OnGlobalEventRaised;

    public ExecutorForPresentation(
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
        Dictionary<string, string> additionalInfo = new();
        if (globalEvent is AgentMoveActionEvent agentEvent)
        {
            Agent? agent = agentEvent.Type == EObjectType.Hero
                ? _playground.Hero
                : _playground.Enemies.FirstOrDefault(e => e.Id == agentEvent.AgentId);

            AgentLog agentLog = new(
                agent.Id, 
                agent.Type, 
                agent.Speed, 
                agent.SightRange, 
                agent.IsRun, 
                agent.Stamina, 
                agent.MaxStamina, 
                agent.OrderInTurnQueue);

            GlobalEventPresentation eventPresentation = new(globalEvent, agentLog);

            OnGlobalEventRaised?.Invoke(_playground.Id, eventPresentation);
        }

        OnGlobalEventRaised?.Invoke(_playground.Id, new(globalEvent, null));
    }
}

