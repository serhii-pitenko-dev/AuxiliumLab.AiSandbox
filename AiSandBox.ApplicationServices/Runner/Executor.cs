using AiSandBox.Ai.AgentActions;
using AiSandBox.Ai.Messages;
using AiSandBox.ApplicationServices.Commands.Playground;
using AiSandBox.ApplicationServices.Commands.Playground.CreatePlayground;
using AiSandBox.Common.MessageBroker;
using AiSandBox.Domain.Playgrounds;
using AiSandBox.Domain.State;
using AiSandBox.Domain.Statistics.Entities;
using AiSandBox.Infrastructure.Configuration.Preconditions;
using AiSandBox.Infrastructure.FileManager;
using AiSandBox.Infrastructure.MemoryManager;
using AiSandBox.SharedBaseTypes.GlobalEvents;
using AiSandBox.SharedBaseTypes.GlobalEvents.Actions.Agent;
using AiSandBox.SharedBaseTypes.GlobalEvents.GameStateEvents;
using AiSandBox.SharedBaseTypes.ValueObjects;
using Microsoft.Extensions.Options;

namespace AiSandBox.ApplicationServices.Runner;

public abstract class Executor
{
    private readonly IPlaygroundCommandsHandleService _playgroundCommands;
    private readonly IMemoryDataManager<StandardPlayground> _playgroundRepository;
    private readonly IFileDataManager<PlaygroundHistoryData> _playgroundHistoryDataFileRepository;
    private readonly IAiActions _aiActions;
    private readonly SandBoxConfiguration _configuration;
    private readonly IMemoryDataManager<PlayGroundStatistics> _statisticsMemoryRepository;
    private readonly IFileDataManager<PlayGroundStatistics> _statisticsFileRepository;
    private readonly IMessageBroker _messageBroker;
    private ESandboxStatus sandboxStatus;
    protected StandardPlayground _playground;

    public event Action<Guid>? OnGameStarted;
    public event Action<Guid, int>? OnTurnExecuted;
    public event Action<Guid, ESandboxStatus>? OnGameFinished;

    public Executor(
        IPlaygroundCommandsHandleService mapCommands,
        IMemoryDataManager<StandardPlayground> sandboxRepository,
        IAiActions aiActions,
        IOptions<SandBoxConfiguration> configuration,
        IMemoryDataManager<PlayGroundStatistics> statisticsMemoryRepository,
        IFileDataManager<PlayGroundStatistics> statisticsFileRepository,
        IFileDataManager<StandardPlayground> playgroundFileRepository,
        IFileDataManager<PlaygroundHistoryData> playgroundHistoryDataFileRepository,
        IMessageBroker messageBroker)
    {
        _playgroundCommands = mapCommands;
        _playgroundRepository = sandboxRepository;
        _aiActions = aiActions;
        _configuration = configuration.Value;
        _statisticsMemoryRepository = statisticsMemoryRepository;
        _statisticsFileRepository = statisticsFileRepository;
        _playgroundHistoryDataFileRepository = playgroundHistoryDataFileRepository;
        _messageBroker = messageBroker;
    }

    public void Run()
    {
        // 1. Create standard map/sandbox
        var sandboxId = _playgroundCommands.CreatePlaygroundCommand.Handle(new CreatePlaygroundCommandParameters(
            _configuration.MapSettings,
            _configuration.Hero,
            _configuration.Enemy
        ));

        _playground = _playgroundRepository.LoadObject(sandboxId);

        // Invoke game started event with sandboxId
        StartGame();

        // Subscribe to game end events
        _aiActions.OnGameLost += OnGameLost;
        _aiActions.OnGameWin += OnGameWon;
        _aiActions.OnAgentAction += OnGlobalEventInvoked;
        _aiActions.OnAgentActionsCompleted += OnAgentActionsCompletedEvent;

        // 2. Endless cycle with turn-based execution
        while (sandboxStatus == ESandboxStatus.InProgress && _playground.Turn < _configuration.MaxTurns)
        {

            // 3. Execute agent actions
            ExecuteTurn(_playground);

            // Save the updated sandbox state
            _playgroundRepository.AddOrUpdate(sandboxId, _playground);

            // 5. Invoke end turn event
            OnTurnEnded();

            // 4. Check if max turns reached
            if (_playground.Turn >= _configuration.MaxTurns)
            {
                sandboxStatus = ESandboxStatus.TurnLimitReached;
                OnGameEndedByMaxTurns();
            }
        }

        // Cleanup
        _aiActions.OnGameLost -= OnGameLost;
        _aiActions.OnGameWin -= OnGameWon;
        _aiActions.OnAgentAction -= OnGlobalEventInvoked;
        _aiActions.OnAgentActionsCompleted -= OnAgentActionsCompletedEvent;
    }

    private void ExecuteTurn(StandardPlayground playground)
    {
        // Clear paths from previous turn

        // Execute hero action with playground ID
        playground.PrepareAgentForTurnActions(playground.Hero);
        _messageBroker.Publish(new AgentActionMessage(playground.Hero, playground.Id));

        // Execute enemy actions with playground ID
        foreach (var enemy in playground.Enemies.OrderBy(e => e.OrderInTurnQueue))
        {
            playground.PrepareAgentForTurnActions(enemy);
            _messageBroker.Publish(new AgentActionMessage(enemy, playground.Id));
        }
    }

    private void OnGameLost(GameLostEvent gameLostEvent)
    {
        // Check if the event is for the current playground
        if (gameLostEvent.PlaygroundId == _playground.Id)
        {
            sandboxStatus = ESandboxStatus.HeroLost;
            // Fix: Use null-conditional operator and direct invocation instead of EndInvoke
            OnGameFinished?.Invoke(gameLostEvent.PlaygroundId, ESandboxStatus.HeroLost);
        }
    }

    private void OnGameWon(GameWonEvent gameWonEvent)
    {
        // Check if the event is for the current playground
        if (gameWonEvent.PlaygroundId == _playground.Id)
        {
            sandboxStatus = ESandboxStatus.HeroWon;
            OnGameFinished?.Invoke(gameWonEvent.PlaygroundId, ESandboxStatus.HeroWon);
        }
    }

    private void OnGameEndedByMaxTurns()
    {
        // Handle max turns reached logic
        OnGameFinished?.Invoke(_playground.Id, ESandboxStatus.TurnLimitReached);
    }

    private void Save()
    {
        var dataToSave = _playground.GetCurrentState();
        if (_playground.Turn == 0)
        {
            var historyData = new PlaygroundHistoryData
            {
                Id = _playground.Id,
                States = new List<PlaygroundState> { dataToSave }
            };
            _playgroundHistoryDataFileRepository.AddOrUpdate(_playground.Id, historyData);
            return;
        }

        PlaygroundHistoryData previousData = _playgroundHistoryDataFileRepository.LoadObject(_playground.Id);

        previousData.States.Add(dataToSave);
        _playgroundHistoryDataFileRepository.AddOrUpdate(_playground.Id, previousData);
    }

    protected virtual void StartGame()
    {
        Save();
        _playground.LookAroundEveryone();

        OnGameStarted?.Invoke(_playground.Id);
    }

    protected virtual void OnTurnEnded()
    {
        _playground.NextTurn();
        Save();
        OnTurnExecuted?.Invoke(_playground.Id, _playground.Turn);
    }

    protected virtual void OnAgentActionsCompletedEvent(List<BaseAgentActionEvent> actions)
    {

    }

    protected abstract void OnGlobalEventInvoked(GlobalEvent globalEvent);
}