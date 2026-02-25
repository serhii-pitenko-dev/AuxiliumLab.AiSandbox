# AuxiliumLab.AiSandbox.ApplicationServices

**Onion layer: Application**  
Orchestrates use-cases by coordinating Domain, Infrastructure, and external services.  
Contains **no game rules** — those live exclusively in `Domain`.

## Purpose
- Execute simulation runs (single, batch, training).
- Expose command and query interfaces consumed by Presentation layers.
- Map between domain objects and persistence/presentation models.
- Integrate AI decision-making into the simulation loop.

## Folder Structure
```
ApplicationServices/
├── Commands/
│   └── Playground/                 IPlaygroundCommandsHandleService, PlaygroundCommandsHandleService
│       └── CreatePlayground/       CreatePlaygroundCommand + handler
├── Queries/
│   └── Map/                        IMapQueriesHandleService, GetMapLayout, GetAffectedCells
├── Executors/                      Core simulation loop implementations
├── Runner/
│   ├── SingleRunner/               One-shot run helpers
│   ├── MassRunner/                 Parallel batch + incremental sweep runner
│   ├── TestPreconditionSet/        Seeded precondition run support
│   └── LogsDto/                    Raw data log and performance DTOs
├── Saver/
│   └── Persistence/Sandbox/        State serialization + mapper
├── Trainer/                        TrainingRunner — orchestrates RL training
├── Converters/                     Domain ↔ DTO converters
└── Configuration/                  ApplicationServicesCollectionExtensions
```

## Core Components

### Commands — `IPlaygroundCommandsHandleService`
Exposes every state-mutating operation on the `StandardPlayground` as a method (light command pattern).  
All commands go through the aggregate root — external code never manipulates the map directly.

Key operations:
- Create a new playground from configuration.
- Move an agent.
- Toggle an agent action (Run, etc.).
- Place objects on the map.

### Queries — `IMapQueriesHandleService`
Read-only access to map state, designed for presentation consumers.

| Query | Returns | Description |
|---|---|---|
| `GetMapLayout` | `MapLayoutResponse` | Full 2D grid of `MapCell` DTOs |
| `GetAffectedCells` | `AffectedCellsResponse` | Only the cells that changed since last render |

### Executor Pattern

All simulation execution flows through an `Executor`.

```
IExecutor (base interface)
├── IExecutorForPresentation   — runs with event notifications (ConsolePresentation subscribes)
├── IStandardExecutor          — silent run, captures ParticularRun result
└── (Training executor)        — created inline by TrainingRunner per environment
```

**`Executor` base class** (`Executors/Executor.cs`):
- Holds repository and service references.
- `RunAsync(config)` — the main simulation loop:
  1. Creates playground via command handler.
  2. Sends `GameStartedEvent`.
  3. Loops per turn: prepares agents → AI decides → executes action → updates vision → checks win/loss.
  4. Saves state to file at configurable intervals.
  5. Sends `HeroWonEvent` or `HeroLostEvent` on termination.

**`StandardExecutor`**: inherits `Executor`, suppresses agent notification events, captures result as `ParticularRun`.

**`ExecutorForPresentation`**: inherits `Executor`, publishes `OnBaseAgentActionEvent` and `TurnExecutedEvent` so the console renderer can animate changes.

### Runners

#### `SingleRunner`
Wraps a single executor call. Three modes:
```csharp
RunSingleAsync(IExecutorForPresentation)         // console visualization
RunSingleTrainedAsync(IStandardExecutor)         // trained-model run
RunTestPreconditionsAsync(IExecutorForPresentation) // seeded precondition run
```

#### `MassRunner`
Parallel batch execution using `Parallel.ForEachAsync`.

**Phases:**
1. **Standard phase** — runs `count` simulations in parallel, collects `ParticularRun` results.
2. **Incremental sweep phase** — for each configured property (e.g. `MaxTurns`, `SightRange`) sweeps its range in configured steps, one batch per step value.
3. **Area sweep phase** — optional, sweeps map area independently.

**Output:** CSV files with per-batch summaries, written via `IStatisticFileDataManager`.

#### `TrainingRunner`
Coordinates the full RL training loop:
1. Selects the correct `ITraining` implementation (PPO / A2C / DQN) from `AiTrainingOrchestrator`.
2. Resolves scoped executor pairs (one per physical CPU core).
3. Starts all executor tasks — each loops an `Sb3Actions`-driven simulation episode.
4. Calls `IPolicyTrainerClient.StartTrainingXxx()` to kick off the Python SB3 training.

### Saver — Persistence
`Saver/Persistence/Sandbox/`:
- `StandardPlaygroundState` — serializable snapshot of a full playground.
- `IStandardPlaygroundMapper` — maps between `StandardPlayground` (domain) and `StandardPlaygroundState` (persistence DTO).
- Snapshots are saved to disk via `IFileDataManager<StandardPlaygroundState>` at a frequency controlled by `SandBox.SaveToFileRegularity`.

### `TestPreconditionData`
Wraps a pre-saved `StandardPlaygroundState`. When `IsPreconditionStart = true` in settings, the executor loads this state instead of generating a random map — useful for reproducible debugging and benchmarking.

## Execution Flow Summary

```
Startup (calls SingleRunner / MassRunner / TrainingRunner)
   └─ Runner creates / resolves Executor
         └─ Executor.RunAsync()
               ├─ PlaygroundCommandsHandleService.CreatePlayground()
               │       └─ Domain: PlaygroundFactory builds StandardPlayground
               ├─ Loop per turn
               │     ├─ IAiActions.GetAction(AgentStateForAIDecision)
               │     ├─ PlaygroundCommandsHandleService.MoveAgent() / ToggleAction()
               │     └─ IMessageBroker.Publish(TurnExecutedEvent)
               └─ Captures result → MassRunner aggregates → CSV output
```

## How to Add a New Use-Case
1. **Command:** Add a new method to `IPlaygroundCommandsHandleService` + implementation in `PlaygroundCommandsHandleService`. The implementation calls `StandardPlayground` methods only.
2. **Query:** Add a new method to `IMapQueriesHandleService` + implementation that reads from `IMemoryDataManager<StandardPlayground>`.
3. **New runner mode:** Implement in `Runner/`, inject whatever executors or services are needed. Register in `Configuration/ApplicationServicesCollectionExtensions`.
