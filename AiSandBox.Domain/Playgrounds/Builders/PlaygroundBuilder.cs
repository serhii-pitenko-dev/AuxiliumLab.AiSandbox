using AiSandBox.Domain.Agents.Entities;
using AiSandBox.Domain.Agents.Factories;
using AiSandBox.Domain.Agents.Services.Vision;
using AiSandBox.Domain.InanimateObjects;
using AiSandBox.Domain.Maps;
using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.Domain.Playgrounds.Builders;

public class PlaygroundBuilder(
    IEnemyFactory EnemyFactory, 
    IHeroFactory HeroFactory, 
    IVisibilityService visibilityService) : IPlaygroundBuilder
{
    private StandardPlayground? _playground;

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
        Playground = new StandardPlayground(tileMap, visibilityService);

        return this;
    }

    public IPlaygroundBuilder PlaceBlocks(int percentOfBlocks)
    {
        int totalBlocks = (int)(Playground.MapArea * (percentOfBlocks / 100.0));
        var random = new Random();
        var occupiedCells = new HashSet<(int x, int y)>();

        while (Playground.Blocks.Count < totalBlocks)
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
                var block = new Block(new Coordinates(x, y), Guid.NewGuid());
                Playground.AddBlock(block);
                occupiedCells.Add((x, y));
            }
        }

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

        Playground.PlaceHero(HeroFactory.CreateHero(new Coordinates(x, y), heroCharacters));

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

        Playground.PlaceExit(new Exit(new Coordinates(x, y), Guid.NewGuid()));
        
        return this;
    }

    public IPlaygroundBuilder PlaceEnemies(int percentOfEnemies, InitialAgentCharacters enemyCharacters)
    {
        var random = new Random();
        int numberOfEnemies = (int)(Playground.MapArea * (percentOfEnemies / 100.0));

        for (int i = 0; i < numberOfEnemies; i++)
        {
            int x, y;
            bool validPosition;

            do
            {
                x = random.Next(0, Playground.MapWidth);
                y = random.Next(0, Playground.MapHeight);
                validPosition = !IsCellOccupied(x, y) && IsDistanceFromHeroValid(x, y);
            } while (!validPosition);

            var enemy = EnemyFactory.CreateEnemy(new Coordinates(x, y), enemyCharacters);

            Playground.PlaceEnemy(enemy);
        }

        return this;
    }

    public IPlaygroundBuilder FillCellGrid()
    {
        for (int x = 0; x < Playground.MapHeight; x++)
        {
            for (int y = 0; y < Playground.MapHeight; y++)
            {
                // Check for blocks
                var block = Playground.Blocks.FirstOrDefault(b => b.Coordinates.X == x && b.Coordinates.Y == y);
                if (block != null)
                {
                    Playground.GetCell(x, y).Object = block;

                    continue;
                }

                // Check for hero
                if (Playground.Hero.Coordinates.X == x && Playground.Hero.Coordinates.Y == y)
                {
                    Playground.GetCell(x, y).Object = Playground.Hero;
                    continue;
                }

                // Check for exit
                if (Playground.Exit.Coordinates.X == x && Playground.Exit.Coordinates.Y == y)
                {
                    Playground.GetCell(x, y).Object = Playground.Exit;
                    continue;
                }

                // Check for enemies
                var enemy = Playground.Enemies.FirstOrDefault(e => e.Coordinates.X == x && e.Coordinates.Y == y);
                if (enemy != null)
                {
                    Playground.GetCell(x, y).Object = enemy;
                    continue;
                }

                // If no object found, create empty cell
                Playground.GetCell(x, y).Object = new EmptyCell(new Coordinates(x, y), Guid.NewGuid());
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
            if (curX == Playground.MapHeight - 1 && curY == 0) // End at bottom-right in Cartesian (Width-1, 0)
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
                if (nextX >= 0 && nextX < Playground.MapHeight &&
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

