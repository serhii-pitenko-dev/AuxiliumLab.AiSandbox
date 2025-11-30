using AiSandBox.Ai.AgentActions;
using AiSandBox.ApplicationServices.Commands.Playground;
using AiSandBox.ApplicationServices.Commands.Playground.CreatePlayground;
using AiSandBox.Domain.Playgrounds;
using AiSandBox.Domain.State;
using AiSandBox.Domain.Statistics.Entities;
using AiSandBox.Infrastructure.Configuration.Preconditions;
using AiSandBox.Infrastructure.FileManager;
using AiSandBox.Infrastructure.MemoryManager;
using AiSandBox.SharedBaseTypes.ValueObjects;
using Microsoft.Extensions.Options;

namespace AiSandBox.ApplicationServices.Runner;

public class Executor : IExecutor
{
    private readonly IPlaygroundCommandsHandleService _playgroundCommands;
    private readonly IMemoryDataManager<StandardPlayground> _playgroundRepository;
    private readonly IFileDataManager<PlaygroundHistoryData> _playgroundHistoryDataFileRepository;
    private readonly IAiActions _aiActions;
    private readonly SandBoxConfiguration _configuration;
    private readonly IMemoryDataManager<PlayGroundStatistics> _statisticsMemoryRepository;
    private readonly IFileDataManager<PlayGroundStatistics> _statisticsFileRepository;
    private ESandboxStatus sandboxStatus;
    private Guid _sandboxId;

    public int Turn { get; private set; } = 0;
    public event Action<Guid>? GameStarted;
    public event Action<Guid>? TurnExecuted;
    public event Action<Guid, ESandboxStatus>? ExecutionFinished;

    public Executor(
        IPlaygroundCommandsHandleService mapCommands,
        IMemoryDataManager<StandardPlayground> sandboxRepository,
        IAiActions aiActions,
        IOptions<SandBoxConfiguration> configuration,
        IMemoryDataManager<PlayGroundStatistics> statisticsMemoryRepository,
        IFileDataManager<PlayGroundStatistics> statisticsFileRepository,
        IFileDataManager<StandardPlayground> playgroundFileRepository,
        IFileDataManager<PlaygroundHistoryData> playgroundHistoryDataFileRepository)
    {
        _playgroundCommands = mapCommands;
        _playgroundRepository = sandboxRepository;
        _aiActions = aiActions;
        _configuration = configuration.Value;
        _statisticsMemoryRepository = statisticsMemoryRepository;
        _statisticsFileRepository = statisticsFileRepository;
        _playgroundHistoryDataFileRepository = playgroundHistoryDataFileRepository;
    }

    public void Run()
    {
        // 1. Create standard map/sandbox
        _sandboxId = _playgroundCommands.CreatePlaygroundCommand.Handle(new CreatePlaygroundCommandParameters(
            _configuration.MapSettings,
            _configuration.Hero,
            _configuration.Enemy
        ));

        StandardPlayground playground = _playgroundRepository.LoadObject(_sandboxId);

        // Invoke game started event with sandboxId
        OnGameStarted(playground);

        // Subscribe to game end events
        _aiActions.LostGame += OnGameLost;
        _aiActions.WinGame += OnGameWon;

        // 2. Endless cycle with turn-based execution
        while (sandboxStatus == ESandboxStatus.InProgress && playground.Turn < _configuration.MaxTurns)
        {
            playground.NextTurn();

            // 3. Execute agent actions
            ExecuteTurn(playground);

            // Save the updated sandbox state
            _playgroundRepository.AddOrUpdate(_sandboxId, playground);

            // Wait for the configured turn timeout
            Thread.Sleep(_configuration.TurnTimeout);

            // 5. Invoke end turn event
            OnTurnEnded(playground);

            // 4. Check if max turns reached
            if (playground.Turn >= _configuration.MaxTurns)
            {
                sandboxStatus = ESandboxStatus.TurnLimitReached;
                OnGameEndedByMaxTurns();
            }
        }

        // Cleanup
        _aiActions.LostGame -= OnGameLost;
        _aiActions.WinGame -= OnGameWon;
    }

    private void ExecuteTurn(StandardPlayground playground)
    {
        // Execute hero action with playground ID
        playground.PrepareAgentForTurnActions(playground.Hero);
        List<Coordinates> heroPath = _aiActions.Action(playground.Hero, playground.Id);
        if (heroPath.Count > 0 && sandboxStatus == ESandboxStatus.InProgress)
            playground.MoveObjectOnMap(playground.Hero.Coordinates, heroPath);

        // Execute enemy actions with playground ID
        foreach (var enemy in playground.Enemies.OrderBy(e => e.OrderInTurnQueue))
        {
            playground.PrepareAgentForTurnActions(enemy);
            List<Coordinates> enemyPath = _aiActions.Action(enemy, playground.Id);
            if (enemyPath.Count > 0)
                playground.MoveObjectOnMap(enemy.Coordinates, enemyPath);
        }

        playground.LookAroundEveryone();
    }

    private void OnGameLost(Guid playgroundId)
    {
        // Check if the event is for the current playground
        if (playgroundId == _sandboxId)
        {
            sandboxStatus = ESandboxStatus.HeroLost;
            // Fix: Use null-conditional operator and direct invocation instead of EndInvoke
            ExecutionFinished?.Invoke(playgroundId, ESandboxStatus.HeroLost);
        }
    }

    private void OnGameWon(Guid playgroundId)
    {
        // Check if the event is for the current playground
        if (playgroundId == _sandboxId)
        {
            sandboxStatus = ESandboxStatus.HeroWon;
            ExecutionFinished?.Invoke(_sandboxId, ESandboxStatus.HeroWon);
        }
    }

    private void OnGameEndedByMaxTurns()
    {
        // Handle max turns reached logic
        ExecutionFinished?.Invoke(_sandboxId, ESandboxStatus.TurnLimitReached);
    }

    private void Save(StandardPlayground playground)
    {

        var dataToSave = playground.GetCurrentState();
        if (playground.Turn <= 1)
        {
            var historyData = new PlaygroundHistoryData
            {
                Id = playground.Id,
                States = new List<PlaygroundState> { dataToSave }
            };
            _playgroundHistoryDataFileRepository.AddOrUpdate(playground.Id, historyData);
            return;
        }

        PlaygroundHistoryData previousData = _playgroundHistoryDataFileRepository.LoadObject(playground.Id);

        previousData.States.Add(dataToSave);
        _playgroundHistoryDataFileRepository.AddOrUpdate(playground.Id, previousData);
    }

    protected virtual void OnGameStarted(StandardPlayground playground)
    {
        Save(playground);
        playground.LookAroundEveryone();

        GameStarted?.Invoke(playground.Id);
    }

    protected virtual void OnTurnEnded(StandardPlayground playground)
    {
        Save(playground);
        Turn++;
        TurnExecuted?.Invoke(playground.Id);
    }
}