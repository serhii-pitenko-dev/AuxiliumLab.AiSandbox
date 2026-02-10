using AiSandBox.Domain.Agents.Entities;
using AiSandBox.Domain.Agents.Services.Vision;
using AiSandBox.Domain.InanimateObjects;
using AiSandBox.Domain.Maps;
using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.Domain.Playgrounds;
    
public class StandardPlayground
{
    public int Turn { get; private set; } = 0;
    public Guid Id { get; init; } = Guid.NewGuid();
    public Hero? Hero { get; private set; }
    public Exit? Exit { get; private set; }
    public IReadOnlyCollection<Block> Blocks => _blocks.AsReadOnly();
    public IReadOnlyCollection<Enemy> Enemies => _enemies.AsReadOnly();
    public int MapWidth => _map.Width;
    public int MapHeight => _map.Height;
    public int MapArea => _map.Area;
    private readonly IVisibilityService _visibilityService;
    private readonly List<Block> _blocks = [];
    private readonly List<Enemy> _enemies = [];
    private readonly MapSquareCells _map;

    public StandardPlayground(MapSquareCells map, IVisibilityService visibilityService)
    {
        _map = map;
        _visibilityService = visibilityService ?? throw new ArgumentNullException(nameof(visibilityService));
    }

    public void OnStartTurnActions()
    {
        Turn++;
    }

    public void LookAroundEveryone()
    {
        _visibilityService.UpdateVisibleCells(Hero, this);

        foreach (var enemy in Enemies)
        {
            _visibilityService.UpdateVisibleCells(enemy, this);
        }
    }

    public void UpdateAgentVision(Agent agent)
    {
        _visibilityService.UpdateVisibleCells(agent, this);
    }

    public void PrepareAgentForTurnActions(Agent agent)
    {
        agent.GetReadyForNewTurn();
        agent.ReCalculateAvailableActions();
    }

    public void PlaceHero(Hero hero, Coordinates coordinates)
    {
        Hero = hero;
        _map.PlaceObject(hero, coordinates);
    }

    public void PlaceEnemy(Enemy enemy, Coordinates coordinates)
    {
        _enemies.Add(enemy);
        _map.PlaceObject(enemy, coordinates);
    }

    public void PlaceExit(Exit exit, Coordinates coordinates)
    {
        Exit = exit;
        _map.PlaceObject(exit, coordinates);
    }

    public void AddBlock(Block block, Coordinates coordinates)
    {
        _blocks.Add(block);
        _map.PlaceObject(block, coordinates);
    }

    /// <summary>
    /// Move agent and update vision
    /// </summary>
    public void MoveObjectOnMap(Coordinates from, Coordinates to)
    {
        var cell = _map.CellGrid[from.X, from.Y];
        var targetCell = _map.MoveObject(from, to);
        if (targetCell.Object is Agent agent)
        {
            UpdateAgentVision(agent);
        }
    }

    public Cell[,] CutMapPart(Coordinates center, int radius)
    {
        return _map.CutOutPartOfTheMap(center, radius);
    }

    public Cell GetCell(Coordinates coordinates)
    {
        return _map.CellGrid[coordinates.X, coordinates.Y];
    }

    public Cell GetCell(int x, int y)
    {
        if (x < 0 || x >= _map.Width || y < 0 || y >= _map.Height)
            throw new ArgumentOutOfRangeException($"Coordinates ({x}, {y}) are out of bounds.");
        
        return _map.CellGrid[x, y];
    }

    /// <summary>
    /// Get all agents in the order they should act this turn. Hero first, then enemies by their order in turn queue.
    /// </summary>
    /// <returns>List of agents for the current turn.</returns>
    public List<Agent> GetOrderedAgentsForTurn()
    {
        var agents = new List<Agent>();
        if (Hero != null)
            agents.Add(Hero);
        agents.AddRange(Enemies.OrderBy(e => e.OrderInTurnQueue));

        return agents;
    }
}