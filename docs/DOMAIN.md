# Domain Model Reference

> For the full project README see [../AuxiliumLab.AiSandbox.Domain/README.md](../AuxiliumLab.AiSandbox.Domain/README.md).  
> For architecture context see [ARCHITECTURE.md](ARCHITECTURE.md).

## Map

The simulation runs on a `MapSquareCells` — a 2D grid of `Cell` objects.

- **Coordinate system:** `(0, 0)` is the **bottom-left** corner. X increases right, Y increases up.
- Every cell is always occupied. An unoccupied cell holds an `EmptyCell` (Null Object pattern).
- `MapSquareCells.MoveObject(from, to)` is the only way to relocate objects — it enforces source occupancy and target emptiness.

### Map Objects (`SandboxMapBaseObject`)

| Type | `ObjectType` | `Transparent` | Can move |
|---|---|---|---|
| `EmptyCell` | `Empty` | `true` | — |
| `Block` | `Block` | `false` | No |
| `Exit` | `Exit` | `true` | No |
| `Hero` | `Hero` | `true` | Yes |
| `Enemy` | `Enemy` | `true` | Yes |

`Transparent = false` means the object blocks line-of-sight for vision calculations.

## Agents

Both `Hero` and `Enemy` inherit from `Agent` which inherits `SandboxMapBaseObject`.

### Key Agent Properties
| Property | Description |
|---|---|
| `Speed` | Number of cells the agent can move per turn |
| `SightRange` | Circular radius for vision (Bresenham line-of-sight within circle) |
| `Stamina` / `MaxStamina` | Running budget — depleted each turn while `IsRun = true` |
| `IsRun` | Sprint toggle; increases effective movement range |
| `PathToTarget` | A* navigation path (set externally by AI / pathfinding) |
| `VisibleCells` | Updated by `VisibilityService` at the start of each turn |
| `AvailableActions` | Valid actions for the current turn (recalculated each turn) |
| `ExecutedActions` | Actions already taken this turn |
| `OrderInTurnQueue` | Determines processing order when multiple agents act |

### Agent Actions (`AgentAction` enum)
- `Move` — move one cell towards a target direction.
- `Run` — toggle sprint mode (costs stamina, increases effective speed).

## Playground (Aggregate Root)

`StandardPlayground` is the aggregate root. **All map mutations go through it.**

```
StandardPlayground
  ├── MapSquareCells  (private)
  ├── Hero?
  ├── Exit?
  ├── IReadOnlyCollection<Block>
  ├── IReadOnlyCollection<Enemy>
  └── IVisibilityService  (injected)
```

Key turn lifecycle methods:
1. `OnStartTurnActions()` — increments `Turn` counter.
2. `PrepareAgentForTurnActions(agent)` — resets stamina, recalculates available actions.
3. AI or rule decides action.
4. `MoveObject(from, to)` — executes the move.
5. `LookAroundEveryone()` — refreshes all agents' `VisibleCells`.

## Vision System

Uses **Bresenham's line algorithm** for ray-casting:
1. Extract a square sub-grid centred on the agent (`CutMapPart`).
2. For each cell in the sub-grid within the circular radius, cast a ray.
3. A ray is blocked at the first cell where `SandboxMapBaseObject.Transparent = false` (`Block`).
4. Visible cells are stored as **live references** to the actual map cells.

## Validation

| Validator | Checks |
|---|---|
| `MapValidator` | Hero present, exit present, map dimensions ≥ 1 |
| `AgentActionAddValidator` | No duplicate actions; action is valid for the agent's current state |

## Factory / Builder Pattern

```
PlaygroundFactory          → creates a random playground from SandBoxConfiguration
PlaygroundFromFileFactory  → loads a playground from a saved StandardPlaygroundState file
PlaygroundBuilder          → fluent builder used by both factories
HeroFactory                → creates Hero from InitialAgentCharacters
EnemyFactory               → creates Enemy from InitialAgentCharacters
```
