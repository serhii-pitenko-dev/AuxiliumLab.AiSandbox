using AiSandBox.Ai.AgentActions;
using AiSandBox.ApplicationServices.Commands.Playground;
using AiSandBox.ApplicationServices.Commands.Playground.CreatePlayground;
using AiSandBox.ApplicationServices.Runner.Logs;
using AiSandBox.ApplicationServices.Runner.Logs.Performance;
using AiSandBox.ApplicationServices.Saver.Persistence.Sandbox.Mappers;
using AiSandBox.ApplicationServices.Saver.Persistence.Sandbox.States;
using AiSandBox.Common.MessageBroker;
using AiSandBox.Common.MessageBroker.Contracts.AiContract.Commands;
using AiSandBox.Common.MessageBroker.Contracts.AiContract.Responses;
using AiSandBox.Common.MessageBroker.Contracts.CoreServicesContract.Events;
using AiSandBox.Common.MessageBroker.Contracts.GlobalMessagesContract.Events.Lost;
using AiSandBox.Common.MessageBroker.Contracts.GlobalMessagesContract.Events.Win;
using AiSandBox.Domain.Agents.Entities;
using AiSandBox.Domain.Maps;
using AiSandBox.Domain.Playgrounds;
using AiSandBox.Domain.Statistics.Entities;
using AiSandBox.Infrastructure.Configuration.Preconditions;
using AiSandBox.Infrastructure.FileManager;
using AiSandBox.Infrastructure.MemoryManager;
using AiSandBox.SharedBaseTypes.AiContract.Dto;
using AiSandBox.SharedBaseTypes.ValueObjects;
using Microsoft.Extensions.Options;

#if CONSOLE_PRESENTATION_DEBUG
#warning CONSOLE_PRESENTATION_DEBUG is ON
#else
#warning CONSOLE_PRESENTATION_DEBUG is OFF
#endif

namespace AiSandBox.ApplicationServices.Runner;

public abstract class Executor : IExecutor
{
    private readonly IPlaygroundCommandsHandleService _playgroundCommands;
    private readonly IMemoryDataManager<StandardPlayground> _playgroundRepository;
    private readonly SandBoxConfiguration _configuration;
    protected readonly IMessageBroker _messageBroker;
    protected StandardPlayground _playground;
    protected List<Agent> _agentsToAct = new();
    protected IMemoryDataManager<AgentStateForAIDecision> _agentStateMemoryRepository;
    protected IBrokerRpcClient _brokerRpcClient;
    protected IAiActions _aiActions;
    protected IStandardPlaygroundMapper _standardPlaygroundMapper;
    protected IFileDataManager<StandardPlaygroundState> _playgroundStateFileRepository;
    protected IFileDataManager<RawDataLog> _rawDataLogFileRepository;
    protected IFileDataManager<TurnExecutionPerformance> _turnExecutionPerformanceFileRepository;
    protected IFileDataManager<SandboxExecutionPerformance> _sandboxExecutionPerformanceFileRepository;

    private SandboxStatus sandboxStatus;
    private SandboxExecutionPerformance sandboxExecutionPerformance;


    public Executor(
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
        IFileDataManager<SandboxExecutionPerformance> sandboxExecutionPerformanceFileRepository)
    {
        _playgroundCommands = mapCommands;
        _playgroundRepository = sandboxRepository;
        _configuration = configuration.Value;
        _agentStateMemoryRepository = agentStateMemoryRepository;
        _messageBroker = messageBroker;
        _brokerRpcClient = brokerRpcClient;
        _aiActions = aiActions;
        _standardPlaygroundMapper = standardPlaygroundMapper;
        _playgroundStateFileRepository = playgroundStateFileRepository;
        _rawDataLogFileRepository = rawDataLogFileRepository;
        _turnExecutionPerformanceFileRepository = turnExecutionPerformanceFileRepository;
        _sandboxExecutionPerformanceFileRepository = sandboxExecutionPerformanceFileRepository;
    }

    public async Task RunAsync()
    {

#if PERFORMANCE_ANALYSIS
            sandboxExecutionPerformance = new SandboxExecutionPerformance
            {
                Start = DateTime.UtcNow,
            };
#endif

        // Create standard map/sandbox and save it
        var sandboxId = _playgroundCommands.CreatePlaygroundCommand.Handle(new CreatePlaygroundCommandParameters(
            _configuration.MapSettings,
            _configuration.Hero,
            _configuration.Enemy
        ));

        // Load the created sandbox
        _playground = _playgroundRepository.LoadObject(sandboxId);

        // Invoke game started events for playground
        await StartSimulationPreparationsAsync();
    }

    /// <summary>
    /// On start simulation actions
    /// </summary>
    protected virtual async Task StartSimulationPreparationsAsync()
    {
        // Initialize AI modulef
        _aiActions.Initialize();

        // Let all agents look around initially
        _playground.LookAroundEveryone();
        await SaveAsync();

        // Notify everyone that the simulation has started
        var result =
            await _brokerRpcClient.RequestAsync<GameStartedEvent, AiReadyToActionsResponse>(new GameStartedEvent(Guid.NewGuid(), _playground.Id));

        CancellationToken cancellationToken = new CancellationToken();
        try
        {
           await CreateRawLog($"Playground with id {_playground.Id} started.");

           await StartSimulationAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await CreateRawLog($"Playground with id {_playground.Id} crashed. Exception: {ex.Message}");
            await SaveAsync();
            throw;
        }
    }

    protected async Task StartSimulationAsync(CancellationToken cancellationToken)
    {
        while (sandboxStatus == SandboxStatus.InProgress)
        {
            if (_playground.Turn >= _configuration.MaxTurns)
                _messageBroker.Publish(new HeroLostEvent(Guid.NewGuid(), _playground.Id, LostReason.MaxTurnsReached));

            _playground.OnStartTurnActions();

#if PERFORMANCE_ANALYSIS
            sandboxExecutionPerformance.TurnPerformances[_playground.Turn] = new TurnExecutionPerformance
                {
                    TurnNumber = _playground.Turn,
                    Start = DateTime.UtcNow,
                };
#endif

            await ExecuteTurnAsync(cancellationToken);

#if PERFORMANCE_ANALYSIS
            sandboxExecutionPerformance.TurnPerformances[_playground.Turn].Finish = DateTime.UtcNow;
             await _turnExecutionPerformanceFileRepository.SaveOrAppendAsync(_playground.Id, sandboxExecutionPerformance.TurnPerformances[_playground.Turn]);
#endif

        }
    }

    private async Task ExecuteTurnAsync(CancellationToken cancellationToken)
    {
        _agentsToAct = _playground.GetOrderedAgentsForTurn();

        while (_agentsToAct.Count > 0 && sandboxStatus == SandboxStatus.InProgress)
        {
            var agent = _agentsToAct[0];


            _playground.PrepareAgentForTurnActions(agent);
            while (agent.AvailableActions.Count > 0 && sandboxStatus == SandboxStatus.InProgress)
            {

#if PERFORMANCE_ANALYSIS

                sandboxExecutionPerformance.TurnPerformances[_playground.Turn]
                    .ActionPerformances[agent.Id] = new ActionExecutionPerformance
                    {
                        Start = DateTime.UtcNow,
                        ObjectType = agent.Type,
                    };
#endif

                AgentDecisionBaseResponse agentDecision = await SendAgentActionRequestAsync(agent, _playground.Id, cancellationToken);
                ApplyAgentAction(agentDecision);

#if PERFORMANCE_ANALYSIS
                sandboxExecutionPerformance.TurnPerformances[_playground.Turn]
                    .ActionPerformances[agent.Id].Finish = DateTime.UtcNow;
                sandboxExecutionPerformance.TurnPerformances[_playground.Turn]
                    .ActionPerformances[agent.Id].Action = agentDecision.ActionType;

#endif

            }
            _agentsToAct.RemoveAt(0);
        }
    }

    private async Task<AgentDecisionBaseResponse> SendAgentActionRequestAsync(Agent agent, Guid playgroundId, CancellationToken cancellationToken)
    {
        // Convert agent data to message format
        var visibleCells = agent.VisibleCells.Select(cell => new VisibleCellData(
            cell.Coordinates,
            cell.Object.Type,
            cell.Object.Id,
            cell.Object.Transparent
        )).ToList();

        var agentState = new AgentStateForAIDecision(
            playgroundId,
            agent.Id,
            agent.Type,
            agent.Coordinates,
            agent.Speed,
            agent.SightRange,
            agent.IsRun,
            agent.Stamina,
            agent.MaxStamina,
            visibleCells,
            agent.AvailableActions,
            agent.ExecutedActions
        );

        _agentStateMemoryRepository.AddOrUpdate(agent.Id, agentState);


        return await _brokerRpcClient.RequestAsync<RequestAgentDecisionMakeCommand, AgentDecisionBaseResponse>(
               new RequestAgentDecisionMakeCommand(Guid.NewGuid(), playgroundId, agent.Id), cancellationToken);
    }

    private async Task SaveAsync()
    {
        var dataToSave = _standardPlaygroundMapper.ToState(_playground);
        await _playgroundStateFileRepository.SaveOrAppendAsync(_playground.Id, dataToSave);
    }


    protected virtual void OnTurnEnded()
    {
        _messageBroker.Publish(new TurnExecutedEvent(Guid.NewGuid(), _playground.Id, _playground.Turn));
    }

    private void ApplyAgentAction(AgentDecisionBaseResponse action)
    {
        // Find the agent
        var agent = _playground.Hero.Id == action.AgentId
            ? (Agent)_playground.Hero
            : _playground.Enemies.FirstOrDefault(e => e.Id == action.AgentId);

        if (agent == null)
            return;

        switch (action)
        {
            case AgentDecisionMoveResponse moveEvent when moveEvent.IsSuccess && moveEvent.From != moveEvent.To:
                // Apply movement
                bool isSuccess;
                var result = CheckGameStatusAndMovingPossibility(agent.Type, _playground.GetCell(moveEvent.To));
                if (result.MovePossibility == true && result.GameStatus == SandboxStatus.InProgress)
                {
                    isSuccess = true;
                    _playground.MoveObjectOnMap(moveEvent.From, moveEvent.To);

                }
                else
                {
                    isSuccess = false;
                    sandboxStatus = result.GameStatus;
                }

#if CONSOLE_PRESENTATION_DEBUG
                SendAgentMoveNotification(moveEvent.Id, _playground.Id, agent.Id, moveEvent.From, moveEvent.To, isSuccess, GetAgentSnapshot(agent));
#endif
                break;

            case AgentDecisionUseAbilityResponse abilityEvent when abilityEvent.IsSuccess:
                // Apply ability activation/deactivation
                agent.DoAction(abilityEvent.ActionType, abilityEvent.IsActivated);
#if CONSOLE_PRESENTATION_DEBUG
                SendAgentToggleActionNotification(abilityEvent.ActionType, _playground.Id, agent.Id, abilityEvent.IsActivated, GetAgentSnapshot(agent));
#endif
                break;
        }
    }

    private (SandboxStatus GameStatus, bool MovePossibility) CheckGameStatusAndMovingPossibility(ObjectType agentType, Cell moveTo)
    {
        // Check for agent if target cell is empty
        if ((agentType is ObjectType.Enemy or ObjectType.Hero) && moveTo.Object.Type == ObjectType.Empty)
        {
            return (SandboxStatus.InProgress, true);
        }

        // Check for agent if target cell with block
        if ((agentType is ObjectType.Enemy or ObjectType.Hero) && moveTo.Object.Type == ObjectType.Block)
        {
            return (SandboxStatus.InProgress, false);
        }

        // Check for enemy is target cell with Hero
        if (agentType is ObjectType.Enemy && moveTo.Object.Type == ObjectType.Hero)
        {
            _messageBroker.Publish(new HeroLostEvent(Guid.NewGuid(), _playground.Id, LostReason.HeroCatched));
            sandboxStatus = SandboxStatus.HeroLost;

            return (SandboxStatus.HeroLost, false);
        }

        // Check for Hero is target cell with Exit
        if (agentType is ObjectType.Enemy && moveTo.Object.Type == ObjectType.Exit)
        {
            return (SandboxStatus.InProgress, false);
        }

        // Check for Hero is target cell with Enemy
        if (agentType == ObjectType.Hero && moveTo.Object.Type == ObjectType.Enemy)
        {
            _messageBroker.Publish(new HeroLostEvent(Guid.NewGuid(), _playground.Id, LostReason.HeroCatched));
            sandboxStatus = SandboxStatus.HeroLost;

            return (SandboxStatus.HeroLost, false);
        }

        // Check for Hero is target cell with Exit
        if (agentType == ObjectType.Hero && moveTo.Object.Type == ObjectType.Exit)
        {
            _messageBroker.Publish(new HeroWonEvent(Guid.NewGuid(), _playground.Id, WinReason.ExitReached));
            sandboxStatus = SandboxStatus.HeroWon;

            return (SandboxStatus.HeroWon, false);
        }

        throw new InvalidOperationException("Unknown move scenario");
    }

    protected abstract void SendAgentMoveNotification(
        Guid Id,
        Guid PlaygroundId,
        Guid AgentId,
        Coordinates From,
        Coordinates To,
        bool isSuccess,
        AgentSnapshot agentSnapshot);

    protected abstract void SendAgentToggleActionNotification(
    AgentAction action,
    Guid playgroundId,
    Guid agentId,
    bool isActivated,
    AgentSnapshot agentSnapshot);

    private AgentSnapshot GetAgentSnapshot(Agent agent)
    {
        return new AgentSnapshot(
            agent.Id,
            agent.Type,
            agent.Speed,
            agent.SightRange,
            agent.IsRun,
            agent.Stamina,
            agent.MaxStamina,
            agent.OrderInTurnQueue);
    }

    private async Task CreateRawLog(string logMessage)
    {
        await _rawDataLogFileRepository.SaveOrAppendAsync(
               Guid.NewGuid(), new RawDataLog(Guid.NewGuid(), logMessage, DateTime.UtcNow));
    }
}