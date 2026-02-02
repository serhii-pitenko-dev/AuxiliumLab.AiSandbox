using AiSandBox.Ai.AgentActions;
using AiSandBox.ApplicationServices.Commands.Playground;
using AiSandBox.Common.MessageBroker;
using AiSandBox.Common.MessageBroker.Contracts.CoreServicesContract.Events;
using AiSandBox.Domain.Playgrounds;
using AiSandBox.Domain.State;
using AiSandBox.Domain.Statistics.Entities;
using AiSandBox.Infrastructure.Configuration.Preconditions;
using AiSandBox.Infrastructure.FileManager;
using AiSandBox.Infrastructure.MemoryManager;
using AiSandBox.SharedBaseTypes.AiContract.Dto;
using AiSandBox.SharedBaseTypes.ValueObjects;
using Microsoft.Extensions.Options;

namespace AiSandBox.ApplicationServices.Runner;

public class ExecutorForPresentation : Executor, IExecutorForPresentation
{
    public ExecutorForPresentation(
        IPlaygroundCommandsHandleService mapCommands, 
        IMemoryDataManager<StandardPlayground> sandboxRepository, 
        IAiActions aiActions, 
        IOptions<SandBoxConfiguration> configuration, IMemoryDataManager<PlayGroundStatistics> statisticsMemoryRepository, IFileDataManager<PlayGroundStatistics> statisticsFileRepository, IFileDataManager<StandardPlayground> playgroundFileRepository, IFileDataManager<PlaygroundHistoryData> playgroundHistoryDataFileRepository, IMemoryDataManager<AgentState> agentStateMemoryRepository, IMessageBroker messageBroker, IBrokerRpcClient brokerRpcClient):
        base(mapCommands, sandboxRepository, aiActions, configuration, statisticsMemoryRepository, statisticsFileRepository, playgroundFileRepository, playgroundHistoryDataFileRepository, agentStateMemoryRepository, messageBroker, brokerRpcClient)
    {
    }

    protected override void SendAgentMoveNotification(Guid id, Guid playgroundId, Guid agentId, Coordinates from, Coordinates to, bool isSuccess, AgentSnapshot agentSnapshot)
    {
        OnBaseAgentActionEvent moveEvent = new OnAgentMoveActionEvent(
            id,
            playgroundId,
            agentId,
            from,
            to,
            isSuccess,
            agentSnapshot);

        _messageBroker.Publish(moveEvent);
    }

    protected override void SendAgentToggleActionNotification(AgentAction action, Guid playgroundId, Guid agentId, bool isActivated, AgentSnapshot agentSnapshot)
    {
        OnBaseAgentActionEvent actionEvent = new OnAgentToggleActionEvent(
            Guid.NewGuid(),
            playgroundId,
            agentId,
            action,
            isActivated,
            agentSnapshot);

        _messageBroker.Publish(actionEvent);
    }
}

