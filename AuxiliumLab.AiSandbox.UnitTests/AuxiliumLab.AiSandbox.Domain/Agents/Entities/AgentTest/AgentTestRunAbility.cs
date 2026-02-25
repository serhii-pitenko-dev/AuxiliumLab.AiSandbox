using AuxiliumLab.AiSandbox.Domain.Agents.Entities;
using AuxiliumLab.AiSandbox.Domain.Maps;
using AuxiliumLab.AiSandbox.SharedBaseTypes.ValueObjects;

namespace AuxiliumLab.AiSandbox.UnitTests.AuxiliumLab.AiSandbox.Domain.Agents.Entities.AgentTest;

/// <summary>
/// Tests for Agent Run ability functionality.
/// Tests verify movement doubling, stamina consumption, and state management.
/// </summary>
[TestClass]
public class AgentTestRunAbility
{
    /// <summary>
    /// Test helper class that inherits from abstract Agent to enable testing.
    /// Provides access to protected members for test verification.
    /// </summary>
    private class TestAgent : Agent
    {
        public TestAgent(Cell cell, InitialAgentCharacters characters, Guid id)
            : base(ObjectType.Hero, characters, cell, id)
        {
        }

        public TestAgent() : base()
        {
        }
    }

    /// <summary>
    /// Tests the Run method with various stamina scenarios to verify correct movement calculation.
    /// The test uses GetReadyForNewTurn to properly initialize movement actions based on stamina and speed.
    /// </summary>
    /// <param name="speed">Agent's speed value</param>
    /// <param name="stamina">Agent's available stamina</param>
    /// <param name="expectedMoveCountBeforeRun">Expected number of Move actions before Run is activated</param>
    /// <param name="expectedMoveCountAfterRun">Expected number of Move actions after Run is activated</param>
    [TestMethod]
    [DataRow(5, 10, 5, 10, DisplayName = "WithSufficientStamina_DoublesAvailableMovements")]
    [DataRow(5, 3, 3, 3, DisplayName = "WithLimitedStamina_TheSameMovements")]
    [DataRow(5, 0, 0, 0, DisplayName = "WithZeroStamina_NoMovements")]
    [DataRow(6, 12, 6, 12, DisplayName = "WithExactStamina_DoublesCorrectly")]
    [DataRow(8, 20, 8, 16, DisplayName = "WithHighStamina_DoublesLargeMovementCount")]
    public void Run_VariousStaminaScenarios_AddsCorrectMovements(
        int speed,
        int stamina,
        int expectedMoveCountBeforeRun,
        int expectedMoveCountAfterRun)
    {
        // Arrange
        var cell = new Cell(new Coordinates(0, 0));
        var characters = new InitialAgentCharacters(speed, 3, stamina, new List<Coordinates>(), new List<AgentAction>(), new List<AgentAction>());
        var agent = new TestAgent(cell, characters, Guid.NewGuid());

        // Initialize the agent properly - this sets up moves based on stamina and speed
        agent.GetReadyForNewTurn();

        // Verify initial state
        var moveCountBeforeRun = agent.AvailableActions.Count(a => a == AgentAction.Move);
        Assert.AreEqual(expectedMoveCountBeforeRun, moveCountBeforeRun,
            $"Before Run: Expected {expectedMoveCountBeforeRun} movements with stamina {stamina} and speed {speed}");

        // Act
        agent.DoAction(AgentAction.Run, true);

        // Assert
        var moveCountAfterRun = agent.AvailableActions.Count(a => a == AgentAction.Move);
        Assert.AreEqual(expectedMoveCountAfterRun, moveCountAfterRun,
            $"After Run: Expected {expectedMoveCountAfterRun} movements with stamina {stamina} and speed {speed}, but got {moveCountAfterRun}");
    }

    /// <summary>
    /// Verifies that calling Run sets the IsRun property to true,
    /// indicating that the run ability has been activated.
    /// </summary>
    [TestMethod]
    public void Run_SetsIsRunToTrue()
    {
        // Arrange
        var cell = new Cell(new Coordinates(0, 0));
        var characters = new InitialAgentCharacters(5, 3, 10, new List<Coordinates>(), new List<AgentAction>(), new List<AgentAction>());
        var agent = new TestAgent(cell, characters, Guid.NewGuid());
        agent.GetReadyForNewTurn();

        // Act
        agent.DoAction(AgentAction.Run, true);

        // Assert
        Assert.IsTrue(agent.IsRun, "IsRun property should be set to true");
    }

    /// <summary>
    /// Verifies that calling Run adds the AgentAction.Run action to the ExecutedActions list,
    /// tracking that the run ability has been used.
    /// </summary>
    [TestMethod]
    public void Run_AddsRunActionToExecutedActions()
    {
        // Arrange
        var cell = new Cell(new Coordinates(0, 0));
        var characters = new InitialAgentCharacters(5, 3, 10, new List<Coordinates>(), new List<AgentAction>(), new List<AgentAction>());
        var agent = new TestAgent(cell, characters, Guid.NewGuid());
        agent.GetReadyForNewTurn();

        // Act
        agent.DoAction(AgentAction.Run, true);

        // Assert
        Assert.Contains(AgentAction.Run, agent.ExecutedActions, "ExecutedActions should contain Run action");
        Assert.DoesNotContain(AgentAction.Run, agent.AvailableActions, "AgentActions should not contain Run action after execution");
    }

    /// <summary>
    /// Verifies that deactivating Run (DoAction with isActivated = false) sets IsRun to false
    /// and removes half of the movement actions.
    /// </summary>
    [TestMethod]
    public void Run_DeactivateRun_SetsIsRunToFalseAndReducesMovements()
    {
        // Arrange
        var cell = new Cell(new Coordinates(0, 0));
        var characters = new InitialAgentCharacters(6, 3, 20, new List<Coordinates>(), new List<AgentAction>(), new List<AgentAction>());
        var agent = new TestAgent(cell, characters, Guid.NewGuid());
        agent.GetReadyForNewTurn();

        // Activate run first
        agent.DoAction(AgentAction.Run, true);
        var moveCountAfterRun = agent.AvailableActions.Count(a => a == AgentAction.Move);

        // Act - Deactivate run
        agent.DoAction(AgentAction.Run, false);

        // Assert
        Assert.IsFalse(agent.IsRun, "IsRun should be false after deactivating");
        var moveCountAfterStop = agent.AvailableActions.Count(a => a == AgentAction.Move);
        Assert.AreEqual(moveCountAfterRun / 2, moveCountAfterStop,
            "Movement count should be halved after deactivating run");
    }

    /// <summary>
    /// Verifies that calling DoAction with Run when already running doesn't double-apply the effect.
    /// </summary>
    [TestMethod]
    public void Run_WhenAlreadyRunning_DoesNotDoubleApply()
    {
        // Arrange
        var cell = new Cell(new Coordinates(0, 0));
        var characters = new InitialAgentCharacters(5, 3, 20, new List<Coordinates>(), new List<AgentAction>(), new List<AgentAction>());
        var agent = new TestAgent(cell, characters, Guid.NewGuid());
        agent.GetReadyForNewTurn();

        // First run activation
        agent.DoAction(AgentAction.Run, true);
        var moveCountAfterFirstRun = agent.AvailableActions.Count(a => a == AgentAction.Move);

        // Act - Try to activate run again
        agent.DoAction(AgentAction.Run, true);

        // Assert
        var moveCountAfterSecondRun = agent.AvailableActions.Count(a => a == AgentAction.Move);
        Assert.AreEqual(moveCountAfterFirstRun, moveCountAfterSecondRun,
            "Movement count should not change when activating run twice");
    }

    /// <summary>
    /// Verifies that GetReadyForNewTurn properly initializes actions including Run ability.
    /// </summary>
    [TestMethod]
    public void GetReadyForNewTurn_InitializesRunAction()
    {
        // Arrange
        var cell = new Cell(new Coordinates(0, 0));
        var characters = new InitialAgentCharacters(5, 3, 10, new List<Coordinates>(), new List<AgentAction>(), new List<AgentAction>());
        var agent = new TestAgent(cell, characters, Guid.NewGuid());

        // Act
        agent.GetReadyForNewTurn();

        // Assert
        Assert.IsTrue(agent.AvailableActions.Contains(AgentAction.Run),
            "Available actions should include Run action after GetReadyForNewTurn");
    }

    /// <summary>
    /// Verifies that Run respects stamina limits and doesn't add more moves than stamina allows.
    /// </summary>
    [TestMethod]
    public void Run_WithInsufficientStaminaForFullDouble_AddsMaximumPossibleMoves()
    {
        // Arrange - Agent with speed 5 but only 7 stamina
        var cell = new Cell(new Coordinates(0, 0));
        var characters = new InitialAgentCharacters(5, 3, 7, new List<Coordinates>(), new List<AgentAction>(), new List<AgentAction>());
        var agent = new TestAgent(cell, characters, Guid.NewGuid());
        agent.GetReadyForNewTurn();

        // Initially should have 5 moves (limited by speed, not stamina)
        var initialMoves = agent.AvailableActions.Count(a => a == AgentAction.Move);
        Assert.AreEqual(5, initialMoves);

        // Act - Try to run (would need 10 moves total, but only have 7 stamina)
        agent.DoAction(AgentAction.Run, true);

        // Assert - Should have 7 moves (stamina limit)
        var finalMoves = agent.AvailableActions.Count(a => a == AgentAction.Move);
        Assert.AreEqual(7, finalMoves,
            "Agent should have maximum moves allowed by stamina when attempting to run");
    }

    /// <summary>
    /// Verifies that agent with IsRun=true maintains doubled movements when GetReadyForNewTurn is called.
    /// </summary>
    [TestMethod]
    public void GetReadyForNewTurn_WhenIsRunIsTrue_MaintainsDoubledMovements()
    {
        // Arrange
        var cell = new Cell(new Coordinates(0, 0));
        var characters = new InitialAgentCharacters(4, 3, 10, new List<Coordinates>(), new List<AgentAction>(), new List<AgentAction>());
        var agent = new TestAgent(cell, characters, Guid.NewGuid());
        agent.GetReadyForNewTurn();

        // Activate run
        agent.DoAction(AgentAction.Run, true);
        Assert.IsTrue(agent.IsRun);

        // Act - Prepare for new turn (IsRun should still be true)
        agent.GetReadyForNewTurn();

        // Assert
        var moveCount = agent.AvailableActions.Count(a => a == AgentAction.Move);
        Assert.AreEqual(8, moveCount,
            "When IsRun is true, GetReadyForNewTurn should maintain doubled movements");
    }
}