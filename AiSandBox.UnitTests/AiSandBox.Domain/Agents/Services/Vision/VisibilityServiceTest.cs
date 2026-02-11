using AiSandBox.Domain.Agents.Entities;
using AiSandBox.Domain.Agents.Services.Vision;
using AiSandBox.Domain.InanimateObjects;
using AiSandBox.Domain.Maps;
using AiSandBox.Domain.Playgrounds;
using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.UnitTests.AiSandBox.Domain.Agents.Services.Vision;

[TestClass]
public class VisibilityServiceTest
{
    private const int MapSize = 21;
    private const int HeroX = 10;
    private const int HeroY = 10;
    private const int HeroSpeed = 5;
    private const int HeroSightRange = 9;
    private const int HeroStamina = 15;

    private StandardPlayground CreatePlayground()
    {
        var map = new MapSquareCells(MapSize, MapSize);
        var visibilityService = new VisibilityService();
        return new StandardPlayground(map, visibilityService);
    }

    private Hero CreateHero()
    {
        var characters = new InitialAgentCharacters(
            Speed: HeroSpeed,
            SightRange: HeroSightRange,
            Stamina: HeroStamina,
            PathToTarget: [],
            AgentActions: [],
            ExecutedActions: [],
            isRun: false,
            orderInTurnQueue: 0
        );
        return new Hero(characters, Guid.NewGuid());
    }

    private Enemy CreateEnemy(int sightRange = 3)
    {
        var characters = new InitialAgentCharacters(
            Speed: 3,
            SightRange: sightRange,
            Stamina: 10,
            PathToTarget: [],
            AgentActions: [],
            ExecutedActions: [],
            isRun: false,
            orderInTurnQueue: 0
        );
        return new Enemy(characters, Guid.NewGuid());
    }

    private bool IsWithinDistance(int x1, int y1, int x2, int y2, int distance)
    {
        int dx = x2 - x1;
        int dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy) <= distance;
    }

    [TestMethod]
    public void UpdateVisibleCells_EmptyMap_HeroSeesAllCellsWithinRange()
    {
        // Arrange
        var playground = CreatePlayground();
        var hero = CreateHero();
        playground.PlaceHero(hero, new Coordinates(HeroX, HeroY));

        // Act
        playground.UpdateAgentVision(hero);

        // Assert
        Assert.IsNotNull(hero.VisibleCells);
        Assert.IsTrue(hero.VisibleCells.Count > 0);

        // Hero should see themselves
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX && c.Coordinates.Y == HeroY),
            "Hero should be able to see their own position");

        // All visible cells should be within sight range
        foreach (var cell in hero.VisibleCells)
        {
            Assert.IsTrue(IsWithinDistance(HeroX, HeroY, cell.Coordinates.X, cell.Coordinates.Y, HeroSightRange),
                $"Cell ({cell.Coordinates.X}, {cell.Coordinates.Y}) is outside sight range");
        }

        // Verify cardinal directions at max range
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX && c.Coordinates.Y == HeroY + HeroSightRange),
            "Hero should see north at max range");
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX && c.Coordinates.Y == HeroY - HeroSightRange),
            "Hero should see south at max range");
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX + HeroSightRange && c.Coordinates.Y == HeroY),
            "Hero should see east at max range");
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX - HeroSightRange && c.Coordinates.Y == HeroY),
            "Hero should see west at max range");
    }

    [TestMethod]
    public void UpdateVisibleCells_SingleBlockDirectlyNorth_BlocksShadowBehind()
    {
        // Arrange
        var playground = CreatePlayground();
        var hero = CreateHero();
        playground.PlaceHero(hero, new Coordinates(HeroX, HeroY));

        var block = new Block(Guid.NewGuid());
        playground.AddBlock(block, new Coordinates(HeroX, HeroY + 1));

        // Act
        playground.UpdateAgentVision(hero);

        // Assert
        Assert.IsNotNull(hero.VisibleCells);

        // Hero can see the block itself
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX && c.Coordinates.Y == HeroY + 1),
            "Hero should see the blocking cell at (10, 11)");

        // Hero cannot see cells directly behind the block
        Assert.IsFalse(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX && c.Coordinates.Y == HeroY + 2),
            "Hero should NOT see cell directly behind block at (10, 12)");
        Assert.IsFalse(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX && c.Coordinates.Y == HeroY + 5),
            "Hero should NOT see cell behind block at (10, 15)");

        // Hero CAN see cells diagonally adjacent to their position (not blocked)
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX + 1 && c.Coordinates.Y == HeroY + 1),
            "Hero should see diagonal adjacent cell (11, 11)");
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX - 1 && c.Coordinates.Y == HeroY + 1),
            "Hero should see diagonal adjacent cell (9, 11)");

        // Hero CANNOT see cells that Bresenham line passes through the block to reach
        // Line from (10,10) to (11,12) passes through (10,11) which is blocked
        Assert.IsFalse(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX + 1 && c.Coordinates.Y == HeroY + 2),
            "Hero should NOT see (11, 12) - line passes through blocking cell");
        Assert.IsFalse(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX - 1 && c.Coordinates.Y == HeroY + 2),
            "Hero should NOT see (9, 12) - line passes through blocking cell");

        // Hero CAN see cells at diagonal angles that don't pass through the block
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX + 2 && c.Coordinates.Y == HeroY + 1),
            "Hero should see (12, 11) - line doesn't pass through block");
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX - 2 && c.Coordinates.Y == HeroY + 1),
            "Hero should see (8, 11) - line doesn't pass through block");
    }

    [TestMethod]
    public void UpdateVisibleCells_HorizontalWall_CreatesShadowBehind()
    {
        // Arrange
        var playground = CreatePlayground();
        var hero = CreateHero();
        playground.PlaceHero(hero, new Coordinates(HeroX, HeroY));

        // Create horizontal wall at y = 13
        int wallY = HeroY + 3;
        for (int x = HeroX - 3; x <= HeroX + 3; x++)
        {
            var block = new Block(Guid.NewGuid());
            playground.AddBlock(block, new Coordinates(x, wallY));
        }

        // Act
        playground.UpdateAgentVision(hero);

        // Assert
        Assert.IsNotNull(hero.VisibleCells);

        // Hero can see the wall
        for (int x = HeroX - 3; x <= HeroX + 3; x++)
        {
            Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == x && c.Coordinates.Y == wallY),
                $"Hero should see wall at ({x}, {wallY})");
        }

        // Hero cannot see most cells directly behind the wall center
        Assert.IsFalse(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX && c.Coordinates.Y == wallY + 1),
            $"Hero should NOT see behind wall at ({HeroX}, {wallY + 1})");
        Assert.IsFalse(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX && c.Coordinates.Y == wallY + 3),
            $"Hero should NOT see behind wall at ({HeroX}, {wallY + 3})");

        // Hero can still see cells at edges beyond wall range
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X < HeroX - 3 || c.Coordinates.X > HeroX + 3),
            "Hero should see cells outside wall shadow");
    }

    [TestMethod]
    public void UpdateVisibleCells_DiagonalBlock_BlocksDiagonalLine()
    {
        // Arrange
        var playground = CreatePlayground();
        var hero = CreateHero();
        playground.PlaceHero(hero, new Coordinates(HeroX, HeroY));

        var block = new Block(Guid.NewGuid());
        playground.AddBlock(block, new Coordinates(HeroX + 4, HeroY + 4));

        // Act
        playground.UpdateAgentVision(hero);

        // Assert
        Assert.IsNotNull(hero.VisibleCells);

        // Hero can see the block
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX + 4 && c.Coordinates.Y == HeroY + 4),
            "Hero should see the diagonal block");

        // Hero cannot see cells on the same diagonal beyond the block
        Assert.IsFalse(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX + 5 && c.Coordinates.Y == HeroY + 5),
            "Hero should NOT see (15, 15) behind diagonal block");
        Assert.IsFalse(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX + 6 && c.Coordinates.Y == HeroY + 6),
            "Hero should NOT see (16, 16) behind diagonal block");

        // Hero can see adjacent cells not on the blocked diagonal
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX + 4 && c.Coordinates.Y == HeroY + 5),
            "Hero should see adjacent cell (14, 15)");
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX + 5 && c.Coordinates.Y == HeroY + 4),
            "Hero should see adjacent cell (15, 14)");
    }

    [TestMethod]
    public void UpdateVisibleCells_LShapedWall_CreatesComplexShadow()
    {
        // Arrange
        var playground = CreatePlayground();
        var hero = CreateHero();
        playground.PlaceHero(hero, new Coordinates(HeroX, HeroY));

        // Create L-shaped wall
        // Vertical part at x=13, y from 11 to 14
        for (int y = HeroY + 1; y <= HeroY + 4; y++)
        {
            var block = new Block(Guid.NewGuid());
            playground.AddBlock(block, new Coordinates(HeroX + 3, y));
        }
        // Horizontal part at y=14, x from 14 to 16
        for (int x = HeroX + 4; x <= HeroX + 6; x++)
        {
            var block = new Block(Guid.NewGuid());
            playground.AddBlock(block, new Coordinates(x, HeroY + 4));
        }

        // Act
        playground.UpdateAgentVision(hero);

        // Assert
        Assert.IsNotNull(hero.VisibleCells);

        // Hero can see the vertical wall (direct line of sight)
        for (int y = HeroY + 1; y <= HeroY + 4; y++)
        {
            Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX + 3 && c.Coordinates.Y == y),
                $"Hero should see vertical wall at ({HeroX + 3}, {y})");
        }

        // The vertical wall blocks the horizontal wall segments from hero's position
        int blockedHorizontalWallCells = 0;
        for (int x = HeroX + 4; x <= HeroX + 6; x++)
        {
            if (!hero.VisibleCells.Any(c => c.Coordinates.X == x && c.Coordinates.Y == HeroY + 4))
            {
                blockedHorizontalWallCells++;
            }
        }

        // All horizontal wall segments should be blocked by the vertical wall
        Assert.AreEqual(3, blockedHorizontalWallCells,
            "All horizontal wall segments (14,14), (15,14), (16,14) should be blocked by vertical wall");

        // Verify specific cells beyond the L-wall are blocked
        var cellsBeyondL = new[] {
        new Coordinates(HeroX + 4, HeroY + 5),  // (14, 15)
        new Coordinates(HeroX + 5, HeroY + 5),  // (15, 15)
        new Coordinates(HeroX + 6, HeroY + 5),  // (16, 15)
    };

        int blockedBeyondL = 0;
        foreach (var coord in cellsBeyondL)
        {
            if (!hero.VisibleCells.Any(c => c.Coordinates.X == coord.X && c.Coordinates.Y == coord.Y))
            {
                blockedBeyondL++;
            }
        }

        Assert.IsTrue(blockedBeyondL >= 2,
            $"At least 2 cells beyond the L-wall should be blocked, found {blockedBeyondL} blocked cells");

        // Verify the wall creates some blockage
        // Hero should see cells before the wall
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX + 2 && c.Coordinates.Y == HeroY + 2),
            "Hero should see cells before the wall at (12, 12)");

        // L-wall should reduce visible cells compared to empty map
        // Empty map with sight range 9 shows approximately 254 cells
        // L-wall should block at least some cells (realistically 20-40 cells)
        Assert.IsTrue(hero.VisibleCells.Count < 245,
            $"The L-wall should block at least some cells, hero sees {hero.VisibleCells.Count} cells (expected < 245)");
        
        Assert.IsTrue(hero.VisibleCells.Count > 200,
            $"The L-wall shouldn't block most cells, hero sees {hero.VisibleCells.Count} cells (expected > 200)");
    }

    [TestMethod]
    public void UpdateVisibleCells_EnemyBlocking_BlocksCellsBehind()
    {
        // Arrange
        var playground = CreatePlayground();
        var hero = CreateHero();
        playground.PlaceHero(hero, new Coordinates(HeroX, HeroY));

        var enemy = CreateEnemy();
        playground.PlaceEnemy(enemy, new Coordinates(HeroX, HeroY + 5));

        // Act
        playground.UpdateAgentVision(hero);

        // Assert
        Assert.IsNotNull(hero.VisibleCells);

        // Hero can see the enemy
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX && c.Coordinates.Y == HeroY + 5),
            "Hero should see the enemy at (10, 15)");

        // Hero cannot see cells directly behind the enemy
        Assert.IsFalse(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX && c.Coordinates.Y == HeroY + 6),
            "Hero should NOT see (10, 16) behind enemy");
        Assert.IsFalse(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX && c.Coordinates.Y == HeroY + 8),
            "Hero should NOT see (10, 18) behind enemy");
    }

    [TestMethod]
    public void UpdateVisibleCells_MultipleScatteredBlocks_CreatesMultipleShadows()
    {
        // Arrange
        var playground = CreatePlayground();
        var hero = CreateHero();
        playground.PlaceHero(hero, new Coordinates(HeroX, HeroY));

        // Place scattered blocks
        var blockPositions = new[]
        {
            new Coordinates(HeroX - 2, HeroY + 2),
            new Coordinates(HeroX + 2, HeroY + 4),
            new Coordinates(HeroX + 5, HeroY),
            new Coordinates(HeroX, HeroY - 4)
        };

        foreach (var pos in blockPositions)
        {
            var block = new Block(Guid.NewGuid());
            playground.AddBlock(block, pos);
        }

        // Act
        playground.UpdateAgentVision(hero);

        // Assert
        Assert.IsNotNull(hero.VisibleCells);

        // Hero can see all blocks
        foreach (var pos in blockPositions)
        {
            Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == pos.X && c.Coordinates.Y == pos.Y),
                $"Hero should see block at ({pos.X}, {pos.Y})");
        }

        // Check shadows behind blocks
        // Block at (15, 10) should block (16, 10) and beyond
        Assert.IsFalse(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX + 6 && c.Coordinates.Y == HeroY),
            "Hero should NOT see (16, 10) behind block");

        // Block at (10, 6) should block (10, 5) and beyond
        Assert.IsFalse(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX && c.Coordinates.Y == HeroY - 5),
            "Hero should NOT see (10, 5) behind block");
    }

    [TestMethod]
    public void UpdateVisibleCells_BlockBetweenHeroAndEdge_BlocksCellsBehind()
    {
        // Arrange
        var playground = CreatePlayground();
        var hero = CreateHero();
        playground.PlaceHero(hero, new Coordinates(HeroX, HeroY));

        var block = new Block(Guid.NewGuid());
        playground.AddBlock(block, new Coordinates(HeroX - 5, HeroY));

        // Act
        playground.UpdateAgentVision(hero);

        // Assert
        Assert.IsNotNull(hero.VisibleCells);

        // Hero can see the block
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX - 5 && c.Coordinates.Y == HeroY),
            "Hero should see the block at (5, 10)");

        // Hero cannot see cells behind the block toward the edge
        Assert.IsFalse(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX - 6 && c.Coordinates.Y == HeroY),
            "Hero should NOT see (4, 10) behind block");
        Assert.IsFalse(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX - 8 && c.Coordinates.Y == HeroY),
            "Hero should NOT see (2, 10) behind block");
    }

    [TestMethod]
    public void UpdateVisibleCells_RingOfBlocks_HeroSeesOnlyInside()
    {
        // Arrange
        var playground = CreatePlayground();
        var hero = CreateHero();
        playground.PlaceHero(hero, new Coordinates(HeroX, HeroY));

        // Create ring of blocks at radius 2
        int ringRadius = 2;
        for (int x = HeroX - ringRadius; x <= HeroX + ringRadius; x++)
        {
            // Top and bottom of ring
            playground.AddBlock(new Block(Guid.NewGuid()), new Coordinates(x, HeroY - ringRadius));
            playground.AddBlock(new Block(Guid.NewGuid()), new Coordinates(x, HeroY + ringRadius));
        }
        for (int y = HeroY - ringRadius + 1; y <= HeroY + ringRadius - 1; y++)
        {
            // Left and right sides of ring
            playground.AddBlock(new Block(Guid.NewGuid()), new Coordinates(HeroX - ringRadius, y));
            playground.AddBlock(new Block(Guid.NewGuid()), new Coordinates(HeroX + ringRadius, y));
        }

        // Act
        playground.UpdateAgentVision(hero);

        // Assert
        Assert.IsNotNull(hero.VisibleCells);

        // Hero should see cells inside the ring
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX && c.Coordinates.Y == HeroY),
            "Hero should see their own position");
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX + 1 && c.Coordinates.Y == HeroY),
            "Hero should see inside ring");

        // Hero should see the ring itself
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX && c.Coordinates.Y == HeroY + ringRadius),
            "Hero should see the ring");

        // Hero should NOT see cells outside the ring
        Assert.IsFalse(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX && c.Coordinates.Y == HeroY + ringRadius + 1),
            "Hero should NOT see outside ring at (10, 13)");
        Assert.IsFalse(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX + ringRadius + 1 && c.Coordinates.Y == HeroY),
            "Hero should NOT see outside ring at (13, 10)");

        // Verify total visible cells is limited to inner area + ring
        int expectedMaxCells = (ringRadius * 2 + 1) * (ringRadius * 2 + 1);
        Assert.IsTrue(hero.VisibleCells.Count <= expectedMaxCells,
            $"Hero should see at most {expectedMaxCells} cells (inside + ring)");
    }

    [TestMethod]
    public void UpdateVisibleCells_MaximumRangeVerification_SeesExactlyToRange()
    {
        // Arrange
        var playground = CreatePlayground();
        var hero = CreateHero();
        playground.PlaceHero(hero, new Coordinates(HeroX, HeroY));

        // Act
        playground.UpdateAgentVision(hero);

        // Assert
        Assert.IsNotNull(hero.VisibleCells);

        // Hero should see cells exactly at max range in cardinal directions
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX && c.Coordinates.Y == HeroY + HeroSightRange),
            $"Hero should see north at max range ({HeroX}, {HeroY + HeroSightRange})");
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX && c.Coordinates.Y == HeroY - HeroSightRange),
            $"Hero should see south at max range ({HeroX}, {HeroY - HeroSightRange})");
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX + HeroSightRange && c.Coordinates.Y == HeroY),
            $"Hero should see east at max range ({HeroX + HeroSightRange}, {HeroY})");
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX - HeroSightRange && c.Coordinates.Y == HeroY),
            $"Hero should see west at max range ({HeroX - HeroSightRange}, {HeroY})");

        // Hero should NOT see cells beyond max range
        Assert.IsFalse(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX && c.Coordinates.Y == HeroY + HeroSightRange + 1),
            "Hero should NOT see beyond north max range");
        Assert.IsFalse(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX + HeroSightRange + 1 && c.Coordinates.Y == HeroY),
            "Hero should NOT see beyond east max range");

        // All visible cells should be within the circular range
        foreach (var cell in hero.VisibleCells)
        {
            double distance = Math.Sqrt(
                Math.Pow(cell.Coordinates.X - HeroX, 2) +
                Math.Pow(cell.Coordinates.Y - HeroY, 2)
            );
            Assert.IsTrue(distance <= HeroSightRange + 0.01, // Small epsilon for floating point
                $"Cell ({cell.Coordinates.X}, {cell.Coordinates.Y}) at distance {distance} exceeds sight range {HeroSightRange}");
        }
    }
}