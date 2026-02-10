using AiSandBox.Domain.Agents.Entities;
using AiSandBox.Domain.Agents.Services.Vision;
using AiSandBox.Domain.Maps;
using AiSandBox.Domain.Playgrounds;
using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.UnitTests.AiSandBox.Domain.Playgrounds;

[TestClass]
public class StandardPlaygroundTest
{
    private const int MapWidth = 21;
    private const int MapHeight = 14;
    private const int HeroSpeed = 5;
    private const int HeroSightRange = 6;
    private const int HeroStamina = 15;

    private StandardPlayground CreatePlayground()
    {
        var map = new MapSquareCells(MapWidth, MapHeight);
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

    [TestMethod]
    public void LookAroundEveryone_HeroAtTopLeftCorner_ShouldSeeExpectedArea()
    {
        // Arrange
        var playground = CreatePlayground();
        var hero = CreateHero();
        var heroPosition = new Coordinates(0, 0);
        playground.PlaceHero(hero, heroPosition);

        // Act
        playground.LookAroundEveryone();

        // Assert
        Assert.IsNotNull(hero.VisibleCells);
        Assert.IsTrue(hero.VisibleCells.Count > 0);

        foreach (var cell in hero.VisibleCells)
        {
            Assert.IsTrue(cell.Coordinates.X >= 0 && cell.Coordinates.X <= 6,
                $"Cell X coordinate {cell.Coordinates.X} is outside expected range [0..6]");
            Assert.IsTrue(cell.Coordinates.Y >= 0 && cell.Coordinates.Y <= 6,
                $"Cell Y coordinate {cell.Coordinates.Y} is outside expected range [0..6]");
        }

        // Verify hero can see their own position
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == 0 && c.Coordinates.Y == 0),
            "Hero should be able to see their own position");
    }

    [TestMethod]
    public void LookAroundEveryone_HeroAtBottomLeftCorner_ShouldSeeExpectedArea()
    {
        // Arrange
        var playground = CreatePlayground();
        var hero = CreateHero();
        var heroPosition = new Coordinates(0, 13);
        playground.PlaceHero(hero, heroPosition);

        // Act
        playground.LookAroundEveryone();

        // Assert
        Assert.IsNotNull(hero.VisibleCells);
        Assert.IsTrue(hero.VisibleCells.Count > 0);

        foreach (var cell in hero.VisibleCells)
        {
            Assert.IsTrue(cell.Coordinates.X >= 0 && cell.Coordinates.X <= 6,
                $"Cell X coordinate {cell.Coordinates.X} is outside expected range [0..6]");
            Assert.IsTrue(cell.Coordinates.Y >= 7 && cell.Coordinates.Y <= 13,
                $"Cell Y coordinate {cell.Coordinates.Y} is outside expected range [7..13]");
        }

        // Verify hero can see their own position
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == 0 && c.Coordinates.Y == 13),
            "Hero should be able to see their own position");
    }

    [TestMethod]
    public void LookAroundEveryone_HeroAtTopRightCorner_ShouldSeeExpectedArea()
    {
        // Arrange
        var playground = CreatePlayground();
        var hero = CreateHero();
        var heroPosition = new Coordinates(20, 0);
        playground.PlaceHero(hero, heroPosition);

        // Act
        playground.LookAroundEveryone();

        // Assert
        Assert.IsNotNull(hero.VisibleCells);
        Assert.IsTrue(hero.VisibleCells.Count > 0);

        foreach (var cell in hero.VisibleCells)
        {
            Assert.IsTrue(cell.Coordinates.X >= 14 && cell.Coordinates.X <= 20,
                $"Cell X coordinate {cell.Coordinates.X} is outside expected range [14..20]");
            Assert.IsTrue(cell.Coordinates.Y >= 0 && cell.Coordinates.Y <= 6,
                $"Cell Y coordinate {cell.Coordinates.Y} is outside expected range [0..6]");
        }

        // Verify hero can see their own position
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == 20 && c.Coordinates.Y == 0),
            "Hero should be able to see their own position");
    }

    [TestMethod]
    public void LookAroundEveryone_HeroAtBottomRightCorner_ShouldSeeExpectedArea()
    {
        // Arrange
        var playground = CreatePlayground();
        var hero = CreateHero();
        var heroPosition = new Coordinates(20, 13);
        playground.PlaceHero(hero, heroPosition);

        // Act
        playground.LookAroundEveryone();

        // Assert
        Assert.IsNotNull(hero.VisibleCells);
        Assert.IsTrue(hero.VisibleCells.Count > 0);

        foreach (var cell in hero.VisibleCells)
        {
            Assert.IsTrue(cell.Coordinates.X >= 14 && cell.Coordinates.X <= 20,
                $"Cell X coordinate {cell.Coordinates.X} is outside expected range [14..20]");
            Assert.IsTrue(cell.Coordinates.Y >= 7 && cell.Coordinates.Y <= 13,
                $"Cell Y coordinate {cell.Coordinates.Y} is outside expected range [7..13]");
        }

        // Verify hero can see their own position
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == 20 && c.Coordinates.Y == 13),
            "Hero should be able to see their own position");
    }

    [TestMethod]
    public void LookAroundEveryone_HeroAtPosition5_2_ShouldSeeExpectedArea()
    {
        // Arrange
        var playground = CreatePlayground();
        var hero = CreateHero();
        var heroPosition = new Coordinates(5, 2);
        playground.PlaceHero(hero, heroPosition);

        // Act
        playground.LookAroundEveryone();

        // Assert
        Assert.IsNotNull(hero.VisibleCells);
        Assert.IsTrue(hero.VisibleCells.Count > 0);

        foreach (var cell in hero.VisibleCells)
        {
            Assert.IsTrue(cell.Coordinates.X >= 0 && cell.Coordinates.X <= 11,
                $"Cell X coordinate {cell.Coordinates.X} is outside expected range [0..11]");
            Assert.IsTrue(cell.Coordinates.Y >= 0 && cell.Coordinates.Y <= 8,
                $"Cell Y coordinate {cell.Coordinates.Y} is outside expected range [0..8]");
        }

        // Verify hero can see their own position
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == 5 && c.Coordinates.Y == 2),
            "Hero should be able to see their own position");
    }

    [TestMethod]
    public void LookAroundEveryone_HeroAtPosition7_7_ShouldSeeExpectedArea()
    {
        // Arrange
        var playground = CreatePlayground();
        var hero = CreateHero();
        var heroPosition = new Coordinates(7, 7);
        playground.PlaceHero(hero, heroPosition);

        // Act
        playground.LookAroundEveryone();

        // Assert
        Assert.IsNotNull(hero.VisibleCells);
        Assert.IsTrue(hero.VisibleCells.Count > 0);

        foreach (var cell in hero.VisibleCells)
        {
            Assert.IsTrue(cell.Coordinates.X >= 1 && cell.Coordinates.X <= 13,
                $"Cell X coordinate {cell.Coordinates.X} is outside expected range [1..13]");
            Assert.IsTrue(cell.Coordinates.Y >= 1 && cell.Coordinates.Y <= 13,
                $"Cell Y coordinate {cell.Coordinates.Y} is outside expected range [1..13]");
        }

        // Verify hero can see their own position
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == 7 && c.Coordinates.Y == 7),
            "Hero should be able to see their own position");
    }

    [TestMethod]
    public void LookAroundEveryone_HeroAtPosition5_10_ShouldSeeExpectedArea()
    {
        // Arrange
        var playground = CreatePlayground();
        var hero = CreateHero();
        var heroPosition = new Coordinates(5, 10);
        playground.PlaceHero(hero, heroPosition);

        // Act
        playground.LookAroundEveryone();

        // Assert
        Assert.IsNotNull(hero.VisibleCells);
        Assert.IsTrue(hero.VisibleCells.Count > 0);

        foreach (var cell in hero.VisibleCells)
        {
            Assert.IsTrue(cell.Coordinates.X >= 0 && cell.Coordinates.X <= 11,
                $"Cell X coordinate {cell.Coordinates.X} is outside expected range [0..11]");
            Assert.IsTrue(cell.Coordinates.Y >= 4 && cell.Coordinates.Y <= 13,
                $"Cell Y coordinate {cell.Coordinates.Y} is outside expected range [4..13]");
        }

        // Verify hero can see their own position
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == 5 && c.Coordinates.Y == 10),
            "Hero should be able to see their own position");
    }

    [TestMethod]
    public void LookAroundEveryone_HeroAtPosition18_10_ShouldSeeExpectedArea()
    {
        // Arrange
        var playground = CreatePlayground();
        var hero = CreateHero();
        var heroPosition = new Coordinates(18, 10);
        playground.PlaceHero(hero, heroPosition);

        // Act
        playground.LookAroundEveryone();

        // Assert
        Assert.IsNotNull(hero.VisibleCells);
        Assert.IsTrue(hero.VisibleCells.Count > 0);

        foreach (var cell in hero.VisibleCells)
        {
            Assert.IsTrue(cell.Coordinates.X >= 12 && cell.Coordinates.X <= 20,
                $"Cell X coordinate {cell.Coordinates.X} is outside expected range [12..20]");
            Assert.IsTrue(cell.Coordinates.Y >= 4 && cell.Coordinates.Y <= 13,
                $"Cell Y coordinate {cell.Coordinates.Y} is outside expected range [4..13]");
        }

        // Verify hero can see their own position
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == 18 && c.Coordinates.Y == 10),
            "Hero should be able to see their own position");
    }

    [TestMethod]
    public void LookAroundEveryone_HeroAtPosition18_2_ShouldSeeExpectedArea()
    {
        // Arrange
        var playground = CreatePlayground();
        var hero = CreateHero();
        var heroPosition = new Coordinates(18, 2);
        playground.PlaceHero(hero, heroPosition);

        // Act
        playground.LookAroundEveryone();

        // Assert
        Assert.IsNotNull(hero.VisibleCells);
        Assert.IsTrue(hero.VisibleCells.Count > 0);

        foreach (var cell in hero.VisibleCells)
        {
            Assert.IsTrue(cell.Coordinates.X >= 12 && cell.Coordinates.X <= 20,
                $"Cell X coordinate {cell.Coordinates.X} is outside expected range [12..20]");
            Assert.IsTrue(cell.Coordinates.Y >= 0 && cell.Coordinates.Y <= 8,
                $"Cell Y coordinate {cell.Coordinates.Y} is outside expected range [0..8]");
        }

        // Verify hero can see their own position
        Assert.IsTrue(hero.VisibleCells.Any(c => c.Coordinates.X == 18 && c.Coordinates.Y == 2),
            "Hero should be able to see their own position");
    }
}