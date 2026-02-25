using AuxiliumLab.AiSandbox.Domain.Agents.Entities;
using AuxiliumLab.AiSandbox.Domain.Agents.Factories;
using AuxiliumLab.AiSandbox.Domain.Agents.Services.Vision;
using AuxiliumLab.AiSandbox.Domain.InanimateObjects;
using AuxiliumLab.AiSandbox.Domain.Maps;
using AuxiliumLab.AiSandbox.SharedBaseTypes.ValueObjects;

namespace AuxiliumLab.AiSandbox.Domain.Playgrounds.Builders;

public class PlaygroundBuilder(
    IEnemyFactory EnemyFactory,
    IHeroFactory HeroFactory,
    IVisibilityService visibilityService) : IPlaygroundBuilder
{
    private StandardPlayground? _playground;
    private int _enemyOrderCounter = 1; // Start enemy orders from 1
    private Guid? _playgroundId;
    private int? _turn;

    public int Test { get; set; } = 0;

    public StandardPlayground Playground
    {
        get
        {
            if (_playground == null)
            {
                throw new InvalidOperationException("Sandbox has not been initialized. Call SetMap first.");
            }

            return _playground;
        }

        private set => _playground = value;
    }

    public IPlaygroundBuilder SetMap(MapSquareCells tileMap)
    {
        Playground = new StandardPlayground(tileMap, visibilityService, _playgroundId, _turn);
        _playgroundId = Playground.Id;
        _enemyOrderCounter = 1; // Reset counter when creating new playground

        return this;
    }

    public IPlaygroundBuilder SetPlaygroundId(Guid id)
    {
        _playgroundId = id;
        
        // If playground already exists, apply immediately
        if (_playground != null)
        {
            var idProperty = typeof(StandardPlayground).GetProperty(nameof(StandardPlayground.Id));
            idProperty?.SetValue(Playground, id);
        }

        return this;
    }

    public IPlaygroundBuilder SetTurn(int turn)
    {
        _turn = turn;
        
        // If playground already exists, apply immediately
        if (_playground != null)
        {
            var turnProperty = typeof(StandardPlayground).GetProperty(nameof(StandardPlayground.Turn));
            turnProperty?.SetValue(Playground, turn);
        }

        return this;
    }

    public IPlaygroundBuilder PlaceBlocks(int blocksCount)
    {
        Test++;
        var random = new Random();
        var occupiedCells = new HashSet<(int x, int y)>();

        while (Playground.Blocks.Count < blocksCount)
        {
            int x = random.Next(0, Playground.MapWidth);
            int y = random.Next(0, Playground.MapHeight);

            // Skip if cell is already occupied
            if (occupiedCells.Contains((x, y)))
                continue;

            // Don't place blocks at corners to prevent closed areas
            if (x == 0 && y == 0 ||
                x == 0 && y == Playground.MapHeight - 1 ||
                x == Playground.MapWidth - 1 && y == 0 ||
                x == Playground.MapWidth - 1 && y == Playground.MapHeight - 1)
                continue;

            // Check if placing a block here would create a closed area
            if (!WouldCreateClosedArea(x, y, occupiedCells))
            {
                var block = new Block(Guid.NewGuid());
                Playground.AddBlock(block, new Coordinates(x, y));
                occupiedCells.Add((x, y));
            }
        }

        return this;
    }

    public IPlaygroundBuilder PlaceBlock(Block block, Coordinates coordinates)
    {
        Playground.AddBlock(block, coordinates);
        return this;
    }

    public IPlaygroundBuilder PlaceHero(InitialAgentCharacters heroCharacters)
    {
        var random = new Random();
        int x = 0; // First column (leftmost X value)
        int y;

        do
        {
            y = random.Next(0, Playground.MapHeight);
        } while (IsCellOccupied(x, y));

        Playground.PlaceHero(HeroFactory.CreateHero(heroCharacters), new Coordinates(x, y));

        return this;
    }

    public IPlaygroundBuilder PlaceHero(Hero hero, Coordinates coordinates)
    {
        Playground.PlaceHero(hero, coordinates);
        return this;
    }

    public IPlaygroundBuilder PlaceExit()
    {
        var random = new Random();
        int x = Playground.MapWidth - 1; // Last column (rightmost X value)
        int y;

        do
        {
            y = random.Next(0, Playground.MapHeight);
        } while (IsCellOccupied(x, y));

        Playground.PlaceExit(new Exit(Guid.NewGuid()), new Coordinates(x, y));

        return this;
    }

    public IPlaygroundBuilder PlaceExit(Exit exit, Coordinates coordinates)
    {
        Playground.PlaceExit(exit, coordinates);
        return this;
    }

    public IPlaygroundBuilder PlaceEnemies(int enemiesCount, InitialAgentCharacters enemyCharacters)
    {
        var random = new Random();

        for (int i = 0; i < enemiesCount; i++)
        {
            int x, y;
            bool validPosition;

            do
            {
                x = random.Next(0, Playground.MapWidth);
                y = random.Next(0, Playground.MapHeight);
                validPosition = !IsCellOccupied(x, y) && IsDistanceFromHeroValid(x, y);
            } while (!validPosition);

            var cell = Playground.GetCell(x, y);
            var enemy = EnemyFactory.CreateEnemy(enemyCharacters);
            enemy.SetOrderInTurnQueue(_enemyOrderCounter++); // Assign and increment order

            Playground.PlaceEnemy(enemy, new Coordinates(x, y));
        }

        return this;
    }

    public IPlaygroundBuilder PlaceEnemy(Enemy enemy, Coordinates coordinates)
    {
        Playground.PlaceEnemy(enemy, coordinates);
        return this;
    }

    public IPlaygroundBuilder FillCellGrid()
    {
        for (int x = 0; x < Playground.MapWidth; x++)
        {
            for (int y = 0; y < Playground.MapHeight; y++)
            {
                var cell = Playground.GetCell(x, y);

                // Check for blocks
                var block = Playground.Blocks.FirstOrDefault(b => b.Coordinates.X == x && b.Coordinates.Y == y);
                if (block != null)
                {
                    cell.PlaceObjectToThisCell(block);
                    block.UpdateCell(cell);
                    continue;
                }

                // Check for hero
                if (Playground.Hero != null && Playground.Hero.Coordinates.X == x && Playground.Hero.Coordinates.Y == y)
                {
                    cell.PlaceObjectToThisCell(Playground.Hero);
                    Playground.Hero.UpdateCell(cell);
                    continue;
                }

                // Check for exit
                if (Playground.Exit != null && Playground.Exit.Coordinates.X == x && Playground.Exit.Coordinates.Y == y)
                {
                    cell.PlaceObjectToThisCell(Playground.Exit);
                    Playground.Exit.UpdateCell(cell);
                    continue;
                }

                // Check for enemies
                var enemy = Playground.Enemies.FirstOrDefault(e => e.Coordinates.X == x && e.Coordinates.Y == y);
                if (enemy != null)
                {
                    cell.PlaceObjectToThisCell(enemy);
                    enemy.UpdateCell(cell);
                    continue;
                }

                // If no object found, create empty cell
                var emptyCell = new EmptyCell(cell);
                cell.PlaceObjectToThisCell(emptyCell);
            }
        }

        return this;
    }

    public StandardPlayground Build()
    {
        Playground.LookAroundEveryone();

        return Playground;
    }

    private bool IsCellOccupied(int x, int y)
    {
        // Check blocks
        if (Playground.Blocks.Any(b => b.Coordinates.X == x && b.Coordinates.Y == y))
            return true;

        // Check hero
        if (Playground.Hero != null && Playground.Hero.Coordinates.X == x && Playground.Hero.Coordinates.Y == y)
            return true;

        // Check exit
        if (Playground.Exit != null && Playground.Exit.Coordinates.X == x && Playground.Exit.Coordinates.Y == y)
            return true;

        // Check enemies
        if (Playground.Enemies.Any(e => e.Coordinates.X == x && e.Coordinates.Y == y))
            return true;

        return false;
    }

    private bool IsDistanceFromHeroValid(int x, int y)
    {
        // Calculate Manhattan distance from hero
        int distance = Math.Abs(x - Playground.Hero.Coordinates.X) + Math.Abs(y - Playground.Hero.Coordinates.Y);
        return distance >= 4;
    }

    private bool WouldCreateClosedArea(int x, int y, HashSet<(int x, int y)> occupiedCells)
    {
        // Temporarily add the new block
        var tempOccupied = new HashSet<(int x, int y)>(occupiedCells)
        {
            (x, y)
        };

        // Check if there's still a path from bottom-left to top-right (Cartesian coordinates)
        var visited = new HashSet<(int x, int y)>();
        var queue = new Queue<(int x, int y)>();
        queue.Enqueue((0, Playground.MapHeight - 1)); // Start from top-left in Cartesian (0, Height-1)

        while (queue.Count > 0)
        {
            var (curX, curY) = queue.Dequeue();
            if (curX == Playground.MapWidth - 1 && curY == 0) // End at bottom-right in Cartesian (Width-1, 0)
                return false; // Path exists, no closed area

            var neighbors = new[]
            {
                (curX + 1, curY),
                (curX - 1, curY),
                (curX, curY + 1),
                (curX, curY - 1)
            };

            foreach (var (nextX, nextY) in neighbors)
            {
                if (nextX >= 0 && nextX < Playground.MapWidth &&
                    nextY >= 0 && nextY < Playground.MapHeight &&
                    !tempOccupied.Contains((nextX, nextY)) &&
                    !visited.Contains((nextX, nextY)))
                {
                    visited.Add((nextX, nextY));
                    queue.Enqueue((nextX, nextY));
                }
            }
        }

        return true; // No path found, would create closed area
    }
}