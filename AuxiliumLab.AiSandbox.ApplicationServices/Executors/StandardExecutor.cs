using AuxiliumLab.AiSandbox.Ai;
using AuxiliumLab.AiSandbox.ApplicationServices.Commands.Playground;
using AuxiliumLab.AiSandbox.ApplicationServices.Runner.LogsDto;
using AuxiliumLab.AiSandbox.ApplicationServices.Runner.LogsDto.Performance;
using AuxiliumLab.AiSandbox.ApplicationServices.Runner.TestPreconditionSet;
using AuxiliumLab.AiSandbox.ApplicationServices.Saver.Persistence.Sandbox.Mappers;
using AuxiliumLab.AiSandbox.ApplicationServices.Saver.Persistence.Sandbox.States;
using AuxiliumLab.AiSandbox.Common.MessageBroker;
using AuxiliumLab.AiSandbox.Common.MessageBroker.Contracts.GlobalMessagesContract.Events.Lost;
using AuxiliumLab.AiSandbox.Common.MessageBroker.Contracts.GlobalMessagesContract.Events.Win;
using AuxiliumLab.AiSandbox.Domain.Playgrounds;
using AuxiliumLab.AiSandbox.Domain.Statistics.Entities;
using AuxiliumLab.AiSandbox.Domain.Statistics.Result;
using AuxiliumLab.AiSandbox.Infrastructure.Configuration.Preconditions;
using AuxiliumLab.AiSandbox.Infrastructure.FileManager;
using AuxiliumLab.AiSandbox.Infrastructure.MemoryManager;
using AuxiliumLab.AiSandbox.SharedBaseTypes.AiContract.Dto;
using AuxiliumLab.AiSandbox.SharedBaseTypes.ValueObjects;
using Microsoft.Extensions.Options;

namespace AuxiliumLab.AiSandbox.ApplicationServices.Executors;

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
