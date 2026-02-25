# AuxiliumLab.AiSandbox

A grid-based simulation engine written in **.NET 9** for testing and training reinforcement-learning agents.  
The solution follows **Onion Architecture** (also known as Clean / Hexagonal Architecture).

## Goals
- Provide a deterministic, configurable grid world for RL experiments.
- Support multiple execution modes: interactive console, batch simulation, AI training, and REST API.
- Bidirectional gRPC integration with a Python RL training service (Stable Baselines3).

## Non-goals
- No real-time graphics.
- No multiplayer.

## Onion Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│  Presentation / Hosts                                           │
│  ConsolePresentation · GrpcHost · WebApi · Startup             │
├─────────────────────────────────────────────────────────────────┤
│  Application Services                                           │
│  ApplicationServices · AiTrainingOrchestrator                  │
├─────────────────────────────────────────────────────────────────┤
│  Infrastructure (implements domain ports)                       │
│  Infrastructure · Statistics                                    │
├─────────────────────────────────────────────────────────────────┤
│  Domain (innermost — no outward dependencies)                   │
│  Domain · Common · SharedBaseTypes                              │
└─────────────────────────────────────────────────────────────────┘
```

Dependency rule: **inner layers never reference outer layers**.  
The `Domain` project has zero dependencies on Infrastructure, Application, or Presentation.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the full dependency graph.

## Solution Structure

| Project | Layer | Purpose |
|---|---|---|
| `AuxiliumLab.AiSandbox.Domain` | Domain | Map, agents, game rules, vision |
| `AuxiliumLab.AiSandbox.SharedBaseTypes` | Domain | Value objects, enums, message contracts |
| `AuxiliumLab.AiSandbox.Common` | Domain/Cross-cutting | In-process pub/sub message broker |
| `AuxiliumLab.AiSandbox.ApplicationServices` | Application | Use-cases, executors, runners, persistence mappers |
| `AuxiliumLab.AiSandbox.Infrastructure` | Infrastructure | File & in-memory data managers |
| `AuxiliumLab.AiSandbox.Statistics` | Infrastructure | Batch run statistics, CSV export |
| `AuxiliumLab.AiSandbox.ConsolePresentation` | Presentation | Real-time Spectre.Console terminal renderer |
| `AuxiliumLab.AiSandbox.AiTrainingOrchestrator` | Application | Training settings, gRPC client to Python |
| `AuxiliumLab.AiSandbox.GrpcHost` | Presentation | gRPC server exposing simulation as a gym |
| `AuxiliumLab.AiSandbox.WebApi` | Presentation | Optional ASP.NET Core REST API |
| `AuxiliumLab.AiSandbox.Startup` | Composition Root | DI wiring, entry point, menu |
| `AuxiliumLab.AiSandbox.UnitTests` | Tests | MSTest unit test suite |

## Build & Run

```powershell
# Build everything
dotnet build AuxiliumLab.AiSandbox.sln

# Run (interactive menu will appear)
dotnet run --project AuxiliumLab.AiSandbox.Startup
```

## Execution Modes

| Mode | Description |
|---|---|
| `SingleSimulation` | One interactive run with console visualization |
| `MassRandomAISimulation` | Parallel batch runs without visualization |
| `Training` | Full RL training loop (requires Python service running) |
| `TestPreconditions` | Run seeded from saved precondition data |

## Documentation

| File | Contents |
|---|---|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Onion architecture, dependency graph, data flow |
| [docs/DOMAIN.md](docs/DOMAIN.md) | Domain model reference |
| [docs/APPLICATION_SERVICES.md](docs/APPLICATION_SERVICES.md) | Application layer execution logic |
| [docs/CONSOLE_PRESENTATION.md](docs/CONSOLE_PRESENTATION.md) | Console rendering system |
| [CONTRIBUTING.md](CONTRIBUTING.md) | Contribution guidelines |
