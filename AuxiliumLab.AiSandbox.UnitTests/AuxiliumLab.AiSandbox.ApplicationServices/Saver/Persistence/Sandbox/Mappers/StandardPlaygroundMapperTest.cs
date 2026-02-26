using AuxiliumLab.AiSandbox.ApplicationServices.Saver.Persistence.Sandbox.Mappers;
using AuxiliumLab.AiSandbox.ApplicationServices.Saver.Persistence.Sandbox.States;
using AuxiliumLab.AiSandbox.Domain.Agents.Entities;
using AuxiliumLab.AiSandbox.Domain.Agents.Factories;
using AuxiliumLab.AiSandbox.Domain.Agents.Services.Vision;
using AuxiliumLab.AiSandbox.Domain.InanimateObjects;
using AuxiliumLab.AiSandbox.Domain.Maps;
using AuxiliumLab.AiSandbox.Domain.Playgrounds;
using AuxiliumLab.AiSandbox.Domain.Playgrounds.Builders;
using AuxiliumLab.AiSandbox.SharedBaseTypes.ValueObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace AuxiliumLab.AiSandbox.UnitTests.AuxiliumLab.AiSandbox.ApplicationServices.Saver.Persistence.Sandbox.Mappers;

/// <summary>
/// The most important test - RoundTrip_ToStateAndFromState_PreservesPlaygroundData 
/// </summary>
[TestClass]
public class StandardPlaygroundMapperTest
{
    private StandardPlaygroundMapper _mapper = null!;
    private Mock<IPlaygroundBuilder> _mockPlaygroundBuilder = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockPlaygroundBuilder = new Mock<IPlaygroundBuilder>();
        _mapper = new StandardPlaygroundMapper(_mockPlaygroundBuilder.Object);
    }

    #region Constructor Tests

    [TestMethod]
    public void Constructor_WhenPlaygroundBuilderIsNull_ThrowsArgumentNullException()
    {

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new StandardPlaygroundMapper(null));
    }

    #endregion

    #region ToState Tests

    [TestMethod]
    public void ToState_WhenPlaygroundIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _mapper.ToState(null!));
    }

    [TestMethod]
    public void ToState_WhenPlaygroundHasBasicProperties_MapsCorrectly()
    {
        // Arrange
        var playground = CreateBasicPlayground();
        var expectedId = playground.Id;
        var expectedTurn = playground.Turn;

        // Act
        var result = _mapper.ToState(playground);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(expectedId, result.Id);
        Assert.AreEqual(expectedTurn, result.Turn);
        Assert.IsNotNull(result.Map);
        Assert.AreEqual(10, result.Map.Width);
        Assert.AreEqual(8, result.Map.Height);
    }

    [TestMethod]
    public void ToState_WhenPlaygroundHasHero_MapsHeroCorrectly()
    {
        // Arrange
        var playground = CreatePlaygroundWithHero();

        // Act
        var result = _mapper.ToState(playground);

        // Assert
        Assert.IsNotNull(result.Hero);
        Assert.AreEqual(playground.Hero!.Id, result.Hero.Id);
        Assert.AreEqual(playground.Hero.Coordinates, result.Hero.Coordinates);
        Assert.AreEqual(playground.Hero.Speed, result.Hero.Speed);
        Assert.AreEqual(playground.Hero.SightRange, result.Hero.SightRange);
        Assert.AreEqual(playground.Hero.IsRun, result.Hero.IsRun);
        Assert.AreEqual(playground.Hero.Stamina, result.Hero.Stamina);
        Assert.AreEqual(playground.Hero.MaxStamina, result.Hero.MaxStamina);
        Assert.AreEqual(playground.Hero.OrderInTurnQueue, result.Hero.OrderInTurnQueue);
        CollectionAssert.AreEqual(playground.Hero.PathToTarget.ToList(), result.Hero.PathToTarget);
        CollectionAssert.AreEqual(playground.Hero.AvailableActions.ToList(), result.Hero.AvailableActions);
        CollectionAssert.AreEqual(playground.Hero.ExecutedActions.ToList(), result.Hero.ExecutedActions);
    }

    [TestMethod]
    public void ToState_WhenPlaygroundHasNoHero_MapsHeroAsNull()
    {
        // Arrange
        var playground = CreateBasicPlayground();

        // Act
        var result = _mapper.ToState(playground);

        // Assert
        Assert.IsNull(result.Hero);
    }

    [TestMethod]
    public void ToState_WhenPlaygroundHasExit_MapsExitCorrectly()
    {
        // Arrange
        var playground = CreatePlaygroundWithExit();

        // Act
        var result = _mapper.ToState(playground);

        // Assert
        Assert.IsNotNull(result.Exit);
        Assert.AreEqual(playground.Exit!.Id, result.Exit.Id);
        Assert.AreEqual(playground.Exit.Coordinates, result.Exit.Coordinates);
    }

    [TestMethod]
    public void ToState_WhenPlaygroundHasNoExit_MapsExitAsNull()
    {
        // Arrange
        var playground = CreateBasicPlayground();

        // Act
        var result = _mapper.ToState(playground);

        // Assert
        Assert.IsNull(result.Exit);
    }

    [TestMethod]
    public void ToState_WhenPlaygroundHasBlocks_MapsBlocksCorrectly()
    {
        // Arrange
        var playground = CreatePlaygroundWithBlocks(3);

        // Act
        var result = _mapper.ToState(playground);

        // Assert
        Assert.AreEqual(3, result.Blocks.Count);
        foreach (var block in playground.Blocks)
        {
            var mappedBlock = result.Blocks.FirstOrDefault(b => b.Id == block.Id);
            Assert.IsNotNull(mappedBlock);
            Assert.AreEqual(block.Coordinates, mappedBlock.Coordinates);
        }
    }

    [TestMethod]
    public void ToState_WhenPlaygroundHasEnemies_MapsEnemiesCorrectly()
    {
        // Arrange
        var playground = CreatePlaygroundWithEnemies(2);

        // Act
        var result = _mapper.ToState(playground);

        // Assert
        Assert.AreEqual(2, result.Enemies.Count);
        foreach (var enemy in playground.Enemies)
        {
            var mappedEnemy = result.Enemies.FirstOrDefault(e => e.Id == enemy.Id);
            Assert.IsNotNull(mappedEnemy);
            Assert.AreEqual(enemy.Coordinates, mappedEnemy.Coordinates);
            Assert.AreEqual(enemy.Speed, mappedEnemy.Speed);
            Assert.AreEqual(enemy.SightRange, mappedEnemy.SightRange);
            Assert.AreEqual(enemy.IsRun, mappedEnemy.IsRun);
            Assert.AreEqual(enemy.Stamina, mappedEnemy.Stamina);
            Assert.AreEqual(enemy.MaxStamina, mappedEnemy.MaxStamina);
            Assert.AreEqual(enemy.OrderInTurnQueue, mappedEnemy.OrderInTurnQueue);
        }
    }

    [TestMethod]
    public void ToState_WhenPlaygroundHasMap_MapsCellGridCorrectly()
    {
        // Arrange
        var playground = CreatePlaygroundWithHero();
        var heroCoordinates = playground.Hero!.Coordinates;

        // Act
        var result = _mapper.ToState(playground);

        // Assert
        Assert.IsNotNull(result.Map.CellGrid);
        Assert.AreEqual(10, result.Map.CellGrid.GetLength(0));
        Assert.AreEqual(8, result.Map.CellGrid.GetLength(1));

        // Verify hero cell
        var heroCell = result.Map.CellGrid[heroCoordinates.X, heroCoordinates.Y];
        Assert.AreEqual(ObjectType.Hero, heroCell.ObjectType);
        Assert.AreEqual(playground.Hero.Id, heroCell.ObjectId);
    }

    #endregion

    #region FromState Tests

    [TestMethod]
    public void FromState_WhenStateIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        SetupMockBuilder();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _mapper.FromState(null!));
    }

    [TestMethod]
    public void FromState_WhenStateHasBasicProperties_ReconstructsPlaygroundCorrectly()
    {
        // Arrange
        var state = CreateBasicState();
        var expectedPlayground = CreateBasicPlayground();
        SetupMockBuilder(expectedPlayground);

        // Act
        var result = _mapper.FromState(state);

        // Assert
        Assert.IsNotNull(result);
        _mockPlaygroundBuilder.Verify(b => b.SetMap(It.IsAny<MapSquareCells>()), Times.Once);
        _mockPlaygroundBuilder.Verify(b => b.SetPlaygroundId(state.Id), Times.Once);
        _mockPlaygroundBuilder.Verify(b => b.SetTurn(state.Turn), Times.Once);
        _mockPlaygroundBuilder.Verify(b => b.FillCellGrid(), Times.Once);
        _mockPlaygroundBuilder.Verify(b => b.Build(), Times.Once);
    }

    [TestMethod]
    public void FromState_WhenStateHasBlocks_PlacesBlocksCorrectly()
    {
        // Arrange
        var state = CreateStateWithBlocks(2);
        var expectedPlayground = CreateBasicPlayground();
        SetupMockBuilder(expectedPlayground);

        // Act
        var result = _mapper.FromState(state);

        // Assert
        _mockPlaygroundBuilder.Verify(b => b.PlaceBlock(It.IsAny<Block>(), It.IsAny<Coordinates>()), Times.Exactly(2));
    }

    [TestMethod]
    public void FromState_WhenStateHasExit_PlacesExitCorrectly()
    {
        // Arrange
        var state = CreateStateWithExit();
        var expectedPlayground = CreateBasicPlayground();
        SetupMockBuilder(expectedPlayground);

        // Act
        var result = _mapper.FromState(state);

        // Assert
        _mockPlaygroundBuilder.Verify(b => b.PlaceExit(It.IsAny<Exit>(), It.IsAny<Coordinates>()), Times.Once);
    }

    [TestMethod]
    public void FromState_WhenStateHasNoExit_DoesNotPlaceExit()
    {
        // Arrange
        var state = CreateBasicState();
        var expectedPlayground = CreateBasicPlayground();
        SetupMockBuilder(expectedPlayground);

        // Act
        var result = _mapper.FromState(state);

        // Assert
        _mockPlaygroundBuilder.Verify(b => b.PlaceExit(It.IsAny<Exit>(), It.IsAny<Coordinates>()), Times.Never);
    }

    [TestMethod]
    public void FromState_WhenStateHasHero_PlacesHeroCorrectly()
    {
        // Arrange
        var state = CreateStateWithHero();
        var expectedPlayground = CreateBasicPlayground();
        SetupMockBuilder(expectedPlayground);

        // Act
        var result = _mapper.FromState(state);

        // Assert
        _mockPlaygroundBuilder.Verify(b => b.PlaceHero(It.IsAny<Hero>(), It.IsAny<Coordinates>()), Times.Once);
    }

    [TestMethod]
    public void FromState_WhenStateHasNoHero_DoesNotPlaceHero()
    {
        // Arrange
        var state = CreateBasicState();
        var expectedPlayground = CreateBasicPlayground();
        SetupMockBuilder(expectedPlayground);

        // Act
        var result = _mapper.FromState(state);

        // Assert
        _mockPlaygroundBuilder.Verify(b => b.PlaceHero(It.IsAny<Hero>(), It.IsAny<Coordinates>()), Times.Never);
    }

    [TestMethod]
    public void FromState_WhenStateHasEnemies_PlacesEnemiesCorrectly()
    {
        // Arrange
        var state = CreateStateWithEnemies(3);
        var expectedPlayground = CreateBasicPlayground();
        SetupMockBuilder(expectedPlayground);

        // Act
        var result = _mapper.FromState(state);

        // Assert
        _mockPlaygroundBuilder.Verify(b => b.PlaceEnemy(It.IsAny<Enemy>(), It.IsAny<Coordinates>()), Times.Exactly(3));
    }

    [TestMethod]
    public void FromState_WhenStateIsComplete_ReconstructsAllComponents()
    {
        // Arrange
        var state = CreateCompleteState();
        var expectedPlayground = CreateBasicPlayground();
        SetupMockBuilder(expectedPlayground);

        // Act
        var result = _mapper.FromState(state);

        // Assert
        _mockPlaygroundBuilder.Verify(b => b.SetMap(It.IsAny<MapSquareCells>()), Times.Once);
        _mockPlaygroundBuilder.Verify(b => b.SetPlaygroundId(state.Id), Times.Once);
        _mockPlaygroundBuilder.Verify(b => b.SetTurn(state.Turn), Times.Once);
        _mockPlaygroundBuilder.Verify(b => b.PlaceBlock(It.IsAny<Block>(), It.IsAny<Coordinates>()), Times.Exactly(state.Blocks.Count));
        _mockPlaygroundBuilder.Verify(b => b.PlaceExit(It.IsAny<Exit>(), It.IsAny<Coordinates>()), Times.Once);
        _mockPlaygroundBuilder.Verify(b => b.PlaceHero(It.IsAny<Hero>(), It.IsAny<Coordinates>()), Times.Once);
        _mockPlaygroundBuilder.Verify(b => b.PlaceEnemy(It.IsAny<Enemy>(), It.IsAny<Coordinates>()), Times.Exactly(state.Enemies.Count));
        _mockPlaygroundBuilder.Verify(b => b.FillCellGrid(), Times.Once);
        _mockPlaygroundBuilder.Verify(b => b.Build(), Times.Once);
    }

    #endregion

    #region Round-Trip Tests

    [TestMethod]
    public void RoundTrip_ToStateAndFromState_PreservesPlaygroundData()
    {
        // Arrange
        var originalPlayground = CreateCompletePlayground();

        // Act - Convert to state
        var state = _mapper.ToState(originalPlayground);

        // Recreate mapper with actual builder for round-trip
        var visibilityService = new Mock<IVisibilityService>().Object;
        var mockEnemyFactory = new Mock<IEnemyFactory>();
        var mockHeroFactory = new Mock<IHeroFactory>();
        var actualBuilder = new PlaygroundBuilder(mockEnemyFactory.Object, mockHeroFactory.Object, visibilityService);
        var roundTripMapper = new StandardPlaygroundMapper(actualBuilder);

        // Act - Convert back to playground
        var reconstructedPlayground = roundTripMapper.FromState(state);

        // Assert - Basic properties
        Assert.AreEqual(originalPlayground.Id, reconstructedPlayground.Id);
        Assert.AreEqual(originalPlayground.Turn, reconstructedPlayground.Turn);
        Assert.AreEqual(originalPlayground.MapWidth, reconstructedPlayground.MapWidth);
        Assert.AreEqual(originalPlayground.MapHeight, reconstructedPlayground.MapHeight);

        // Assert - Collections count
        // Border blocks are re-created by SetMap and are not persisted in state,
        // so exclude them when comparing against the original (which was built
        // directly via StandardPlayground, without any border blocks).
        Assert.AreEqual(originalPlayground.Blocks.Count, reconstructedPlayground.Blocks.Count(b => b is not BorderBlock));
        Assert.AreEqual(originalPlayground.Enemies.Count, reconstructedPlayground.Enemies.Count);
        Assert.IsNotNull(reconstructedPlayground.Hero);
        Assert.IsNotNull(reconstructedPlayground.Exit);
    }

    #endregion

    #region Helper Methods

    private StandardPlayground CreateBasicPlayground()
    {
        var map = new MapSquareCells(10, 8);
        var visibilityService = new Mock<IVisibilityService>().Object;
        return new StandardPlayground(map, visibilityService);
    }

    private StandardPlayground CreatePlaygroundWithHero()
    {
        var playground = CreateBasicPlayground();
        var cell = playground.GetCell(0, 0);
        var characters = new InitialAgentCharacters(5, 3, 100, [], [], [], false, 0);
        var hero = new Hero(cell, characters, Guid.NewGuid());
        playground.PlaceHero(hero, hero.Coordinates);
        return playground;
    }

    private StandardPlayground CreatePlaygroundWithExit()
    {
        var playground = CreateBasicPlayground();
        var cell = playground.GetCell(9, 7);
        var exit = new Exit(cell, Guid.NewGuid());
        playground.PlaceExit(exit, exit.Coordinates);
        return playground;
    }

    private StandardPlayground CreatePlaygroundWithBlocks(int count)
    {
        var playground = CreateBasicPlayground();
        for (int i = 0; i < count; i++)
        {
            var cell = playground.GetCell(i + 1, 1);
            var block = new Block(cell, Guid.NewGuid());
            playground.AddBlock(block, block.Coordinates);
        }
        return playground;
    }

    private StandardPlayground CreatePlaygroundWithEnemies(int count)
    {
        var playground = CreateBasicPlayground();
        for (int i = 0; i < count; i++)
        {
            var cell = playground.GetCell(i + 5, 3);
            var characters = new InitialAgentCharacters(4, 2, 80, [], [], [], false, i);
            var enemy = new Enemy(cell, characters, Guid.NewGuid());
            playground.PlaceEnemy(enemy, enemy.Coordinates);
        }
        return playground;
    }

    private StandardPlayground CreateCompletePlayground()
    {
        var playground = CreateBasicPlayground();

        // Add hero — place at interior cell (x=1 is first interior column after border)
        var heroCell = playground.GetCell(1, 1);
        var heroCharacters = new InitialAgentCharacters(5, 3, 100, [], [], [], false, 0);
        var hero = new Hero(heroCell, heroCharacters, Guid.NewGuid());
        playground.PlaceHero(hero, hero.Coordinates);

        // Add exit — place at last interior column (x=8 for a 10-wide map)
        var exitCell = playground.GetCell(8, 6);
        var exit = new Exit(exitCell, Guid.NewGuid());
        playground.PlaceExit(exit, exit.Coordinates);

        // Add blocks at interior positions (start at x=2 to avoid hero at x=1)
        for (int i = 0; i < 2; i++)
        {
            var cell = playground.GetCell(i + 2, 1);
            var block = new Block(cell, Guid.NewGuid());
            playground.AddBlock(block, block.Coordinates);
        }

        // Add enemies
        for (int i = 0; i < 2; i++)
        {
            var cell = playground.GetCell(i + 5, 3);
            var characters = new InitialAgentCharacters(4, 2, 80, [], [], [], false, i + 1);
            var enemy = new Enemy(cell, characters, Guid.NewGuid());
            playground.PlaceEnemy(enemy, enemy.Coordinates);
        }

        return playground;
    }

    private StandardPlaygroundState CreateBasicState()
    {
        var cellGrid = new CellState[10, 8];
        for (int x = 0; x < 10; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                cellGrid[x, y] = new CellState
                {
                    Coordinates = new Coordinates(x, y),
                    ObjectType = ObjectType.Empty,
                    ObjectId = Guid.NewGuid()
                };
            }
        }

        return new StandardPlaygroundState
        {
            Id = Guid.NewGuid(),
            Turn = 0,
            Map = new MapSquareCellsState
            {
                Width = 10,
                Height = 8,
                CellGrid = cellGrid
            }
        };
    }

    private StandardPlaygroundState CreateStateWithHero()
    {
        var state = CreateBasicState();
        var heroId = Guid.NewGuid();
        state = state with
        {
            Hero = new HeroState
            {
                Id = heroId,
                Coordinates = new Coordinates(0, 0),
                Speed = 5,
                SightRange = 3,
                IsRun = false,
                Stamina = 100,
                MaxStamina = 100,
                OrderInTurnQueue = 0,
                PathToTarget = [],
                VisibleCells = [],
                AvailableActions = [],
                ExecutedActions = []
            }
        };
        return state;
    }

    private StandardPlaygroundState CreateStateWithExit()
    {
        var state = CreateBasicState();
        state = state with
        {
            Exit = new ExitState
            {
                Id = Guid.NewGuid(),
                Coordinates = new Coordinates(9, 7)
            }
        };
        return state;
    }

    private StandardPlaygroundState CreateStateWithBlocks(int count)
    {
        var state = CreateBasicState();
        var blocks = new List<BlockState>();
        for (int i = 0; i < count; i++)
        {
            blocks.Add(new BlockState
            {
                Id = Guid.NewGuid(),
                Coordinates = new Coordinates(i + 1, 1)
            });
        }
        state = state with { Blocks = blocks };
        return state;
    }

    private StandardPlaygroundState CreateStateWithEnemies(int count)
    {
        var state = CreateBasicState();
        var enemies = new List<EnemyState>();
        for (int i = 0; i < count; i++)
        {
            enemies.Add(new EnemyState
            {
                Id = Guid.NewGuid(),
                Coordinates = new Coordinates(i + 5, 3),
                Speed = 4,
                SightRange = 2,
                IsRun = false,
                Stamina = 80,
                MaxStamina = 80,
                OrderInTurnQueue = i,
                PathToTarget = [],
                VisibleCells = [],
                AvailableActions = [],
                ExecutedActions = []
            });
        }
        state = state with { Enemies = enemies };
        return state;
    }

    private StandardPlaygroundState CreateCompleteState()
    {
        var state = CreateBasicState();
        var heroId = Guid.NewGuid();
        var exitId = Guid.NewGuid();

        var blocks = new List<BlockState>
        {
            new() { Id = Guid.NewGuid(), Coordinates = new Coordinates(1, 1) },
            new() { Id = Guid.NewGuid(), Coordinates = new Coordinates(2, 1) }
        };

        var enemies = new List<EnemyState>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Coordinates = new Coordinates(5, 3),
                Speed = 4,
                SightRange = 2,
                IsRun = false,
                Stamina = 80,
                MaxStamina = 80,
                OrderInTurnQueue = 1,
                PathToTarget = [],
                VisibleCells = [],
                AvailableActions = [],
                ExecutedActions = []
            },
            new()
            {
                Id = Guid.NewGuid(),
                Coordinates = new Coordinates(6, 3),
                Speed = 4,
                SightRange = 2,
                IsRun = false,
                Stamina = 80,
                MaxStamina = 80,
                OrderInTurnQueue = 2,
                PathToTarget = [],
                VisibleCells = [],
                AvailableActions = [],
                ExecutedActions = []
            }
        };

        return state with
        {
            Turn = 5,
            Hero = new HeroState
            {
                Id = heroId,
                Coordinates = new Coordinates(0, 0),
                Speed = 5,
                SightRange = 3,
                IsRun = false,
                Stamina = 100,
                MaxStamina = 100,
                OrderInTurnQueue = 0,
                PathToTarget = [],
                VisibleCells = [],
                AvailableActions = [],
                ExecutedActions = []
            },
            Exit = new ExitState
            {
                Id = exitId,
                Coordinates = new Coordinates(9, 7)
            },
            Blocks = blocks,
            Enemies = enemies
        };
    }

    private void SetupMockBuilder(StandardPlayground? returnPlayground = null)
    {
        var playground = returnPlayground ?? CreateBasicPlayground();

        _mockPlaygroundBuilder.Setup(b => b.SetMap(It.IsAny<MapSquareCells>())).Returns(_mockPlaygroundBuilder.Object);
        _mockPlaygroundBuilder.Setup(b => b.SetPlaygroundId(It.IsAny<Guid>())).Returns(_mockPlaygroundBuilder.Object);
        _mockPlaygroundBuilder.Setup(b => b.SetTurn(It.IsAny<int>())).Returns(_mockPlaygroundBuilder.Object);
        _mockPlaygroundBuilder.Setup(b => b.PlaceBlock(It.IsAny<Block>(), It.IsAny<Coordinates>())).Returns(_mockPlaygroundBuilder.Object);
        _mockPlaygroundBuilder.Setup(b => b.PlaceExit(It.IsAny<Exit>(), It.IsAny<Coordinates>())).Returns(_mockPlaygroundBuilder.Object);
        _mockPlaygroundBuilder.Setup(b => b.PlaceHero(It.IsAny<Hero>(), It.IsAny<Coordinates>())).Returns(_mockPlaygroundBuilder.Object);
        _mockPlaygroundBuilder.Setup(b => b.PlaceEnemy(It.IsAny<Enemy>(), It.IsAny<Coordinates>())).Returns(_mockPlaygroundBuilder.Object);
        _mockPlaygroundBuilder.Setup(b => b.FillCellGrid()).Returns(_mockPlaygroundBuilder.Object);
        _mockPlaygroundBuilder.Setup(b => b.Build()).Returns(playground);
    }

    #endregion
}