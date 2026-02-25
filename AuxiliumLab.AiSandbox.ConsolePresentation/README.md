# AuxiliumLab.AiSandbox.ConsolePresentation

**Onion layer: Presentation**  
Real-time terminal renderer for interactive simulation runs.  
Depends on: `ApplicationServices`, `Common`, `SharedBaseTypes`, `Infrastructure`.

> **Note:** `MenuRunner` was moved to `AuxiliumLab.AiSandbox.Startup/Menu/` to avoid a circular dependency (`Startup → WebApi → ConsolePresentation`). The stub `MenuRunner.cs` that remains here is a placeholder comment only.

## Purpose
Renders the simulation grid, agent actions, and game events live in the terminal using **Spectre.Console**.  
Subscribes to `IMessageBroker` events published by the executor and reacts without any direct coupling to the simulation engine.

## Key Classes

### `ConsoleRunner` (`IConsoleRunner`)
The main presenter. Created and started by `Startup` when `PresentationMode = Console`.

**Lifecycle:**
1. Constructor: receives `IMessageBroker`, `IMapQueriesHandleService`, `ConsoleSettings`.
2. `Run()`: initialises the console, subscribes to all relevant events.
3. Event handlers react to the message broker:

| Event | Handler | Action |
|---|---|---|
| `GameStartedEvent` | `OnGameStarted` | Loads state, queries full map layout, renders background + full map |
| `OnBaseAgentActionEvent` | `OnAgentActionEvent` | Re-renders only the cells affected by the action |
| `TurnExecutedEvent` | `OnTurnEnded` | Refreshes turn counter and status panel |
| `HeroWonEvent` | `OnGameWon` | Renders win screen |
| `HeroLostEvent` | `OnGameLost` | Renders loss screen |

**Partial rendering optimisation:**  
On each action event, `ConsoleRunner` calls `IMapQueriesHandleService.GetAffectedCells()` and re-renders only those cells. The full map is only rendered once on game start.

### `IConsoleRunner`
```csharp
void Run();
event Action<Guid>? ReadyForRendering;
```
`ReadyForRendering` is fired once the renderer is initialised, signalling the executor it can begin publishing events.

## Configuration — `ConsoleSettings` / `Settings.json`

| Setting | Type | Description |
|---|---|---|
| `ConsoleSize.Width` | `int` | Console window width in characters |
| `ConsoleSize.Height` | `int` | Console window height in characters |
| `ColorScheme.GlobalBackGroundColor` | `string` | ANSI background colour |
| `ColorScheme.*` | per-object | Colour for Hero, Enemy, Block, Exit, Empty cells |
| `ActionTimeout` | `int` (ms) | Delay between rendered actions (controls animation speed) |

Settings are loaded from `AuxiliumLab.AiSandbox.ConsolePresentation/Settings.json` and injected as `IOptions<ConsoleSettings>`.

## Cell Rendering
Each `ObjectType` maps to a configurable character + colour:
- `Empty` → space / background colour
- `Block` → `█` / wall colour
- `Hero` → `H` / hero colour
- `Enemy` → `E` / enemy colour
- `Exit` → `X` / exit colour

## Rendering Architecture
```
IMessageBroker ──► ConsoleRunner
                      │
                      ├── IMapQueriesHandleService.MapLayoutQuery.GetFromMemory()
                      │     └── Returns MapLayoutResponse (full grid of MapCell DTOs)
                      │
                      ├── IMapQueriesHandleService.GetAffectedCells()
                      │     └── Returns only changed cells since last render
                      │
                      └── Spectre.Console writes to terminal buffer
```

## Adding a New Object Type to the Renderer
1. Add colour/character config to `ColorScheme` in `Settings.json`.
2. Map the new `ObjectType` value in the `_cellData` dictionary inside `ConsoleRunner`.
3. No changes needed in Domain or Application layers.
