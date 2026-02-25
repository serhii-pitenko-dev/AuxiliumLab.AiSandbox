# AuxiliumLab.AiSandbox.WebApi

**Onion layer: Presentation / Host**  
Optional ASP.NET Core REST API that can run alongside the simulation engine.  
Depends on: `SharedBaseTypes`.

## Purpose
Provides an HTTP REST API entry point for external clients that want to interact with the simulation without using gRPC or the console interface. Enabled via `IsWebApiEnabled = true` in `appsettings.json` → `StartupSettings`.

## Running
The Web API is hosted as a background task launched from `Startup/Program.cs`:
```csharp
if (isWebEnabled)
    _ = WebApiHost.RunAsync(args, cancellationToken);
```
It does **not** block the rest of the application — simulation and Web API run concurrently.

## `WebApiHost`
Static entry point that builds and runs the `WebApplication`:
```csharp
WebApiHost.RunAsync(args, cancellationToken)
```
- Calls `builder.Services.AddWebApiPresentationServices()` for controller registration.
- Maps controllers via `app.MapControllers()`.

## Configuration
Add your Kestrel port configuration to `appsettings.json` or a dedicated `webapi.appsettings.json`:
```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": { "Url": "http://localhost:5000" }
    }
  }
}
```

## Adding a New Endpoint
1. Create a controller in `WebApi/` inheriting `ControllerBase`.
2. Inject required Application Service interfaces (commands/queries).
3. Register any additional services inside `AddWebApiPresentationServices()`.
4. Ensure the controller is in the same assembly as `WebApiHost` so `MapControllers()` discovers it.

## Current Status
The Web API project provides the hosting scaffolding.  
REST endpoints should be added here as the project grows. Current controllers:  
_(none yet — the project is a ready scaffold)_
