using AuxiliumLab.AiSandbox.ApplicationServices.Queries.Map.Entities;
using AuxiliumLab.AiSandbox.ApplicationServices.Queries.Maps;
using AuxiliumLab.AiSandbox.SharedBaseTypes.ValueObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AuxiliumLab.AiSandbox.UnitTests.AuxiliumLab.AiSandbox.ApplicationServices.Queries.Maps;

/// <summary>
/// Tests for <see cref="ConsoleMapState.ApplyAgentMove"/>.
///
/// Root-cause context: <c>ConsoleRunner.HandleAgentMoveEvent</c> used to clear
/// the agent's "from" cell and write to the "to" cell unconditionally regardless
/// of <c>IsSuccess</c>. When the AI tried to walk into a <c>BorderBlock</c> the
/// hero icon was erased and the hero appeared to pass through the wall.
/// </summary>
[TestClass]
public class ConsoleMapStateTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static MapCell[,] BuildGrid(int width, int height)
    {
        var cells = new MapCell[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                cells[x, y] = new MapCell(new Coordinates(x, y), Guid.Empty, ObjectType.Empty, []);
        return cells;
    }

    private static MapCell[,] PlaceAgent(MapCell[,] cells, Coordinates pos, Guid agentId, ObjectType agentType)
    {
        cells[pos.X, pos.Y] = cells[pos.X, pos.Y] with { ObjectId = agentId, ObjectType = agentType };
        return cells;
    }

    private static MapCell[,] PlaceObstacle(MapCell[,] cells, Coordinates pos, ObjectType obstacleType)
    {
        cells[pos.X, pos.Y] = cells[pos.X, pos.Y] with { ObjectId = Guid.NewGuid(), ObjectType = obstacleType };
        return cells;
    }

    // ── successful move ───────────────────────────────────────────────────────

    [TestMethod]
    public void ApplyAgentMove_WhenSuccess_ClearsFromCell()
    {
        var agentId = Guid.NewGuid();
        var from = new Coordinates(2, 2);
        var to = new Coordinates(3, 2);
        var cells = PlaceAgent(BuildGrid(5, 5), from, agentId, ObjectType.Hero);
        var rerender = new HashSet<Coordinates>();

        ConsoleMapState.ApplyAgentMove(cells, from, to, agentId, isSuccess: true, rerender);

        Assert.AreEqual(ObjectType.Empty, cells[from.X, from.Y].ObjectType,
            "From cell must be emptied after a successful move");
        Assert.AreEqual(Guid.Empty, cells[from.X, from.Y].ObjectId,
            "From cell ObjectId must be reset after a successful move");
    }

    [TestMethod]
    public void ApplyAgentMove_WhenSuccess_SetsAgentInToCell()
    {
        var agentId = Guid.NewGuid();
        var from = new Coordinates(2, 2);
        var to = new Coordinates(3, 2);
        var cells = PlaceAgent(BuildGrid(5, 5), from, agentId, ObjectType.Hero);
        var rerender = new HashSet<Coordinates>();

        ConsoleMapState.ApplyAgentMove(cells, from, to, agentId, isSuccess: true, rerender);

        Assert.AreEqual(agentId, cells[to.X, to.Y].ObjectId,
            "To cell must carry the agent id after a successful move");
        Assert.AreEqual(ObjectType.Hero, cells[to.X, to.Y].ObjectType,
            "To cell must carry the agent type (Hero) after a successful move");
    }

    [TestMethod]
    public void ApplyAgentMove_WhenSuccess_QueuesBothCellsForRerender()
    {
        var agentId = Guid.NewGuid();
        var from = new Coordinates(1, 1);
        var to = new Coordinates(2, 1);
        var cells = PlaceAgent(BuildGrid(5, 5), from, agentId, ObjectType.Hero);
        var rerender = new HashSet<Coordinates>();

        ConsoleMapState.ApplyAgentMove(cells, from, to, agentId, isSuccess: true, rerender);

        Assert.IsTrue(rerender.Contains(from), "From coordinate must be queued for re-render");
        Assert.IsTrue(rerender.Contains(to), "To coordinate must be queued for re-render");
    }

    [TestMethod]
    public void ApplyAgentMove_WhenSuccess_PreservesEnemyType()
    {
        var enemyId = Guid.NewGuid();
        var from = new Coordinates(3, 3);
        var to = new Coordinates(3, 4);
        var cells = PlaceAgent(BuildGrid(7, 7), from, enemyId, ObjectType.Enemy);
        var rerender = new HashSet<Coordinates>();

        ConsoleMapState.ApplyAgentMove(cells, from, to, enemyId, isSuccess: true, rerender);

        Assert.AreEqual(ObjectType.Enemy, cells[to.X, to.Y].ObjectType,
            "Enemy type must be preserved on the to cell");
    }

    // ── failed move (the bug scenario) ────────────────────────────────────────

    [TestMethod]
    public void ApplyAgentMove_WhenFailed_DoesNotEraseAgentFromFromCell()
    {
        // This is the core regression test:
        // Before the fix, HandleAgentMoveEvent always cleared the "from" cell,
        // causing the hero icon to vanish when trying to walk into a BorderBlock.
        var agentId = Guid.NewGuid();
        var from = new Coordinates(2, 2);
        var to = new Coordinates(0, 2); // border block column
        var cells = PlaceAgent(BuildGrid(5, 5), from, agentId, ObjectType.Hero);
        PlaceObstacle(cells, to, ObjectType.BorderBlock);
        var rerender = new HashSet<Coordinates>();

        ConsoleMapState.ApplyAgentMove(cells, from, to, agentId, isSuccess: false, rerender);

        Assert.AreEqual(ObjectType.Hero, cells[from.X, from.Y].ObjectType,
            "From cell must still contain the hero after a failed move — agent must not disappear");
        Assert.AreEqual(agentId, cells[from.X, from.Y].ObjectId,
            "From cell ObjectId must be unchanged after a failed move");
    }

    [TestMethod]
    public void ApplyAgentMove_WhenFailed_DoesNotOverwriteToCellWithAgentType()
    {
        // Before the fix the agent type was written to the obstacle cell,
        // making the hero appear to pass through the wall.
        var agentId = Guid.NewGuid();
        var from = new Coordinates(2, 2);
        var to = new Coordinates(0, 2);
        var obstacleId = Guid.NewGuid();
        var cells = PlaceAgent(BuildGrid(5, 5), from, agentId, ObjectType.Hero);
        cells[to.X, to.Y] = cells[to.X, to.Y] with { ObjectId = obstacleId, ObjectType = ObjectType.BorderBlock };
        var rerender = new HashSet<Coordinates>();

        ConsoleMapState.ApplyAgentMove(cells, from, to, agentId, isSuccess: false, rerender);

        Assert.AreEqual(ObjectType.BorderBlock, cells[to.X, to.Y].ObjectType,
            "BorderBlock cell must not be overwritten when the move is rejected");
        Assert.AreEqual(obstacleId, cells[to.X, to.Y].ObjectId,
            "BorderBlock cell ObjectId must remain unchanged");
    }

    [TestMethod]
    public void ApplyAgentMove_WhenFailed_QueuesFromCellForRerender()
    {
        // The from cell must be re-rendered so that sight effects cleared
        // earlier in the pipeline are flushed correctly.
        var agentId = Guid.NewGuid();
        var from = new Coordinates(2, 2);
        var to = new Coordinates(0, 2);
        var cells = PlaceAgent(BuildGrid(5, 5), from, agentId, ObjectType.Hero);
        PlaceObstacle(cells, to, ObjectType.Block);
        var rerender = new HashSet<Coordinates>();

        ConsoleMapState.ApplyAgentMove(cells, from, to, agentId, isSuccess: false, rerender);

        Assert.IsTrue(rerender.Contains(from),
            "From coordinate must be queued for re-render even on a failed move");
    }

    [TestMethod]
    public void ApplyAgentMove_WhenFailed_DoesNotQueueToCellForRerender()
    {
        // No need to re-render the to cell when the agent didn't move there.
        var agentId = Guid.NewGuid();
        var from = new Coordinates(2, 2);
        var to = new Coordinates(0, 2);
        var cells = PlaceAgent(BuildGrid(5, 5), from, agentId, ObjectType.Hero);
        PlaceObstacle(cells, to, ObjectType.BorderBlock);
        var rerender = new HashSet<Coordinates>();

        ConsoleMapState.ApplyAgentMove(cells, from, to, agentId, isSuccess: false, rerender);

        Assert.IsFalse(rerender.Contains(to),
            "To coordinate must not be queued for re-render when the move was rejected");
    }
}
