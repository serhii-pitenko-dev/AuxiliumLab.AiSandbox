using AiSandBox.GrpcHost.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on port 50062 with HTTP/2
builder.WebHost.ConfigureKestrel(options =>
{
    // Listen on port 50062 for gRPC (HTTP/2) - SimulationService for Python to call
    // Port 50051 is used by Python PolicyTrainerService (for C# to call)
    // Ports 50052-50061 reserved for future services
    options.ListenLocalhost(50062, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
});

// Add gRPC services
builder.Services.AddGrpc();

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Add health checks
builder.Services.AddGrpcHealthChecks()
    .AddCheck("simulation_service", () => 
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

var app = builder.Build();

// Map gRPC services
app.MapGrpcService<SimulationService>();

// Map health check service
app.MapGrpcHealthChecksService();

// Optional: add a default endpoint for debugging
app.MapGet("/", () => "gRPC Simulation Service is running. Use a gRPC client to communicate.");

app.Logger.LogInformation("Starting AiSandBox SimulationService on http://localhost:50062");

app.Run();
