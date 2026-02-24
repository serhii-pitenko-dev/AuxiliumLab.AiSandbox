using AiSandBox.Ai;
using AiSandBox.ApplicationServices.Commands.Playground;
using AiSandBox.ApplicationServices.Runner.LogsDto;
using AiSandBox.ApplicationServices.Runner.LogsDto.Performance;
using AiSandBox.ApplicationServices.Runner.TestPreconditionSet;
using AiSandBox.ApplicationServices.Saver.Persistence.Sandbox.Mappers;
using AiSandBox.ApplicationServices.Saver.Persistence.Sandbox.States;
using AiSandBox.Common.MessageBroker;
using AiSandBox.Common.MessageBroker.Contracts.CoreServicesContract.Events;
using AiSandBox.Domain.Playgrounds;
using AiSandBox.Domain.Statistics.Entities;
using AiSandBox.Infrastructure.Configuration.Preconditions;
using AiSandBox.Infrastructure.FileManager;
using AiSandBox.Infrastructure.MemoryManager;
using AiSandBox.SharedBaseTypes.AiContract.Dto;
using AiSandBox.SharedBaseTypes.ValueObjects;
using Microsoft.Extensions.Options;

namespace AiSandBox.ApplicationServices.Executors;

public class ExecutorForPresentation : Executor, IExecutorForPresentation
{
    public ExecutorForPresentation(
        IPlaygroundCommandsHandleService mapCommands,
        IMemoryDataManager<StandardPlayground> sandboxRepository,
        IAiActions aiActions,
        IOptions<SandBoxConfiguration> configuration,
        IMemoryDataManager<PlayGroundStatistics> statisticsMemoryRepository,
        IFileDataManager<PlayGroundStatistics> statisticsFileRepository,
        IFileDataManager<StandardPlaygroundState> playgroundStateFileRepository,
        IMemoryDataManager<AgentStateForAIDecision> agentStateMemoryRepository,
        IMessageBroker messageBroker,
        IBrokerRpcClient brokerRpcClient,
        IStandardPlaygroundMapper standardPlaygroundMapper,
        IFileDataManager<RawDataLog> rawDataLogFileRepository,
        IFileDataManager<TurnExecutionPerformance> turnExecutionPerformanceFileRepository,
        IFileDataManager<SandboxExecutionPerformance> sandboxExecutionPerformanceFileRepository,
        ITestPreconditionData testPreconditionData) :
        base(mapCommands, sandboxRepository, aiActions,
             configuration, statisticsMemoryRepository, statisticsFileRepository,
             playgroundStateFileRepository, agentStateMemoryRepository, messageBroker,
             brokerRpcClient, standardPlaygroundMapper, rawDataLogFileRepository,
             turnExecutionPerformanceFileRepository, sandboxExecutionPerformanceFileRepository,
             testPreconditionData)
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

        _messageBroker.Publish<OnBaseAgentActionEvent>(moveEvent);
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

        _messageBroker.Publish<OnBaseAgentActionEvent>(actionEvent);
    }
}
