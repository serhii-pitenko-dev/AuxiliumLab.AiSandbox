# Application Services — Execution Logic

> For the full project README see [../AuxiliumLab.AiSandbox.ApplicationServices/README.md](../AuxiliumLab.AiSandbox.ApplicationServices/README.md).  
> For architecture context see [ARCHITECTURE.md](ARCHITECTURE.md).

## Responsibility
The Application Services layer orchestrates simulation runs.  
It holds **no game rules** — those live in the Domain.  
It coordinates: Domain commands, Infrastructure persistence, AI decisions, and event broadcasting via the MessageBroker.

## Executor Hierarchy

```
IExecutor
  ├── RunAsync(config?)             core loop
  └── TestRunWithPreconditionsAsync seeded run

IExecutorForPresentation : IExecutor
  └── ExecutorForPresentation       publishes per-action events to ConsoleRunner

IStandardExecutor : IExecutor
  └── StandardExecutor              silent run, returns ParticularRun result
```

All executors share a base `Executor` class that contains the main simulation loop.

## Simulation Loop (inside `Executor.RunAsync`)

```
1.  Create playground
      └─ PlaygroundCommandsHandleService.CreatePlayground(config)
            └─ PlaygroundFactory → StandardPlayground  (Domain)
            └─ MemoryDataManager.AddOrUpdate(id, playground)

2.  Publish GameStartedEvent  →  ConsoleRunner initialises display

3.  Loop per turn (until win / loss / turn limit):
    a. playground.OnStartTurnActions()         increment Turn counter
    b. For each agent in turn order:
       i.   PrepareAgentForTurnActions(agent)  reset stamina, recalc available actions
       ii.  Build AgentStateForAIDecision      (visible cells, stats, actions)
       iii. IAiActions.GetAction(state)        AI returns AgentAction + direction
       iv.  Execute action via command handler (Move or ToggleRun)
       v.   playground.UpdateAgentVision(agent)
       vi.  Publish OnBaseAgentActionEvent     (only ExecutorForPresentation)

    c. Publish TurnExecutedEvent  →  ConsoleRunner re-renders

4.  Detect termination:
      Hero reached Exit cell  → Publish HeroWonEvent(WinReason.ReachedExit)
      Enemy reached Hero cell → Publish HeroLostEvent(LostReason.CaughtByEnemy)
      Turn == MaxTurns        → Publish HeroLostEvent(LostReason.TurnLimitReached)

5.  Save state every N turns (SandBox.SaveToFileRegularity)
```

## Win / Loss Conditions

| Condition | Outcome |
|---|---|
| Hero occupies the Exit cell | `HeroWonEvent(WinReason.ReachedExit)` |
| An Enemy occupies the Hero's cell | `HeroLostEvent(LostReason.CaughtByEnemy)` |
| `Turn >= MaxTurns` | `HeroLostEvent(LostReason.TurnLimitReached)` |

## AI Integration

```
IAiActions.GetAction(AgentStateForAIDecision)
    ├── RandomAiActions       — picks a random valid action each turn
    ├── Sb3Actions            — sends Sb3Contract messages through IMessageBroker;
    │                           awaits correlated SimulationStepResponse via IBrokerRpcClient
    └── (future)              — any other AI backend implementing IAiActions
```

`AgentStateForAIDecision` contains:
- `VisibleCells` — list of visible cells with object types.
- `HeroCoordinates`, `ExitCoordinates`.
- `AvailableActions` — which actions are valid this turn.
- `Stamina`, `MaxStamina`, `SightRange`.

## Commands — `IPlaygroundCommandsHandleService`

| Method | Domain call |
|---|---|
| `CreatePlayground(config)` | `PlaygroundFactory.Create()` |
| `MoveAgent(playgroundId, agentId, direction)` | `StandardPlayground.MoveObject()` |
| `ToggleAgentAction(playgroundId, agentId, action, isActivated)` | `Agent.DoAction()` |

## Queries — `IMapQueriesHandleService`

| Query | Returns |
|---|---|
| `MapLayoutQuery.GetFromMemory(id)` | Full `MapLayoutResponse` (all cells, 2D grid of `MapCell`) |
| `GetAffectedCellsQuery.Get(id, coords)` | `AffectedCellsResponse` (only changed cells) |

## Runners

### `SingleRunner`
Wraps a single executor invocation. Used for console and load-from-file modes.

### `MassRunner`
Parallel batch runner. Runs `count` simulations in parallel using `Parallel.ForEachAsync`.  
Also performs **incremental parameter sweeps**: for each configured property, runs a separate batch per step value.  
Output: per-batch CSV rows + a master CSV file per `batchRunId`.

### `TrainingRunner`
Coordinates RL training:
1. Selects `ITraining` (PPO / A2C / DQN) from `AiTrainingOrchestrator`.
2. Creates `N = PhysicalCores` scoped executor+AI pairs.
3. All executors respond to gym `reset`/`step` calls from the Python service.
4. Calls `IPolicyTrainerClient.StartTrainingXxx()` to begin the Python SB3 training loop.

## Persistence

- `StandardPlaygroundState` — serialisable snapshot of a complete playground.
- `IStandardPlaygroundMapper` maps between `StandardPlayground` ↔ `StandardPlaygroundState`.
- Snapshots saved via `IFileDataManager<StandardPlaygroundState>`.
- Frequency: every `SandBox.SaveToFileRegularity` turns (0 = disabled).
