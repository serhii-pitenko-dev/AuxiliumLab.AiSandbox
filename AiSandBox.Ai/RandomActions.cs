using AiSandBox.Common.MessageBroker;
using AiSandBox.Common.MessageBroker.Contracts.AiContract.Commands;
using AiSandBox.Common.MessageBroker.Contracts.AiContract.Responses;
using AiSandBox.Common.MessageBroker.Contracts.CoreServicesContract.Events;
using AiSandBox.Infrastructure.MemoryManager;
using AiSandBox.SharedBaseTypes.AiContract.Dto;
using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.Ai.AgentActions;

/// <summary>
/// Should be one instace per one simulation 
/// </summary>
public class RandomActions : IAiActions
{
    private readonly Random _random = new();
    private readonly IMessageBroker _messageBroker;
    protected IMemoryDataManager<AgentState> _agentStateMemoryRepository;

    public RandomActions(IMessageBroker messageBroker, IMemoryDataManager<AgentState> agentStateMemoryRepository)
    {
        _messageBroker = messageBroker;
        _agentStateMemoryRepository = agentStateMemoryRepository;
    }

    public void Initialize()
    {
        _messageBroker.Subscribe<GameStartedEvent>(msg =>
        {
            _messageBroker.Publish(new AiReadyToActionsResponse(Guid.NewGuid(), msg.PlaygroundId, msg.Id));
        });

        _messageBroker.Subscribe<RequestAgentDecisionMakeCommand>(msg =>
        {
            var agent = _agentStateMemoryRepository.LoadObject(msg.AgentId);
            AgentDecisionBaseResponse response = HandleAgentActionMessage(agent, msg.Id);
            _messageBroker.Publish(response);
        });
    }

    private AgentDecisionBaseResponse HandleAgentActionMessage(AgentState agent, Guid correlationId)
    {
        return Action(agent, correlationId);
    }

    private AgentDecisionBaseResponse Action(AgentState agent, Guid correlationId)
    {
        var action = agent.AvailableLimitedActions[Random.Shared.Next(agent.AvailableLimitedActions.Count)];
        switch (action)
        {
            case AgentAction.Move:
                // Calculate the path without modifying the agent
                return CalculatePath(agent, correlationId);
                break;
            case AgentAction.Run:
                // Randomly decide whether to use abilities
                return UseAbilities(agent, action, correlationId);
                break;
            default:
                break;
        }

        throw new NotImplementedException($"Action {action} is not implemented in RandomActions AI.");
    }

    private AgentDecisionBaseResponse UseAbilities(AgentState agentState, AgentAction ability, Guid correlationId)
    {
        bool isActivated = agentState.IsRun;
        bool isSuccess = false;

        if (_random.NextDouble() < 0.1) // 10% chance to activate ability
        {
            if (ability == AgentAction.Run && !agentState.IsRun)
            {
                isActivated = true;
                isSuccess = true;
            }
        }

        if (_random.NextDouble() < 0.1) // 10% chance to deactivate ability
        {
            if (ability == AgentAction.Run && agentState.IsRun)
            {
                isActivated = false;
                isSuccess = true;
            }
        }

        return new AgentDecisionUseAbilityResponse(
            Guid.NewGuid(), 
            agentState.Id, 
            isActivated, 
            ability, 
            correlationId, 
            isSuccess);
    }

    private AgentDecisionMoveResponse CalculatePath(AgentState agentState, Guid correlationId)
    {
        // Get random number of moves based on agent's speed
        int numberOfMoves = _random.Next(0, agentState.Speed + 1);
        Coordinates from = agentState.Coordinates;
        Coordinates to = agentState.Coordinates;
        to = CalculateNextMove(agentState, from);

        return new AgentDecisionMoveResponse(
            Guid.NewGuid(),
            agentState.Id,
            from,
            to,
            correlationId,
            IsSuccess: true);
    }

    private Coordinates CalculateNextMove(AgentState agentState, Coordinates currentPosition)
    {
        if (agentState.VisibleCells.Count == 0)
        {
            return currentPosition; // No visible cells to move to
        }

        // Filter for neighboring cells only (Manhattan distance = 1)
        var neighboringCells = agentState.VisibleCells
            .Where(cell => Math.Max(Math.Abs(cell.Coordinates.X - currentPosition.X),
                                   Math.Abs(cell.Coordinates.Y - currentPosition.Y)) == 1)
            .ToList();

        if (neighboringCells.Count == 0)
        {
            return currentPosition; // No neighboring cells available
        }

        // Get a random neighboring cell
        VisibleCellData targetCell = neighboringCells[_random.Next(neighboringCells.Count)];

        return targetCell.Coordinates;
    }
}