using AiSandBox.Ai.AgentActions;
using AiSandBox.ApplicationServices.Commands.Playground;
using AiSandBox.ApplicationServices.Commands.Playground.CreatePlayground;
using AiSandBox.Common.MessageBroker;
using AiSandBox.Common.MessageBroker.Contracts.AiContract.Commands;
using AiSandBox.Common.MessageBroker.Contracts.AiContract.Responses;
using AiSandBox.Common.MessageBroker.Contracts.CoreServicesContract.Events;
using AiSandBox.Common.MessageBroker.Contracts.GlobalMessagesContract.Events.Lost;
using AiSandBox.Common.MessageBroker.Contracts.GlobalMessagesContract.Events.Win;
using AiSandBox.Domain.Agents.Entities;
using AiSandBox.Domain.Maps;
using AiSandBox.Domain.Playgrounds;
using AiSandBox.Domain.State;
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

public abstract class Executor: IExecutor
{
    private readonly IPlaygroundCommandsHandleService _playgroundCommands;
    private readonly IMemoryDataManager<StandardPlayground> _playgroundRepository;
    private readonly IFileDataManager<PlaygroundHistoryData> _playgroundHistoryDataFileRepository;
    private readonly SandBoxConfiguration _configuration;
    protected readonly IMessageBroker _messageBroker;
    protected StandardPlayground _playground;
    protected List<Agent> _agentsToAct = new();
    protected IMemoryDataManager<AgentState> _agentStateMemoryRepository;
    protected IBrokerRpcClient _brokerRpcClient;
    protected IAiActions _aiActions;

    private SandboxStatus sandboxStatus;

    public Executor(
        IPlaygroundCommandsHandleService mapCommands,
        IMemoryDataManager<StandardPlayground> sandboxRepository,
        IAiActions aiActions,
        IOptions<SandBoxConfiguration> configuration,
        IMemoryDataManager<PlayGroundStatistics> statisticsMemoryRepository,
        IFileDataManager<PlayGroundStatistics> statisticsFileRepository,
        IFileDataManager<StandardPlayground> playgroundFileRepository,
        IFileDataManager<PlaygroundHistoryData> playgroundHistoryDataFileRepository,
        IMemoryDataManager<AgentState> agentStateMemoryRepository,
        IMessageBroker messageBroker,
        IBrokerRpcClient brokerRpcClient)
    {
        _playgroundCommands = mapCommands;
        _playgroundRepository = sandboxRepository;
        _configuration = configuration.Value;
        _playgroundHistoryDataFileRepository = playgroundHistoryDataFileRepository;
        _agentStateMemoryRepository = agentStateMemoryRepository;
        _messageBroker = messageBroker;
        _brokerRpcClient = brokerRpcClient;
        _aiActions = aiActions;
    }

    public async Task Run()
    {
        // Create standard map/sandbox and save it
        var sandboxId = _playgroundCommands.CreatePlaygroundCommand.Handle(new CreatePlaygroundCommandParameters(
            _configuration.MapSettings,
            _configuration.Hero,
            _configuration.Enemy
        ));

        // Load the created sandbox
        _playground = _playgroundRepository.LoadObject(sandboxId);

        // Invoke game started events for playground
        await StartSimulationPreparations();
    }

    /// <summary>
    /// On start simulation actions
    /// </summary>
    protected virtual async Task StartSimulationPreparations()
    {
        //Lets save immidiately the initial state
        Save();

        // Initialize AI modulef
        _aiActions.Initialize();

        // Let all agents look around initially
        _playground.LookAroundEveryone();

        // Notify everyone that the simulation has started
       var result = 
            await _brokerRpcClient.RequestAsync<GameStartedEvent, AiReadyToActionsResponse>(new GameStartedEvent(Guid.NewGuid(), _playground.Id));

        CancellationToken cancellationToken = new CancellationToken();
        await StartSimulation(cancellationToken);
    }

    protected async Task StartSimulation(CancellationToken cancellationToken)
    {
        while (sandboxStatus == SandboxStatus.InProgress)
        {
            if (_playground.Turn >= _configuration.MaxTurns)
                _messageBroker.Publish(new HeroLostEvent(Guid.NewGuid(), _playground.Id, LostReason.MaxTurnsReached));
            
            await ExecuteTurn(cancellationToken);
        }
    }

    private async Task ExecuteTurn(CancellationToken cancellationToken)
    {
        _agentsToAct = _playground.GetOrderedAgentsForTurn();

        while (_agentsToAct.Count > 0 && sandboxStatus == SandboxStatus.InProgress)
        {
            var agent = _agentsToAct[0];
            _playground.PrepareAgentForTurnActions(agent);
            while (agent.AvailableActions.Count > 0 && sandboxStatus == SandboxStatus.InProgress)
            {
                
                AgentDecisionBaseResponse agentDecision = await SendAgentActionRequest(agent, _playground.Id, cancellationToken);
                ApplyAgentAction(agentDecision);

                #if CONSOLE_PRESENTATION_DEBUG
                //just block thread for 1 second to see the actions in console presentation
                Task.Delay(_configuration.TurnTimeout).Wait();
                #endif
            }
            _agentsToAct.RemoveAt(0);
        }
    }

    private async Task<AgentDecisionBaseResponse> SendAgentActionRequest(Agent agent, Guid playgroundId, CancellationToken cancellationToken)
    {
        // Convert agent data to message format
        var visibleCells = agent.VisibleCells.Select(cell => new VisibleCellData(
            cell.Coordinates,
            cell.Object.Type,
            cell.Object.Id,
            cell.Object.Transparent
        )).ToList();

        var agentState = new AgentState(
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

   
    protected virtual void OnTurnEnded()
    {
        _playground.OnEndTurnActions();
        Save();
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
        if (agentType is ObjectType.Enemy  && moveTo.Object.Type == ObjectType.Hero)
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
}