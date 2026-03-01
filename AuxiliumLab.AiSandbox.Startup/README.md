# AuxiliumLab.AiSandbox.Startup

**Onion layer: Composition Root**  
The application entry point and Dependency Injection root.  
Depends on: every other project in the solution.

## Purpose
- Wires all services together (composition root).
- Presents the interactive start-up menu.
- Selects the correct host type (generic host vs. Kestrel WebApplication) based on execution mode.
- Dispatches to the correct runner.

## Startup Sequence

```
Program.cs
  │
  ├─ 1. Read configuration files
  │       appsettings.json           →  StartupSettings
  │       training-settings.json     →  TrainingSettings
  │       aggregation-settings.json  →  AggregationSettings  (optional)
  │
  ├─ 2. Interactive menu (unless IsPreconditionStart = true)
  │       MenuRunner.ResolveSettings()
  │         ├─ Choose PresentationMode (Console / Web / WithoutVisualization)
  │         ├─ Choose ExecutionMode
  │         └─ Choose Algorithm (Training mode only)
  │
  ├─ 3. Build host
  │       Training, or AggregationRun containing a Training step
  │                     →  GrpcTrainingHost (Kestrel + HTTP/2 on :50062)
  │       All else      →  Host.CreateDefaultBuilder (pure generic host)
  │
  ├─ 4. Start ConsoleRunner (if Console mode)
  │
  ├─ 5. host.StartAsync()
  │
  ├─ 6. Optionally launch WebApiHost in background (if IsWebApiEnabled)
  │
  └─ 7. Dispatch on ExecutionMode:
          Training                   → TrainingRunner.RunTrainingAsync()
          SingleRandomAISimulation   → SingleRunner.RunSingleAsync()
          MassRandomAISimulation     → MassRunner.RunManyAsync()
          SingleTrainedAISimulation  → SingleRunner.RunSingleTrainedAsync()
          MassTrainedAISimulation    → MassRunner.RunManyAsync()
          TestPreconditions          → SingleRunner.RunTestPreconditionsAsync()
          AggregationRun             → AggregationRunner.RunAggregationAsync()
```

## Execution Modes

| `ExecutionMode` | Host | Description | Status |
|---|---|---|---|
| `Training` | Kestrel (GrpcTrainingHost) | Full RL training with Python SB3 | ✅ |
| `SingleRandomAISimulation` | Generic | One run, random AI, optional console | ✅ |
| `MassRandomAISimulation` | Generic | Parallel batch runs with statistics | ✅ |
| `TestPreconditions` | Generic | Seeded run from saved precondition state | ✅ |
| `SingleTrainedAISimulation` | Generic | Single run using a trained model | ⏭️ |
| `MassTrainedAISimulation` | Generic | Batch runs using trained models | ⏭️ |
| `LoadSimulation` | Generic | Load and continue a saved simulation state | ⏭️ |
| `AggregationRun` | Generic | Runs a configurable sequence of jobs and produces a combined CSV report | ✅ |

## AggregationRun

`AggregationRun` lets you define an ordered sequence of jobs to be executed one after another in a single launch. When all steps finish, a combined comparison report is written to disk.

### How it works

1. On startup, `Program.cs` loads `aggregation-settings.json` (located next to the executable).
2. `AggregationRunner.RunAggregationAsync` iterates the step list in order:
   - **`Training`** — delegates to `TrainingRunner.RunTrainingAsync` and captures the algorithm name, experiment ID and hyperparameters as `TrainingRunInfo`.
   - **`MassRandomAISimulation`** — delegates to `MassRunner.RunManyAsync` (random AI) and captures the full `MassRunCapturedResult`.
   - **`MassTrainedAISimulation`** — delegates to `MassRunner.RunManyAsync` via `InferenceExecutorFactory`, which wires `InferenceActions` (uses the last trained model). Captures `MassRunCapturedResult`.
3. After all steps, `IStatisticFileDataManager.SaveAggregationReportAsync` is called to write the report.

### Configuration — `aggregation-settings.json`

The file lives in the `AuxiliumLab.AiSandbox.Startup` project and is copied to the output directory automatically.

```json
{
  "AggregationSettings": {
    "Steps": [
      { "Name": "Training",  "Mode": "Training" },
      { "Name": "Random AI", "Mode": "MassRandomAISimulation" },
      { "Name": "PPO - AI",  "Mode": "MassTrainedAISimulation" }
    ]
  }
}
```

Each step has two fields:

| Field | Description |
|---|---|
| `Name` | Human-readable label used as a column header in the CSV report |
| `Mode` | One of the `ExecutionMode` enum values (`Training`, `MassRandomAISimulation`, `MassTrainedAISimulation`) |

Steps are executed in the order they appear in the array. The `Training` step, if present, must appear before any `MassTrainedAISimulation` step so the model is ready.

### Output — report location and file name

The report is saved by `StatisticFileDataManager` into the **`STATISTICS`** sub-folder relative to the application's configured file-storage base path:

```
<FileStorage.BasePath>/STATISTICS/aggregation_<yyyy-MM-dd_HH-mm-ss>.csv
```

Example: `STATISTICS/aggregation_2026-03-01_14-30-00.csv`

### Report format (CSV)

The file is a **UTF-8 CSV** with commented section headers (`# …`). It is structured as follows:

| Section | Content |
|---|---|
| **Header** | Report date; comma-separated list of all step names |
| **Training Information** | Algorithm name, experiment ID and all hyperparameters (only present when a `Training` step ran) |
| **Standard Runs Comparison** | One data row; columns are `MaxTurns \| AvgTurns \| Wins \| WinPct` repeated for every mass-run step |
| **Incremental Sweep: \<property\>** | One section per swept property; rows = sweep-step values; same 4-column group per run type |
| **Area Sweep** | Single-row summary of the area-parameter sweep (when available) |

The `WinPct` column is `Wins / TotalRuns * 100` as a floating-point percentage (no `%` symbol).

## `RegisterCoreServices`
```csharp
services.AddEventAggregator();        // Common: IMessageBroker, IBrokerRpcClient
services.AddInfrastructureServices(); // Infrastructure: file & memory managers
services.AddDomainServices();         // Domain: IVisibilityService
services.AddApplicationServices();   // ApplicationServices: executors, commands, queries
services.AddAiSandboxServices(mode);  // AiTrainingOrchestrator: IAiActions, IPolicyTrainerClient
```

## `GrpcTrainingHost`
When `ExecutionMode = Training`, a `WebApplicationBuilder`-based host is used:
- Configures Kestrel on **port 50062** with HTTP/2 for gRPC.
- Registers `SimulationService` (gRPC).
- Calls `RegisterCoreServices` for the full DI stack.

## `MenuRunner`
Interactive console menu (uses plain `Console.ReadLine`).  
Overrides the defaults from `appsettings.json → StartupSettings`.  
Resides here (not in `ConsolePresentation`) to avoid a circular reference through `WebApi`.

## `appsettings.json` Key Settings

| Key | Default | Description |
|---|---|---|
| `StartupSettings.IsPreconditionStart` | `true` | Skip menu and use settings from file directly |
| `StartupSettings.ExecutionMode` | `MassRandomAISimulation` | Default mode when skipping menu |
| `StartupSettings.PresentationMode` | `WithoutVisualization` | Default presentation |
| `StartupSettings.StandardSimulationCount` | `0` | Number of standard batch runs |
| `SandBox.MaxTurns.Current` | `10` | Default turn limit |
| `SandBox.MapSettings.Size.Width/Height.Current` | `20` | Default map size |
| `PolicyTrainerClient.ServerAddress` | `http://localhost:50051` | Python service address |

## DI Lifetime Conventions

| Service | Lifetime | Reason |
|---|---|---|
| `IMessageBroker` | Singleton | All executors and presenters share one bus |
| `IMemoryDataManager<T>` | Singleton | Shared in-memory store across executors |
| `IFileDataManager<T>` | Scoped | Each scope (executor) gets its own file-access instance |
| `IPlaygroundCommandsHandleService` | Scoped | Per-execution to carry the active playground |
| `IExecutorForPresentation` | Scoped | One per simulation run |
