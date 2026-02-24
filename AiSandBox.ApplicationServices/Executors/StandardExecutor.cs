using AiSandBox.Ai;
using AiSandBox.ApplicationServices.Commands.Playground;
using AiSandBox.ApplicationServices.Runner.LogsDto;
using AiSandBox.ApplicationServices.Runner.LogsDto.Performance;
using AiSandBox.ApplicationServices.Runner.TestPreconditionSet;
using AiSandBox.ApplicationServices.Saver.Persistence.Sandbox.Mappers;
using AiSandBox.ApplicationServices.Saver.Persistence.Sandbox.States;
using AiSandBox.Common.MessageBroker;
using AiSandBox.Common.MessageBroker.Contracts.GlobalMessagesContract.Events.Lost;
using AiSandBox.Common.MessageBroker.Contracts.GlobalMessagesContract.Events.Win;
using AiSandBox.Domain.Playgrounds;
using AiSandBox.Domain.Statistics.Entities;
using AiSandBox.Domain.Statistics.Result;
using AiSandBox.Infrastructure.Configuration.Preconditions;
using AiSandBox.Infrastructure.FileManager;
using AiSandBox.Infrastructure.MemoryManager;
using AiSandBox.SharedBaseTypes.AiContract.Dto;
using AiSandBox.SharedBaseTypes.ValueObjects;
using Microsoft.Extensions.Options;

namespace AiSandBox.ApplicationServices.Executors;

public class StandardExecutor : Executor, IStandardExecutor
{
    public StandardExecutor(
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

    /// <inheritdoc/>
    public Task<ParticularRun> RunAndCaptureAsync() => RunAndCaptureAsync(default);

    /// <inheritdoc/>
    public async Task<ParticularRun> RunAndCaptureAsync(SandBoxConfiguration sandBoxConfiguration)
    {
        WinReason? winReason = null;
        LostReason? lostReason = null;

        void OnWon(HeroWonEvent e) { winReason = e.WinReason; }
        void OnLost(HeroLostEvent e) { lostReason = e.LostReason; }

        _messageBroker.Subscribe<HeroWonEvent>(OnWon);
        _messageBroker.Subscribe<HeroLostEvent>(OnLost);

        try
        {
            await RunAsync(default, sandBoxConfiguration);
        }
        finally
        {
            _messageBroker.Unsubscribe<HeroWonEvent>(OnWon);
            _messageBroker.Unsubscribe<HeroLostEvent>(OnLost);
        }

        return new ParticularRun(
            _playground.Id,
            _playground.Turn,
            _playground.Enemies.Count,
            winReason,
            lostReason);
    }

    protected override void SendAgentMoveNotification(Guid id, Guid playgroundId, Guid agentId, Coordinates from, Coordinates to, bool isSuccess, AgentSnapshot agentSnapshot)
    {
    }

    protected override void SendAgentToggleActionNotification(AgentAction action, Guid playgroundId, Guid agentId, bool isActivated, AgentSnapshot agentSnapshot)
    {
    }
}
