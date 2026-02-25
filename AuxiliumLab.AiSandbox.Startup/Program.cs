using AuxiliumLab.AiSandbox.Ai;
using AuxiliumLab.AiSandbox.Ai.Configuration;
using AuxiliumLab.AiSandbox.AiTrainingOrchestrator.Configuration;
using AuxiliumLab.AiSandbox.AiTrainingOrchestrator.GrpcClients;
using AuxiliumLab.AiSandbox.ApplicationServices.Configuration;
using AuxiliumLab.AiSandbox.ApplicationServices.Executors;
using AuxiliumLab.AiSandbox.ApplicationServices.Runner.MassRunner;
using AuxiliumLab.AiSandbox.ApplicationServices.Runner.SingleRunner;
using AuxiliumLab.AiSandbox.ApplicationServices.Trainer;
using AuxiliumLab.AiSandbox.Common.Extensions;
using AuxiliumLab.AiSandbox.ConsolePresentation;
using AuxiliumLab.AiSandbox.ConsolePresentation.Configuration;
using AuxiliumLab.AiSandbox.Domain.Configuration;
using AuxiliumLab.AiSandbox.Domain.Statistics.Result;
using AuxiliumLab.AiSandbox.GrpcHost.Configuration;
using AuxiliumLab.AiSandbox.Infrastructure.Configuration;
using AuxiliumLab.AiSandbox.Infrastructure.Configuration.Preconditions;
using AuxiliumLab.AiSandbox.Infrastructure.FileManager;
using AuxiliumLab.AiSandbox.SharedBaseTypes.ValueObjects.StartupSettings;
using AuxiliumLab.AiSandbox.Startup.Configuration;
using AuxiliumLab.AiSandbox.Startup.Menu;
using AuxiliumLab.AiSandbox.Statistics.Preconditions;
using AuxiliumLab.AiSandbox.Statistics.StatisticDataManager;
using AuxiliumLab.AiSandbox.WebApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;


// ── 1. Read settings early (before building host) ────────────────────────────
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();


StartupSettings startupSettings =
    configuration.GetSection("StartupSettings").Get<StartupSettings>()
    ?? new StartupSettings();

ModelType? selectedAlgorithm = null;

// ── 2. Interactive menu (unless this is a precondition-driven run) ────────────
if (!startupSettings.IsPreconditionStart)
{
    TryClearConsole();
    var menu = new MenuRunner();
    (startupSettings, selectedAlgorithm) = menu.ResolveSettings(startupSettings);
    TryClearConsole();
}

bool isTraining   = startupSettings.ExecutionMode == ExecutionMode.Training;
bool isConsole    = startupSettings.PresentationMode == PresentationMode.Console;
bool isWebEnabled = startupSettings.IsWebApiEnabled;

// ── 3. Build host ─────────────────────────────────────────────────────────────
//
//   Training  → WebApplication (needs Kestrel to host the gRPC server on 50062)
//   All else  → Generic Host   (pure console, zero Kestrel / HTTP pipeline)
//
IHost host;

if (isTraining)
{
    // ── Training path: gRPC + Kestrel (all internals owned by GrpcTrainingHost) ──
    host = GrpcTrainingHost.Build(args, builder =>
    {
        RegisterCoreServices(builder.Services, builder.Configuration, startupSettings.ExecutionMode);
    });
}
else
{
    // ── Console / Simulation path: pure generic host, no Kestrel ─────────────
    // ConfigureAppConfiguration runs before ConfigureServices, so Settings.json
    // is available when AddConsolePresentationServices reads ConsoleSettings.
    host = Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((_, cfgBuilder) =>
        {
            if (isConsole)
                cfgBuilder.AddConsoleConfigurationFile();
        })
        .ConfigureServices((ctx, services) =>
        {
            RegisterCoreServices(services, ctx.Configuration, startupSettings.ExecutionMode);

            if (isConsole)
                services.AddConsolePresentationServices(ctx.Configuration);
        })
        .Build();
}

// ── 4. Execute ────────────────────────────────────────────────────────────────
if (isConsole)
    host.Services.GetRequiredService<IConsoleRunner>().Run();

await host.StartAsync();

// ── 5. Launch WebApi in-process (own Kestrel, own DI) ────────────────────────
Task? webApiTask = null;
if (isWebEnabled)
    webApiTask = WebApiHost.RunAsync(
        args,
        host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping);

try
{
    var sandboxConfiguration = host.Services.GetRequiredService<IOptions<SandBoxConfiguration>>();

    switch (startupSettings.ExecutionMode)
    {
        // ── Training ──────────────────────────────────────────────────────────
        case ExecutionMode.Training:
        {
            var runTraining = new TrainingRunner(
                host.Services,
                host.Services.GetRequiredService<TrainingSettings>(),
                host.Services.GetRequiredService<Sb3AlgorithmTypeProvider>(),
                host.Services.GetRequiredService<IPolicyTrainerClient>());

            await runTraining.RunTrainingAsync(
                selectedAlgorithm ?? ModelType.PPO,
                host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping);
            break;
        }

        // ── Single random simulation ──────────────────────────────────────────
        case ExecutionMode.SingleRandomAISimulation:
        {
            using var scope = host.Services.CreateScope();
            var executor = scope.ServiceProvider.GetRequiredService<IExecutorForPresentation>();
            await new SingleRunner(sandboxConfiguration.Value).RunSingleAsync(executor);
            break;
        }

        // ── Mass random simulations ───────────────────────────────────────────
        case ExecutionMode.MassRandomAISimulation:
        {
            using var scope = host.Services.CreateScope();
            var executorFactory = scope.ServiceProvider.GetRequiredService<IExecutorFactory>();
            var batchFileManager = scope.ServiceProvider.GetRequiredService<IFileDataManager<GeneralBatchRunInformation>>();

            // Build the CSV storage folder: FileSource.Path / MASS_RUN_STATISTICS
            string massRunStatsFolder = System.IO.Path.Combine(
                sandboxConfiguration.Value.MapSettings.FileSource.Path,
                "MASS_RUN_STATISTICS");
            var statisticFileManager = new StatisticFileDataManager(massRunStatsFolder);

            // Map startup settings (excluding IsPreconditionStart and PresentationMode)
            var simulationStartupSettings = new SimulationStartupSettings
            {
                PolicyType              = startupSettings.PolicyType.ToString(),
                ExecutionMode           = startupSettings.ExecutionMode.ToString(),
                StandardSimulationCount = startupSettings.StandardSimulationCount,
                IncrementalProperties   = new SimulationIncrementalPropertiesSettings
                {
                    SimulationCount = startupSettings.IncrementalProperties.SimulationCount,
                    Properties      = startupSettings.IncrementalProperties.Properties,
                },
            };

            await new MassRunner(batchFileManager, statisticFileManager, sandboxConfiguration)
                .RunManyAsync(
                    executorFactory,
                    startupSettings.StandardSimulationCount,
                    startupSettings: simulationStartupSettings);
            break;
        }

        // ── Test preconditions ────────────────────────────────────────────────
        case ExecutionMode.TestPreconditions:
        {
            using var scope = host.Services.CreateScope();
            var executor = scope.ServiceProvider.GetRequiredService<IExecutorForPresentation>();
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
    if (webApiTask is not null)
        await webApiTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

    // Training keeps the gRPC host alive until signalled; all others just stop.
    if (isTraining)
        await host.WaitForShutdownAsync();
    else
        await host.StopAsync();
}

// ── Helper Methods ────────────────────────────────────────────────────────────
static void RegisterCoreServices(
    IServiceCollection services,
    IConfiguration configuration,
    ExecutionMode executionMode)
{
    services.AddEventAggregator();
    services.AddInfrastructureServices(configuration);
    services.AddDomainServices();
    services.AddApplicationServices();
    services.AddAiSandboxServices(executionMode);
}

static void TryClearConsole()
{
    try
    {
        Console.Clear();
    }
    catch (IOException ex)
    {
        Console.WriteLine($"Warning: Cannot clear console - {ex.Message}");
        Console.WriteLine("(This is expected when debugging in VS Code)");
    }
}

