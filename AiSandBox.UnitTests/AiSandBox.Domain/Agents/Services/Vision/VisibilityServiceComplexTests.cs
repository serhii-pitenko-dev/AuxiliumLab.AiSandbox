using AiSandBox.Domain.InanimateObjects;
using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.UnitTests.AiSandBox.Domain.Agents.Services.Vision;

[TestClass]
public class VisibilityServiceComplexTests : VisibilityServiceTestBase
{
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

        // Hero can see the vertical wall
        for (int y = HeroY + 1; y <= HeroY + 4; y++)
        {
            Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX + 3 && c.Coordinates.Y == y),
                $"Hero should see vertical wall at ({HeroX + 3}, {y})");
        }

        // The vertical wall blocks the horizontal wall segments
        int blockedHorizontalWallCells = 0;
        for (int x = HeroX + 4; x <= HeroX + 6; x++)
        {
            if (!hero.VisibleCells.Any(c => c.Coordinates.X == x && c.Coordinates.Y == HeroY + 4))
            {
                blockedHorizontalWallCells++;
            }
        }

        Assert.AreEqual(3, blockedHorizontalWallCells,
            "All horizontal wall segments (14,14), (15,14), (16,14) should be blocked by vertical wall");

        // Verify cells beyond the L-wall are blocked
        var cellsBeyondL = new[]
        {
            new Coordinates(HeroX + 4, HeroY + 5),
            new Coordinates(HeroX + 5, HeroY + 5),
            new Coordinates(HeroX + 6, HeroY + 5),
        };

        int blockedBeyondL = cellsBeyondL.Count(coord =>
            !hero.VisibleCells.Any(c => c.Coordinates.X == coord.X && c.Coordinates.Y == coord.Y));

        Assert.IsTrue(blockedBeyondL >= 2,
            $"At least 2 cells beyond the L-wall should be blocked, found {blockedBeyondL} blocked cells");

        // Hero should see cells before the wall
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX + 2 && c.Coordinates.Y == HeroY + 2),
            "Hero should see cells before the wall at (12, 12)");

        // L-wall should reduce visible cells
        int maxExpectedCells = ExpectedEmptyMapCellsAtCenter - MinCellsBlockedByLWall;
        int minExpectedCells = ExpectedEmptyMapCellsAtCenter - MaxCellsBlockedByLWall;

        Assert.IsTrue(hero.VisibleCells.Count < maxExpectedCells,
            $"The L-wall should block at least {MinCellsBlockedByLWall} cells, hero sees {hero.VisibleCells.Count} cells (expected < {maxExpectedCells})");

        Assert.IsTrue(hero.VisibleCells.Count > minExpectedCells,
            $"The L-wall shouldn't block more than {MaxCellsBlockedByLWall} cells, hero sees {hero.VisibleCells.Count} cells (expected > {minExpectedCells})");
    }

    [TestMethod]
    public void LookAroundEveryone_MultipleAgentsWithDifferentRanges_UpdatesAllCorrectly()
    {
        // Arrange
        var playground = CreatePlayground();
        var hero = CreateHero(sightRange: 9);
        var enemy1 = CreateEnemy(sightRange: 3);
        var enemy2 = CreateEnemy(sightRange: 5);

        playground.PlaceHero(hero, new Coordinates(5, 5));
        playground.PlaceEnemy(enemy1, new Coordinates(15, 8));
        playground.PlaceEnemy(enemy2, new Coordinates(10, 15));

        // Act
        playground.LookAroundEveryone();

        // Assert
        AssertVisibleCellsAreValid(hero, new Coordinates(5, 5), 9, "Hero");
        AssertVisibleCellsAreValid(enemy1, new Coordinates(15, 8), 3, "Enemy1");
        AssertVisibleCellsAreValid(enemy2, new Coordinates(10, 15), 5, "Enemy2");

        // Verify different sight ranges result in different cell counts
        Assert.IsTrue(hero.VisibleCells.Count > enemy1.VisibleCells.Count,
            "Hero with sight range 9 should see more cells than Enemy1 with sight range 3");
        Assert.IsTrue(enemy2.VisibleCells.Count > enemy1.VisibleCells.Count,
            "Enemy2 with sight range 5 should see more cells than Enemy1 with sight range 3");
    }

    [TestMethod]
    public void UpdateVisibleCells_OverlappingShadowsFromMultipleBlocks_MergesShadowsCorrectly()
    {
        // Arrange
        var playground = CreatePlayground();
        var hero = CreateHero();
        playground.PlaceHero(hero, new Coordinates(HeroX, HeroY));

        // Create two adjacent blocks that create overlapping shadows
        playground.AddBlock(new Block(Guid.NewGuid()), new Coordinates(HeroX, HeroY + 3));
        playground.AddBlock(new Block(Guid.NewGuid()), new Coordinates(HeroX + 1, HeroY + 3));

        // Act
        playground.UpdateAgentVision(hero);

        // Assert
        // Hero can see both blocks
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX && c.Coordinates.Y == HeroY + 3),
            "Hero should see first block");
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX + 1 && c.Coordinates.Y == HeroY + 3),
            "Hero should see second block");

        // Both blocks create merged shadow behind them
        Assert.IsFalse(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX && c.Coordinates.Y == HeroY + 4),
            "Hero should NOT see (10, 14) behind first block");
        Assert.IsFalse(hero.VisibleCells.Any(c => c.Coordinates.X == HeroX + 1 && c.Coordinates.Y == HeroY + 4),
            "Hero should NOT see (11, 14) behind second block");
    }
}