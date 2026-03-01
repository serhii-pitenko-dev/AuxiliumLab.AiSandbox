using AuxiliumLab.AiSandbox.AiTrainingOrchestrator.Configuration;
using AuxiliumLab.AiSandbox.GrpcHost.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace AuxiliumLab.AiSandbox.GrpcHost.Configuration;

/// <summary>
/// Encapsulates all WebApplication / Kestrel / gRPC wiring required for the training mode.
/// Program.cs only calls <see cref="Build"/> and never touches
/// <see cref="WebApplication.CreateBuilder"/> or Kestrel directly.
/// </summary>
public static class GrpcTrainingHost
{
    /// <summary>
    /// Creates a <see cref="WebApplication"/> pre-configured for the training gRPC server on
    /// port 50062. The optional <paramref name="configure"/> callback lets the caller (Startup)
    /// register its own core services without knowing about Kestrel or gRPC internals.
    /// </summary>
    /// <param name="args">Command-line arguments forwarded to <see cref="WebApplication.CreateBuilder"/>.</param>
    /// <param name="configure">
    ///   Optional callback invoked with the <see cref="WebApplicationBuilder"/> before any
    ///   gRPC-specific services are added. Use it to register domain/application services and
    ///   optionally console-presentation services.
    /// </param>
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);

        // ── Caller registers core + presentation services ─────────────────────
        configure?.Invoke(builder);

        // ── Training-specific: settings ───────────────────────────────────────
        builder.Configuration.AddJsonFile("training-settings.json", optional: false, reloadOnChange: false);
        var trainingSettings =
            builder.Configuration.GetSection("TrainingSettings").Get<TrainingSettings>()
            ?? new TrainingSettings();
        builder.Services.AddSingleton(trainingSettings);

        // ── Training-specific: gRPC server (Python → C#) on port 50062 ───────
        builder.Services.AddGrpc();
        builder.WebHost.ConfigureKestrel(opts =>
            opts.ListenLocalhost(50062, lo => lo.Protocols = HttpProtocols.Http2));

        // ── Build and map ─────────────────────────────────────────────────────
        var app = builder.Build();
        app.MapGrpcService<SimulationService>();
        return app;
    }
}
