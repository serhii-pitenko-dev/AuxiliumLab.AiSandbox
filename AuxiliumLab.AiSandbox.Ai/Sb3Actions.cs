using AuxiliumLab.AiSandbox.Ai.Configuration;
using AuxiliumLab.AiSandbox.Common.MessageBroker;
using AuxiliumLab.AiSandbox.Common.MessageBroker.Contracts.AiContract.Commands;
using AuxiliumLab.AiSandbox.Common.MessageBroker.Contracts.AiContract.Responses;
using AuxiliumLab.AiSandbox.Common.MessageBroker.Contracts.CoreServicesContract.Events;
using AuxiliumLab.AiSandbox.Common.MessageBroker.Contracts.GlobalMessagesContract.Events.Lost;
using AuxiliumLab.AiSandbox.Common.MessageBroker.Contracts.GlobalMessagesContract.Events.Win;
using AuxiliumLab.AiSandbox.Common.MessageBroker.Contracts.Sb3Contract.Commands;
using AuxiliumLab.AiSandbox.Common.MessageBroker.Contracts.Sb3Contract.Responses;
using AuxiliumLab.AiSandbox.Infrastructure.MemoryManager;
using AuxiliumLab.AiSandbox.SharedBaseTypes.AiContract.Dto;
using AuxiliumLab.AiSandbox.SharedBaseTypes.ValueObjects;

namespace AuxiliumLab.AiSandbox.Ai;

/// <summary>
/// Bridges Python Stable-Baselines3 gym environments with the .NET simulation.
/// One instance per parallel gym (each has a unique GymId).
/// </summary>
public class Sb3Actions : IAiActions
{
    // ── Dependencies ──────────────────────────────────────────────────────────
    private readonly IMessageBroker _messageBroker;
    private readonly IMemoryDataManager<AgentStateForAIDecision> _agentStateMemoryRepository;

    // ── Reward settings ───────────────────────────────────────────────────────
    private readonly float _stepPenalty;
    private readonly float _winReward;
    private readonly float _lossReward;

    // ── State ─────────────────────────────────────────────────────────────────
    private Guid _playgroundId = Guid.Empty;
    private Func<Task>? _episodeCallback;
    private volatile bool _isWaitingForResetObservation;
    private float[] _lastObservation = [];

    // TCS for the current Reset call (SimulationService awaits SimulationResetResponse)
    private TaskCompletionSource<SimulationResetResponse>? _resetResponseTcs;
    private Guid _resetCorrelationId;

    // TCS for the current Step call (SimulationService awaits SimulationStepResponse)
    private TaskCompletionSource<SimulationStepResponse>? _stepResponseTcs;
    private Guid _stepCorrelationId;

    // TCS bridging Python Step action to executor DecisionMakeCommand
    private TaskCompletionSource<int>? _actionTcs;

    // ── IAiActions ────────────────────────────────────────────────────────────
    public AiConfiguration AiConfiguration { get; init; }

    // ── Extra public API ──────────────────────────────────────────────────────
    public Guid GymId { get; }
    public ModelType ModelType { get; }

    // ── Constructor ───────────────────────────────────────────────────────────
    public Sb3Actions(
        IMessageBroker messageBroker,
        IMemoryDataManager<AgentStateForAIDecision> agentStateMemoryRepository,
        ModelType modelType,
        AiConfiguration aiConfiguration,
        Guid gymId,
        float stepPenalty = -0.1f,
        float winReward = 10f,
        float lossReward = -10f)
    {
        _messageBroker = messageBroker;
        _agentStateMemoryRepository = agentStateMemoryRepository;
        ModelType = modelType;
        GymId = gymId;
        AiConfiguration = aiConfiguration;
        _stepPenalty = stepPenalty;
        _winReward = winReward;
        _lossReward = lossReward;

        _messageBroker.Subscribe<GameStartedEvent>(OnGameStarted);
        _messageBroker.Subscribe<RequestAgentDecisionMakeCommand>(OnDecisionRequest);
        _messageBroker.Subscribe<HeroWonEvent>(OnHeroWon);
        _messageBroker.Subscribe<HeroLostEvent>(OnHeroLost);
        _messageBroker.Subscribe<RequestSimulationResetCommand>(OnSimulationReset);
        _messageBroker.Subscribe<RequestSimulationStepCommand>(OnSimulationStep);
        _messageBroker.Subscribe<RequestSimulationCloseCommand>(OnSimulationClose);
    }

    /// <summary>Called by RunTraining to restart simulation episodes.</summary>
    public void SetEpisodeCallback(Func<Task> callback) => _episodeCallback = callback;

    // ── Initialize ────────────────────────────────────────────────────────────
    // IAiActions.Initialize() is an executor lifecycle hook called at the start
    // of every episode via Executor.StartSimulationPreparationsAsync().
    //
    // For RandomActions it is meaningful: RandomActions is recreated per episode
    // by ExecutorFactory, so it uses Initialize() to set up its broker subscriptions
    // for that specific episode's playground.
    //
    // For Sb3Actions it is a no-op: Sb3Actions is long-lived (one instance per gym
    // for the entire training session) and subscribes to the broker once in its
    // constructor. The executor still calls Initialize() on every episode, but
    // Sb3Actions simply ignores it.
    public void Initialize() { }

    // ── AiContract handlers ───────────────────────────────────────────────────

    private void OnGameStarted(GameStartedEvent evt)
    {
        // With a per-gym isolated broker, every GameStartedEvent on this broker
        // was fired by this gym's own executor — no filtering needed.
        _playgroundId = evt.PlaygroundId;
        _messageBroker.Publish(new AiReadyToActionsResponse(Guid.NewGuid(), evt.PlaygroundId, evt.Id));
    }

    private void OnDecisionRequest(RequestAgentDecisionMakeCommand cmd)
    {
        if (cmd.PlaygroundId != _playgroundId) return;

        Console.WriteLine($"[Sb3Actions:{GymId:N}] OnDecisionRequest playground={cmd.PlaygroundId:N} waitingForReset={_isWaitingForResetObservation}");

        var agent = _agentStateMemoryRepository.LoadObject(cmd.AgentId);
        if (agent is null) return;

        var obs = BuildObservation(agent);
        _lastObservation = obs;

        if (_isWaitingForResetObservation)
        {
            // First decision after Reset -> deliver initial observation to Python
            _isWaitingForResetObservation = false;
            var resetTcs = _resetResponseTcs;
            _resetResponseTcs = null;
            resetTcs?.TrySetResult(new SimulationResetResponse(
                Guid.NewGuid(), GymId, _resetCorrelationId, obs, new Dictionary<string, string>()));
        }
        else
        {
            // Subsequent decision -> deliver step result (step penalty reward)
            var stepTcs = _stepResponseTcs;
            _stepResponseTcs = null;
            stepTcs?.TrySetResult(new SimulationStepResponse(
                Guid.NewGuid(), GymId, _stepCorrelationId, obs,
                _stepPenalty, false, false,
                new Dictionary<string, string>()));
        }

        // Asynchronously wait for Python's next action
        var actionTcs = new TaskCompletionSource<int>();
        _actionTcs = actionTcs;

        var agentId = cmd.AgentId;
        var corrId = cmd.Id;
        var agentPos = agent.Coordinates;

        _ = Task.Run(async () =>
        {
            var action = await actionTcs.Task.ConfigureAwait(false);
            var response = BuildDecisionResponse(corrId, agentId, agentPos, action);
            _messageBroker.Publish(response);
        });
    }

    private void OnHeroWon(HeroWonEvent evt)
    {
        if (evt.PlaygroundId != _playgroundId) return;
        CompleteStep(_winReward, terminated: true);
        _playgroundId = Guid.Empty; // cleared; next Reset will re-capture via _isExpectingNewPlayground
    }

    private void OnHeroLost(HeroLostEvent evt)
    {
        if (evt.PlaygroundId != _playgroundId) return;
        CompleteStep(_lossReward, terminated: true);
        _playgroundId = Guid.Empty;
    }

    private void CompleteStep(float reward, bool terminated)
    {
        var stepTcs = _stepResponseTcs;
        _stepResponseTcs = null;
        stepTcs?.TrySetResult(new SimulationStepResponse(
            Guid.NewGuid(), GymId, _stepCorrelationId, _lastObservation,
            reward, terminated, false, new Dictionary<string, string>()));
    }

    // ── Sb3Contract handlers ──────────────────────────────────────────────────

    private void OnSimulationReset(RequestSimulationResetCommand cmd)
    {
        if (cmd.GymId != GymId) return;

        Console.WriteLine($"[Sb3Actions:{GymId:N}] OnSimulationReset firing, starting episode...");

        _resetCorrelationId = cmd.Id;
        _isWaitingForResetObservation = true;

        var tcs = new TaskCompletionSource<SimulationResetResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _resetResponseTcs = tcs;

        // Start a new episode in the background; propagate failures to reset TCS so Python gets an error
        var capturedTcs = tcs;
        _ = Task.Run(async () =>
        {
            try
            {
                if (_episodeCallback != null)
                    await _episodeCallback().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Sb3Actions:{GymId:N}] Episode callback FAILED: {ex.Message}");
                capturedTcs.TrySetException(ex);
            }
        });

        // Await initial obs then publish response to SimulationService
        _ = Task.Run(async () =>
        {
            var response = await tcs.Task.ConfigureAwait(false);
            Console.WriteLine($"[Sb3Actions:{GymId:N}] Reset TCS resolved, publishing SimulationResetResponse");
            _messageBroker.Publish(response);
        });
    }

    private void OnSimulationStep(RequestSimulationStepCommand cmd)
    {
        if (cmd.GymId != GymId) return;

        _stepCorrelationId = cmd.Id;

        var tcs = new TaskCompletionSource<SimulationStepResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _stepResponseTcs = tcs;

        // Provide action to executor (wakes pending _actionTcs)
        _actionTcs?.TrySetResult(cmd.Action);

        // Await step result then publish response to SimulationService
        _ = Task.Run(async () =>
        {
            var response = await tcs.Task.ConfigureAwait(false);
            _messageBroker.Publish(response);
        });
    }

    private void OnSimulationClose(RequestSimulationCloseCommand cmd)
    {
        if (cmd.GymId != GymId) return;

        _actionTcs?.TrySetCanceled();
        _resetResponseTcs?.TrySetCanceled();
        _stepResponseTcs?.TrySetCanceled();

        _messageBroker.Publish(new SimulationCloseResponse(Guid.NewGuid(), GymId, cmd.Id, true));
    }

    // ── Observation builder ───────────────────────────────────────────────────

    private float[] BuildObservation(AgentStateForAIDecision agent)
    {
        // Grid dimensions are derived entirely from the agent's SightRange,
        // so this method stays correct regardless of configuration changes.
        // SightRange cells in each direction + 1 center cell = one axis length.
        int gridSize = 2 * agent.SightRange + 1;

        // 5 scalar features + gridSize² vision cells.
        // Must stay in sync with the Python-side OBS_DIM = 5 + (2*SightRange+1)².
        int observationSize = 5 + gridSize * gridSize;

        var obs = new float[observationSize];

        // ── Agent scalar features: indices 0–4 ────────────────────────────────
        obs[0] = agent.Coordinates.X;   // Absolute column on the map (0-based).
        obs[1] = agent.Coordinates.Y;   // Absolute row on the map (0-based).
        obs[2] = agent.IsRun ? 1f : 0f; // Run mode: 1.0 = active, 0.0 = inactive.
        obs[3] = agent.MaxStamina > 0 ? (float)agent.Stamina / agent.MaxStamina : 0f; // Stamina fraction [0,1].
        obs[4] = agent.Speed;           // Cells traversable per turn.

        // ── Local vision grid: indices 5 .. (5 + gridSize²−1) ────────────────
        // The grid is centred on the agent. Each cell encodes ObjectType as float:
        //   -1.0  →  not visible (outside SightRange or blocked by LOS)
        //    0.0  →  ObjectType.Empty
        //    1.0  →  ObjectType.Hero
        //    2.0  →  ObjectType.Enemy
        //    3.0  →  ObjectType.Block
        //    4.0  →  ObjectType.Exit
        // VisibleCells is already filtered by the domain (VisibilityService) —
        // no re-filtering needed here.
        for (int i = 5; i < observationSize; i++) obs[i] = -1f;

        // Map each visible cell into the flat obs array using row-major order.
        //
        // The local grid is centred on the agent. With SightRange=2, gridSize=5:
        //
        //   gy=0 → map Y = agentY-2  (topmost row,    smallest Y)
        //   gy=1 → map Y = agentY-1
        //   gy=2 → map Y = agentY    ← agent row (centre)
        //   gy=3 → map Y = agentY+1
        //   gy=4 → map Y = agentY+2  (bottommost row, largest Y)
        //
        // Y increases downward (screen convention), matching action 0 = Y-1 (up)
        // and action 1 = Y+1 (down).
        //
        // For agent at (X=3, Y=9), SightRange=2:
        //   gy=0 → map Y=7:  (1,7)(2,7)(3,7)(4,7)(5,7)  → obs[5..9]
        //   gy=1 → map Y=8:  (1,8)(2,8)(3,8)(4,8)(5,8)  → obs[10..14]
        //   gy=2 → map Y=9:  (1,9)(2,9)(3,9)(4,9)(5,9)  → obs[15..19]  ← agent
        //   gy=3 → map Y=10: (1,10)...(5,10)             → obs[20..24]
        //   gy=4 → map Y=11: (1,11)...(5,11)             → obs[25..29]
        //
        // Index formula: obs[5 + gy * gridSize + gx]
        //   gy * gridSize  — skip gy complete rows of gridSize cells each
        //   + gx           — column within that row
        //   + 5            — skip the 5 scalar features at the front
        foreach (var cell in agent.VisibleCells)
        {
            int dx = cell.Coordinates.X - agent.Coordinates.X;
            int dy = cell.Coordinates.Y - agent.Coordinates.Y;

            // Shift delta into [0, gridSize-1]; agent sits at (SightRange, SightRange).
            int gx = dx + agent.SightRange;
            int gy = dy + agent.SightRange;

            obs[5 + gy * gridSize + gx] = (float)cell.ObjectType;
        }

        return obs;
    }

    // ── Action decoder ────────────────────────────────────────────────────────

    private static AgentDecisionBaseResponse BuildDecisionResponse(
        Guid correlationId, Guid agentId, Coordinates from, int action)
    {
        return action switch
        {
            0 => new AgentDecisionMoveResponse(Guid.NewGuid(), agentId,
                     from, new Coordinates(from.X, Math.Max(0, from.Y - 1)), correlationId, true),
            1 => new AgentDecisionMoveResponse(Guid.NewGuid(), agentId,
                     from, new Coordinates(from.X, from.Y + 1), correlationId, true),
            2 => new AgentDecisionMoveResponse(Guid.NewGuid(), agentId,
                     from, new Coordinates(Math.Max(0, from.X - 1), from.Y), correlationId, true),
            3 => new AgentDecisionMoveResponse(Guid.NewGuid(), agentId,
                     from, new Coordinates(from.X + 1, from.Y), correlationId, true),
            _ => new AgentDecisionUseAbilityResponse(Guid.NewGuid(), agentId,
                     true, AgentAction.Run, correlationId, true)
        };
    }
}
