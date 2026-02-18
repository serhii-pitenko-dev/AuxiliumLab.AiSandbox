using AiSandBox.Ai.Configuration;
using AiSandBox.Common.MessageBroker;
using AiSandBox.Common.MessageBroker.Contracts.AiContract.Commands;
using AiSandBox.Common.MessageBroker.Contracts.AiContract.Responses;
using AiSandBox.Common.MessageBroker.Contracts.CoreServicesContract.Events;
using AiSandBox.Common.MessageBroker.Contracts.GlobalMessagesContract.Events.Lost;
using AiSandBox.Common.MessageBroker.Contracts.GlobalMessagesContract.Events.Win;
using AiSandBox.Common.MessageBroker.Contracts.Sb3Contract.Commands;
using AiSandBox.Common.MessageBroker.Contracts.Sb3Contract.Responses;
using AiSandBox.Infrastructure.MemoryManager;
using AiSandBox.SharedBaseTypes.AiContract.Dto;
using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.Ai;

/// <summary>
/// Bridges Python Stable-Baselines3 gym environments with the .NET simulation.
/// One instance per parallel gym (each has a unique GymId).
/// </summary>
public class Sb3Actions : IAiActions
{
    // ── Constants ─────────────────────────────────────────────────────────────
    public const int ObservationSize = 126; // 5 agent features + 11x11 grid
    private const int SightRadius = 5;
    private const int GridSize = 11; // 2 * SightRadius + 1

    // ── Dependencies ──────────────────────────────────────────────────────────
    private readonly IMessageBroker _messageBroker;
    private readonly IMemoryDataManager<AgentStateForAIDecision> _agentStateMemoryRepository;

    // ── State ─────────────────────────────────────────────────────────────────
    private Guid _playgroundId = Guid.Empty;
    private Func<Task>? _episodeCallback;
    private volatile bool _isWaitingForResetObservation;
    private float[] _lastObservation = new float[ObservationSize];

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
        Guid gymId)
    {
        _messageBroker = messageBroker;
        _agentStateMemoryRepository = agentStateMemoryRepository;
        ModelType = modelType;
        GymId = gymId;
        AiConfiguration = aiConfiguration;
    }

    /// <summary>Called by RunTraining to restart simulation episodes.</summary>
    public void SetEpisodeCallback(Func<Task> callback) => _episodeCallback = callback;

    // ── Initialize ────────────────────────────────────────────────────────────
    public void Initialize()
    {
        _messageBroker.Subscribe<GameStartedEvent>(OnGameStarted);
        _messageBroker.Subscribe<RequestAgentDecisionMakeCommand>(OnDecisionRequest);
        _messageBroker.Subscribe<HeroWonEvent>(OnHeroWon);
        _messageBroker.Subscribe<HeroLostEvent>(OnHeroLost);
        _messageBroker.Subscribe<RequestSimulationResetCommand>(OnSimulationReset);
        _messageBroker.Subscribe<RequestSimulationStepCommand>(OnSimulationStep);
        _messageBroker.Subscribe<RequestSimulationCloseCommand>(OnSimulationClose);
    }

    // ── AiContract handlers ───────────────────────────────────────────────────

    private void OnGameStarted(GameStartedEvent evt)
    {
        // Capture playground id from the first game that targets our gym
        if (_playgroundId == Guid.Empty)
            _playgroundId = evt.PlaygroundId;

        if (evt.PlaygroundId == _playgroundId)
            _messageBroker.Publish(new AiReadyToActionsResponse(Guid.NewGuid(), evt.PlaygroundId, evt.Id));
    }

    private void OnDecisionRequest(RequestAgentDecisionMakeCommand cmd)
    {
        if (cmd.PlaygroundId != _playgroundId) return;

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
                -0.1f, false, false,
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
        CompleteStep(+10f, terminated: true);
        _playgroundId = Guid.Empty; // reset; next episode will re-capture
    }

    private void OnHeroLost(HeroLostEvent evt)
    {
        if (evt.PlaygroundId != _playgroundId) return;
        CompleteStep(-10f, terminated: true);
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

        _resetCorrelationId = cmd.Id;
        _isWaitingForResetObservation = true;

        var tcs = new TaskCompletionSource<SimulationResetResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _resetResponseTcs = tcs;

        // Start a new episode in the background
        _ = _episodeCallback?.Invoke();

        // Await initial obs then publish response to SimulationService
        _ = Task.Run(async () =>
        {
            var response = await tcs.Task.ConfigureAwait(false);
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
        var obs = new float[ObservationSize];

        // Agent features [0-4]
        obs[0] = agent.Coordinates.X;
        obs[1] = agent.Coordinates.Y;
        obs[2] = agent.IsRun ? 1f : 0f;
        obs[3] = agent.MaxStamina > 0 ? (float)agent.Stamina / agent.MaxStamina : 0f;
        obs[4] = agent.Speed;

        // Grid [5-125]: -1 = not visible, else ObjectType int value
        for (int i = 5; i < ObservationSize; i++) obs[i] = -1f;

        foreach (var cell in agent.VisibleCells)
        {
            int dx = cell.Coordinates.X - agent.Coordinates.X;
            int dy = cell.Coordinates.Y - agent.Coordinates.Y;
            if (Math.Abs(dx) <= SightRadius && Math.Abs(dy) <= SightRadius)
            {
                int gx = dx + SightRadius;
                int gy = dy + SightRadius;
                obs[5 + gy * GridSize + gx] = (float)cell.ObjectType;
            }
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
