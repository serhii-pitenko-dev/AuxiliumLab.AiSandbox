# AuxiliumLab.AiSandbox gRPC Host

ASP.NET Core gRPC server exposing the simulation environment for external control.

## Quick Start

### Run the Server

```bash
dotnet run
```

The server will start on `http://localhost:50051`.

## Service Endpoints

### SimulationService

**Reset** - Reset the simulation environment
```protobuf
rpc Reset(ResetRequest) returns (ResetResponse);
```

**Step** - Execute a single step with an action
```protobuf
rpc Step(StepRequest) returns (StepResponse);
```

**Close** - Close/cleanup the simulation
```protobuf
rpc Close(CloseRequest) returns (CloseResponse);
```

## Current Implementation

⚠️ **Important**: This is a **stub implementation**. All methods return dummy values:
- `Reset()` returns a zero observation `[0, 0, 0, 0]`
- `Step()` returns zeros with reward=0, terminated=false
- `Close()` returns success=true

**Real simulation logic will be integrated later.**

## Testing

### From Python

```python
from auxilium_rl.infra.external_env_adapter import GrpcExternalEnvAdapter

adapter = GrpcExternalEnvAdapter("localhost:50051")
observation = adapter.reset(seed=42)
obs, reward, terminated, truncated, info = adapter.step(action=1)
adapter.close()
```

### Using the Test Script

```bash
# From auxilium_rl_service_baselines3/ directory
python test_grpc_communication.py
```

## Health Checks

The server includes gRPC health checks:

```bash
# Using grpcurl (if installed)
grpcurl -plaintext localhost:50051 grpc.health.v1.Health/Check
```

## Configuration

Edit [appsettings.json](appsettings.json) to change logging levels or other settings.

To change the port, modify [Program.cs](Program.cs):
```csharp
options.ListenLocalhost(50051, listenOptions => // Change port here
```

## Project Structure

```
AuxiliumLab.AiSandbox.GrpcHost/
├── Protos/
│   └── simulation.proto          # Service contract
├── Services/
│   └── SimulationService.cs      # Stub implementation
├── Examples/
│   └── PolicyTrainerClientExample.cs  # Example of calling Python
├── Program.cs                     # Startup & configuration
└── appsettings.json              # Settings
```

## Next Steps

1. ✅ gRPC infrastructure is set up
2. ✅ Stub implementation works
3. ⏭️ Integrate real simulation logic from AuxiliumLab.AiSandbox.Domain
4. ⏭️ Wire up actual game state to gRPC responses

See [GRPC_SETUP.md](../GRPC_SETUP.md) for complete documentation.
