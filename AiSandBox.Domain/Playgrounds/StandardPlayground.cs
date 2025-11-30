using AiSandBox.Domain.Agents.Entities;
using AiSandBox.Domain.Agents.Services.Vision;
using AiSandBox.Domain.InanimateObjects;
using AiSandBox.Domain.Maps;
using AiSandBox.Domain.State;
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

    public void NextTurn()
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

    public void PrepareAgentForTurnActions(Agent agent)
    {
        agent.GetReadyForNewTurn();
    }

    public PlaygroundState GetCurrentState()
    {
        return new PlaygroundState(Turn, Id, Hero, Enemies.ToList());
    }

    public void PlaceHero(Hero hero)
    {
        Hero = hero;
        _map.PlaceObject(hero);
    }

    public void PlaceEnemy(Enemy enemy)
    {
        _enemies.Add(enemy);
        _map.PlaceObject(enemy);

    }

    public void PlaceExit(Exit exit)
    {
        Exit = exit;
        _map.PlaceObject(exit);
    }

    public void AddBlock(Block block)
    {
        _blocks.Add(block);
        _map.PlaceObject(block);
    } 

    public void MoveObjectOnMap(Coordinates from, List<Coordinates> path)
    {
        _map.MoveObject(from, path);
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
}