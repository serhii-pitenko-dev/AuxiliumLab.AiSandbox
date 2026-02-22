using AiSandBox.Ai;
using AiSandBox.Ai.Configuration;
using AiSandBox.AiTrainingOrchestrator.Configuration;
using AiSandBox.AiTrainingOrchestrator.GrpcClients;
using AiSandBox.ApplicationServices.Configuration;
using AiSandBox.ApplicationServices.Executors;
using AiSandBox.ApplicationServices.Runner.MassRunner;
using AiSandBox.ApplicationServices.Runner.SingleRunner;
using AiSandBox.ApplicationServices.Trainer;
using AiSandBox.Common.Extensions;
using AiSandBox.ConsolePresentation;
using AiSandBox.ConsolePresentation.Configuration;
using AiSandBox.Domain.Configuration;
using AiSandBox.Domain.Statistics.Result;
using AiSandBox.GrpcHost.Services;
using AiSandBox.Infrastructure.Configuration;
using AiSandBox.Infrastructure.Configuration.Preconditions;
using AiSandBox.Infrastructure.FileManager;
using AiSandBox.SharedBaseTypes.ValueObjects.StartupSettings;
using AiSandBox.Startup.Configuration;
using AiSandBox.Startup.Menu;
using AiSandBox.WebApi.Configuration;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;


// ── 1. Read default settings ─────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

StartupSettings startupSettings =
    builder.Configuration.GetSection("StartupSettings").Get<StartupSettings>()
    ?? new StartupSettings();

ModelType? selectedAlgorithm = null;

// ── 2. Interactive menu (unless this is a precondition-driven run) ────────────
if (!startupSettings.IsPreconditionStart)
{
    Console.Clear();
    var menu = new MenuRunner();
    (startupSettings, selectedAlgorithm) = menu.ResolveSettings(startupSettings);
    Console.Clear();
}

bool isTraining   = startupSettings.ExecutionMode == ExecutionMode.Training;
bool isConsole    = startupSettings.PresentationMode == PresentationMode.Console;
bool isWebEnabled = startupSettings.IsWebApiEnabled;

// ── 3. Register core services ─────────────────────────────────────────────────
builder.Services.AddEventAggregator();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddDomainServices();
builder.Services.AddApplicationServices();
builder.Services.AddAiSandBoxServices(startupSettings.ExecutionMode);

// ── 4. Training-specific services ─────────────────────────────────────────────
if (isTraining)
{
    // Load training-settings.json
    builder.Configuration.AddJsonFile("training-settings.json", optional: false, reloadOnChange: false);
    var trainingSettings =
        builder.Configuration.GetSection("TrainingSettings").Get<TrainingSettings>()
        ?? new TrainingSettings();
    builder.Services.AddSingleton(trainingSettings);

    // PolicyTrainer gRPC client (C# → Python)
    builder.Services.AddPolicyTrainerClient(builder.Configuration);

    // gRPC server (Python → C#) on port 50062
    builder.Services.AddGrpc();
    builder.WebHost.ConfigureKestrel(opts =>
    {
        opts.ListenLocalhost(50062, lo => lo.Protocols = HttpProtocols.Http2);
    });
}

// ── 5. Presentation-specific services ─────────────────────────────────────────
if (isConsole)
{
    builder.Services.AddConsolePresentationServices(builder.Configuration, builder.Configuration);
}

if (isWebEnabled)
    builder.Services.AddWebApiPresentationServices();

// ── 6. Build ─────────────────────────────────────────────────────────────────
var app = builder.Build();

if (isTraining)
    app.MapGrpcService<SimulationService>();

if (isWebEnabled)
    app.MapControllers();

// ── 7. Execute ────────────────────────────────────────────────────────────────
if (isConsole)
    app.Services.GetRequiredService<IConsoleRunner>().Run();

await app.StartAsync();

try
{
    var sandboxConfiguration = app.Services.GetRequiredService<IOptions<SandBoxConfiguration>>();
    
    switch (startupSettings.ExecutionMode)
    {
        // ── Training ──────────────────────────────────────────────────────────
        case ExecutionMode.Training:
        {
            var runTraining = new TrainingRunner(
                app.Services,
                app.Services.GetRequiredService<TrainingSettings>(),
                app.Services.GetRequiredService<Sb3AlgorithmTypeProvider>(),
                app.Services.GetRequiredService<IPolicyTrainerClient>());

            await runTraining.RunTrainingAsync(
                selectedAlgorithm ?? ModelType.PPO,
                app.Lifetime.ApplicationStopping);
            break;
        }

        // ── Single random simulation ──────────────────────────────────────────
        case ExecutionMode.SingleRandomAISimulation:
        {
            using var scope = app.Services.CreateScope();
            var executor = scope.ServiceProvider.GetRequiredService<IExecutorForPresentation>();
            var batchFileManager = scope.ServiceProvider.GetRequiredService<IFileDataManager<GeneralBatchRunInformation>>();
            await new SingleRunner(sandboxConfiguration.Value).RunSingleAsync(executor);
            break;
        }

        // ── Mass random simulations ───────────────────────────────────────────
        case ExecutionMode.MassRandomAISimulation:
        {
            using var scope = app.Services.CreateScope();
            var executor = scope.ServiceProvider.GetRequiredService<IStandardExecutor>();
            var batchFileManager = scope.ServiceProvider.GetRequiredService<IFileDataManager<GeneralBatchRunInformation>>();
            await new MassRunner(batchFileManager, sandboxConfiguration).RunManyAsync(executor, startupSettings.SimulationCount);
            break;
        }

        // ── Test preconditions ────────────────────────────────────────────────
        case ExecutionMode.TestPreconditions:
        {
            using var scope = app.Services.CreateScope();
            var executor = scope.ServiceProvider.GetRequiredService<IExecutorForPresentation>();
            var batchFileManager = scope.ServiceProvider.GetRequiredService<IFileDataManager<GeneralBatchRunInformation>>();
            await new SingleRunner(sandboxConfiguration.Value).RunTestPreconditionsAsync(executor);
            break;
        }

        // ── Not yet implemented ───────────────────────────────────────────────
        case ExecutionMode.SingleTrainedAISimulation:
            throw new NotImplementedException("SingleTrainedAISimulation is not yet implemented.");
        case ExecutionMode.MassTrainedAISimulation:
            throw new NotImplementedException("MassTrainedAISimulation is not yet implemented.");
        case ExecutionMode.LoadSimulation:
            throw new NotImplementedException("LoadSimulation is not yet implemented.");
    }
}
finally
{
    // Keep alive only when WebApi or training needs the host running
    if (isWebEnabled || isTraining)
        await app.WaitForShutdownAsync();
    else
        await app.StopAsync();
}
