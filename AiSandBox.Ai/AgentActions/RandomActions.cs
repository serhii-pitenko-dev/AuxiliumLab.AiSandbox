using AiSandBox.Domain.Agents.Entities;
using AiSandBox.Domain.Maps;
using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.Ai.AgentActions;

public class RandomActions : IAiActions
{
    private readonly Random _random = new();
    
    public event Action<Guid>? LostGame;

    public event Action<Guid>? WinGame;

    public List<Coordinates> Action(Agent agent, Guid playgroundId)
    {
        // Calculate the path without modifying the agent
        List<Coordinates> path = CalculatePath(agent, playgroundId);
        
        // Randomly decide whether to use abilities (without actually applying them)
        if (_random.NextDouble() < 0.5) // 50% chance
        {
            UseAbilities(agent, Enum.GetValues<EAbility>());
        }
        
        return path;
    }

    public void Move(Agent agent, Guid playgroundId)
    {
        // This method kept for interface compatibility but not used in new logic
        // You can remove this if you update the interface to not require it
    }

    public void UseAbilities(Agent agent, EAbility[] abilities)
    {
        if (_random.NextDouble() < 0.1) // 10% chance
        {
            agent.ActivateAbilities(abilities);
        }
        if (_random.NextDouble() < 0.1) // 10% chance
        {
            agent.DeActivateAbility(abilities);
        }
    }

    private List<Coordinates> CalculatePath(Agent agent, Guid playgroundId)
    {
        List<Coordinates> path = new();
        
        if (agent.VisibleCells.Count == 0)
        {
            return path; // Return empty path if no visible cells
        }

        // Get random number of moves based on agent's speed
        int numberOfMoves = _random.Next(0, agent.Speed + 1);
        Coordinates currentPosition = agent.Coordinates;

        for (int i = 0; i < numberOfMoves; i++)
        {
            var nextCoordinate = CalculateNextMove(agent, currentPosition, playgroundId, out bool shouldStop);
            
            if (nextCoordinate.HasValue)
            {
                path.Add(nextCoordinate.Value);
                currentPosition = nextCoordinate.Value;
            }
            
            if (shouldStop)
            {
                break;
            }
        }

        return path;
    }

    private Coordinates? CalculateNextMove(Agent agent, Coordinates currentPosition, Guid playgroundId, out bool shouldStop)
    {
        shouldStop = false;

        // Filter for neighboring cells only (Manhattan distance = 1)
        var neighboringCells = agent.VisibleCells
            .Where(cell => Math.Max(Math.Abs(cell.Coordinates.X - currentPosition.X), 
                                   Math.Abs(cell.Coordinates.Y - currentPosition.Y)) == 1)
            .ToList();

        if (neighboringCells.Count == 0)
        {
            return null; // No neighboring cells available
        }

        // Get a random neighboring cell
        Cell targetCell = neighboringCells[_random.Next(neighboringCells.Count)];
        
        // Check if move is valid based on agent type
        if (agent is Hero)
        {
            return ValidateHeroMove(targetCell, playgroundId, out shouldStop);
        }
        else if (agent is Enemy)
        {
            return ValidateEnemyMove(targetCell, playgroundId, out shouldStop);
        }

        return null;
    }

    private Coordinates? ValidateHeroMove(Cell targetCell, Guid playgroundId, out bool shouldStop)
    {
        shouldStop = false;

        // Hero: if block, don't move; if enemy, invoke LostGame; if exit, invoke WinGame
        if (targetCell.Object.Type == ECellType.Block)
        {
            return null; // Invalid move, don't add to path
        }
        else if (targetCell.Object.Type == ECellType.Enemy)
        {
            OnLostGame(playgroundId);
            shouldStop = true;
            return null; // Stop calculating path after game loss
        }
        else if (targetCell.Object.Type == ECellType.Exit)
        {
            OnWinGame(playgroundId);
            shouldStop = true;
            return targetCell.Coordinates; // Add exit to path, then stop
        }
        else
        {
            // Valid move
            return targetCell.Coordinates;
        }
    }

    private Coordinates? ValidateEnemyMove(Cell targetCell, Guid playgroundId, out bool shouldStop)
    {
        shouldStop = false;

        // Enemy: if block, enemy, or exit, don't move; if hero, invoke LostGame
        if (targetCell.Object.Type == ECellType.Block || 
            targetCell.Object.Type == ECellType.Enemy || 
            targetCell.Object.Type == ECellType.Exit)
        {
            return null; // Invalid move, don't add to path
        }
        else if (targetCell.Object.Type == ECellType.Hero)
        {
            OnLostGame(playgroundId);
            shouldStop = true;
            return null; // Stop calculating path after game loss
        }
        else
        {
            // Valid move (only Empty cells)
            return targetCell.Coordinates;
        }
    }

    protected virtual void OnLostGame(Guid playgroundId)
    {
        LostGame?.Invoke(playgroundId);
    }

    protected virtual void OnWinGame(Guid playgroundId)
    {
        WinGame?.Invoke(playgroundId);
    }
}