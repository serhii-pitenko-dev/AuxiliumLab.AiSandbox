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
  ├─ 1. Read appsettings.json  →  StartupSettings
  │
  ├─ 2. Interactive menu (unless IsPreconditionStart = true)
  │       MenuRunner.ResolveSettings()
  │         ├─ Choose PresentationMode (Console / Web / WithoutVisualization)
  │         ├─ Choose ExecutionMode
  │         └─ Choose Algorithm (Training mode only)
  │
  ├─ 3. Build host
  │       Training  →  GrpcTrainingHost (Kestrel + HTTP/2 on :50062)
  │       All else  →  Host.CreateDefaultBuilder (pure generic host)
  │
  ├─ 4. Start ConsoleRunner (if Console mode)
  │
  ├─ 5. host.StartAsync()
  │
  ├─ 6. Optionally launch WebApiHost in background (if IsWebApiEnabled)
  │
  └─ 7. Dispatch on ExecutionMode:
          Training                 → TrainingRunner.RunTrainingAsync()
          SingleRandomAISimulation → SingleRunner.RunSingleAsync()
          MassRandomAISimulation   → MassRunner.RunManyAsync()
          TestPreconditions        → SingleRunner.RunTestPreconditionsAsync()
          (others not yet implemented)
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
