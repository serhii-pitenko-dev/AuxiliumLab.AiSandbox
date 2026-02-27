using AuxiliumLab.AiSandbox.Ai;
using AuxiliumLab.AiSandbox.ApplicationServices.Commands.Playground;
using AuxiliumLab.AiSandbox.ApplicationServices.Runner.LogsDto;
using AuxiliumLab.AiSandbox.ApplicationServices.Runner.LogsDto.Performance;
using AuxiliumLab.AiSandbox.ApplicationServices.Runner.TestPreconditionSet;
using AuxiliumLab.AiSandbox.ApplicationServices.Saver.Persistence.Sandbox.Mappers;
using AuxiliumLab.AiSandbox.ApplicationServices.Saver.Persistence.Sandbox.States;
using AuxiliumLab.AiSandbox.Common.MessageBroker;
using AuxiliumLab.AiSandbox.Common.MessageBroker.Contracts.CoreServicesContract.Events;
using AuxiliumLab.AiSandbox.Domain.Playgrounds;
using AuxiliumLab.AiSandbox.Infrastructure.Configuration.Preconditions;
using AuxiliumLab.AiSandbox.Infrastructure.FileManager;
using AuxiliumLab.AiSandbox.Infrastructure.MemoryManager;
using AuxiliumLab.AiSandbox.SharedBaseTypes.AiContract.Dto;
using AuxiliumLab.AiSandbox.SharedBaseTypes.ValueObjects;
using Microsoft.Extensions.Options;

namespace AuxiliumLab.AiSandbox.ApplicationServices.Executors;

public class ExecutorForPresentation : Executor, IExecutorForPresentation
{
    /// <inheritdoc/>
    protected override bool NeedsStatePersistence => true;

    public ExecutorForPresentation(
        IPlaygroundCommandsHandleService mapCommands,
        IMemoryDataManager<StandardPlayground> sandboxRepository,
        IAiActions aiActions,
        IOptions<SandBoxConfiguration> configuration,
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
             configuration,
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
