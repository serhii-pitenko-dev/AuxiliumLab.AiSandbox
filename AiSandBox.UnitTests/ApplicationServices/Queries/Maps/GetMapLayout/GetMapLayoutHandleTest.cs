using AiSandBox.ApplicationServices.Queries.Maps.GetMapLayout;
using AiSandBox.Domain.Agents.Entities;
using AiSandBox.Domain.Agents.Services.Vision;
using AiSandBox.Domain.InanimateObjects;
using AiSandBox.Domain.Maps;
using AiSandBox.Domain.Playgrounds;
using AiSandBox.Infrastructure.FileManager;
using AiSandBox.Infrastructure.MemoryManager;
using AiSandBox.SharedBaseTypes.ValueObjects;
using Moq;

namespace AiSandBox.UnitTests.ApplicationServices.Queries.Maps.GetMapLayout;

[TestClass]
public class GetMapLayoutHandleTest
{
    private Mock<IMemoryDataManager<StandardPlayground>> _mockMemoryDataManager;
    private Mock<IFileDataManager<StandardPlayground>> _mockFileDataManager;
    private Mock<IFileDataManager<MapLayoutResponse>> _mockMapLayoutDataManager;
    private GetMapLayoutHandle _handler;
    private Guid _testGuid;
    private IVisibilityService _visibilityService;

    [TestInitialize]
    public void Setup()
    {
        _mockMemoryDataManager = new Mock<IMemoryDataManager<StandardPlayground>>();
        _mockFileDataManager = new Mock<IFileDataManager<StandardPlayground>>();
        _mockMapLayoutDataManager = new Mock<IFileDataManager<MapLayoutResponse>>();
        _handler = new GetMapLayoutHandle(
            _mockMemoryDataManager.Object,
            _mockFileDataManager.Object,
            _mockMapLayoutDataManager.Object);
        _testGuid = Guid.NewGuid();
        _visibilityService = new VisibilityService();
    }

    [TestMethod]
    public void GetFromMemory_ShouldReturnCorrectMapLayoutResponse_WhenPlaygroundIsLoaded()
    {
        // Arrange
        var playground = CreateTestPlayground();
        _mockMemoryDataManager
            .Setup(m => m.LoadObject(_testGuid))
            .Returns(playground);

        // Act
        var result = _handler.GetFromMemory(_testGuid);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.turnNumber);
        Assert.AreEqual(10, result.Cells.GetLength(0)); // Width
        Assert.AreEqual(15, result.Cells.GetLength(1)); // Height

        // Verify Hero position (Cartesian X0Y2 -> Screen X0Y12)
        int heroScreenY = 15 - 1 - 2; // Height - 1 - cartesianY = 12
        Assert.AreEqual(ECellType.Hero, result.Cells[0, heroScreenY].CellType);

        // Verify Enemy positions
        // Enemy 1: Cartesian X3Y4 -> Screen X3Y10
        int enemy1ScreenY = 15 - 1 - 4; // = 10
        Assert.AreEqual(ECellType.Enemy, result.Cells[3, enemy1ScreenY].CellType);

        // Enemy 2: Cartesian X8Y8 -> Screen X8Y6
        int enemy2ScreenY = 15 - 1 - 8; // = 6
        Assert.AreEqual(ECellType.Enemy, result.Cells[8, enemy2ScreenY].CellType);

        // Verify Block positions (Cartesian to Screen conversion)
        // Block 1: Cartesian X1Y1 -> Screen X1Y13
        Assert.AreEqual(ECellType.Block, result.Cells[1, 13].CellType);
        // Block 2: Cartesian X5Y5 -> Screen X5Y9
        Assert.AreEqual(ECellType.Block, result.Cells[5, 9].CellType);
        // Block 3: Cartesian X9Y9 -> Screen X9Y5
        Assert.AreEqual(ECellType.Block, result.Cells[9, 5].CellType);

        // Verify that MemoryDataManager.LoadObject was called
        _mockMemoryDataManager.Verify(m => m.LoadObject(_testGuid), Times.Once);
    }

    private StandardPlayground CreateTestPlayground()
    {
        // Create 10x15 map
        var map = new MapSquareCells(10, 15);

        // Create Hero at Cartesian coordinates X0Y2
        var hero = new Hero(
            new Coordinates(0, 2),
            new InitialAgentCharacters(speed: 1, sightRange: 3, stamina: 100),
            id: Guid.NewGuid());

        // Create Enemies
        var enemy1 = new Enemy(
            new Coordinates(3, 4),
            new InitialAgentCharacters(speed: 1, sightRange: 2, stamina: 50),
            id: Guid.NewGuid());

        var enemy2 = new Enemy(
            new Coordinates(8, 8),
            new InitialAgentCharacters(speed: 1, sightRange: 2, stamina: 50),
            id: Guid.NewGuid());

        // Create Blocks at various positions
        var block1 = new Block(new Coordinates(1, 1), Guid.NewGuid());
        var block2 = new Block(new Coordinates(5, 5), Guid.NewGuid());
        var block3 = new Block(new Coordinates(9, 9), Guid.NewGuid());

        // Create Playground
        var playground = new StandardPlayground(map, _visibilityService);

        // Place objects using Playground methods (these will call map.PlaceObject internally)
        playground.PlaceHero(hero);
        playground.PlaceEnemy(enemy1);
        playground.PlaceEnemy(enemy2);
        playground.AddBlock(block1);
        playground.AddBlock(block2);
        playground.AddBlock(block3);

        // Simulate LookAround to set visibility (optional but realistic)
        playground.LookAroundEveryone();

        return playground;
    }
}

