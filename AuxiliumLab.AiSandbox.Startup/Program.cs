using AuxiliumLab.AiSandbox.Ai;
using AuxiliumLab.AiSandbox.Ai.Configuration;
using AuxiliumLab.AiSandbox.AiTrainingOrchestrator;
using AuxiliumLab.AiSandbox.AiTrainingOrchestrator.Configuration;
using AuxiliumLab.AiSandbox.AiTrainingOrchestrator.GrpcClients;
using AuxiliumLab.AiSandbox.ApplicationServices.Configuration;
using AuxiliumLab.AiSandbox.ApplicationServices.Executors;
using AuxiliumLab.AiSandbox.ApplicationServices.Runner.AggregationRunner;
using AuxiliumLab.AiSandbox.ApplicationServices.Runner.MassRunner;
using AuxiliumLab.AiSandbox.ApplicationServices.Runner.SingleRunner;
using AuxiliumLab.AiSandbox.ApplicationServices.Trainer;
using AuxiliumLab.AiSandbox.Common.Extensions;
using AuxiliumLab.AiSandbox.Common.MessageBroker;
using AuxiliumLab.AiSandbox.ConsolePresentation;
using AuxiliumLab.AiSandbox.ConsolePresentation.Configuration;
using AuxiliumLab.AiSandbox.Domain.Configuration;
using AuxiliumLab.AiSandbox.Domain.Statistics.Result;
using AuxiliumLab.AiSandbox.GrpcHost.Configuration;
using AuxiliumLab.AiSandbox.Infrastructure.Configuration;
using AuxiliumLab.AiSandbox.Infrastructure.Configuration.Preconditions;
using AuxiliumLab.AiSandbox.Infrastructure.FileManager;
using AuxiliumLab.AiSandbox.Infrastructure.MemoryManager;
using AuxiliumLab.AiSandbox.SharedBaseTypes.AiContract.Dto;
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
    .AddJsonFile("appsettings.json",            optional: false, reloadOnChange: false)
    .AddJsonFile("training-settings.json",      optional: false, reloadOnChange: false)
    .AddJsonFile("aggregation-settings.json",   optional: true,  reloadOnChange: false)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();


StartupSettings startupSettings =
    configuration.GetSection("StartupSettings").Get<StartupSettings>()
    ?? new StartupSettings();

AggregationSettings aggregationSettings =
    configuration.GetSection(AggregationSettings.SectionName).Get<AggregationSettings>()
    ?? new AggregationSettings();

ModelType? selectedAlgorithm = null;

// ── 2. Interactive menu (unless this is a precondition-driven run) ────────────
if (!startupSettings.IsPreconditionStart)
{
    TryClearConsole();
    var menu = new MenuRunner();
    (startupSettings, selectedAlgorithm) = menu.ResolveSettings(startupSettings);
    TryClearConsole();
}

bool aggIncludesTraining = startupSettings.ExecutionMode == ExecutionMode.AggregationRun
    && aggregationSettings.Steps.Any(s =>
        s.Mode.Equals("Training", StringComparison.OrdinalIgnoreCase));

bool isTraining   = startupSettings.ExecutionMode == ExecutionMode.Training || aggIncludesTraining;
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
            cfgBuilder.AddJsonFile("training-settings.json", optional: false, reloadOnChange: false);
            if (isConsole)
                cfgBuilder.AddConsoleConfigurationFile();
        })
        .ConfigureServices((ctx, services) =>
        {
            RegisterCoreServices(services, ctx.Configuration, startupSettings.ExecutionMode);

            // For trained simulation modes, override IAiActions with InferenceActions
            // which calls the Python Act RPC with the pre-trained model path.
            if (startupSettings.ExecutionMode is ExecutionMode.SingleTrainedAISimulation
                                              or ExecutionMode.MassTrainedAISimulation)
            {
                // Resolve the model type: prefer the interactively selected algorithm,
                // then fall back to what is configured in appsettings.json.
                var modelType = selectedAlgorithm ?? startupSettings.Algorithm;

                // Auto-discover the latest trained model for the chosen algorithm.
                var fileSourceCfg = ctx.Configuration
                    .GetSection(FileSourceConfiguration.SectionName)
                    .Get<FileSourceConfiguration>() ?? new FileSourceConfiguration();

                var algorithmFolder = Path.Combine(
                    fileSourceCfg.FileStorage.BasePath,
                    fileSourceCfg.FileStorage.TrainedAlgorithms,
                    modelType.ToString());

                var modelPath = Directory.Exists(algorithmFolder)
                    ? Directory.GetFiles(algorithmFolder)
                        .OrderByDescending(File.GetLastWriteTime)
                        .FirstOrDefault() ?? string.Empty
                    : string.Empty;

                var aiConfig = new AiConfiguration
                {
                    ModelType  = modelType,
                    Version    = "1.0",
                    PolicyType = startupSettings.PolicyType
                };
                services.AddScoped<IAiActions>(sp => new InferenceActions(
                    sp.GetRequiredService<IMessageBroker>(),
                    sp.GetRequiredService<IMemoryDataManager<AgentStateForAIDecision>>(),
                    sp.GetRequiredService<IPolicyTrainerClient>(),
                    modelPath,
                    aiConfig));
            }

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
    var fileSourceConfig     = host.Services.GetRequiredService<IOptions<FileSourceConfiguration>>();

    switch (startupSettings.ExecutionMode)
    {
        // ── Training ──────────────────────────────────────────────────────────
        case ExecutionMode.Training:
        {
            var runTraining = new TrainingRunner(
                host.Services,
                host.Services.GetRequiredService<TrainingSettings>(),
                host.Services.GetRequiredService<Sb3AlgorithmTypeProvider>(),
                host.Services.GetRequiredService<IPolicyTrainerClient>(),
                host.Services.GetRequiredService<GymBrokerRegistry>());

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

            // Build the CSV storage folder: FileStorage.BasePath / FileStorage.SavedSimulations
            string massRunStatsFolder = System.IO.Path.Combine(
                fileSourceConfig.Value.FileStorage.BasePath,
                fileSourceConfig.Value.FileStorage.SavedSimulations);
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

        // ── Single trained AI simulation ───────────────────────────────────────────
        case ExecutionMode.SingleTrainedAISimulation:
        {
            using var scope = host.Services.CreateScope();
            var executorFactory = scope.ServiceProvider.GetRequiredService<IExecutorFactory>();
            await new SingleRunner(sandboxConfiguration.Value).RunSingleTrainedAsync(
                executorFactory.CreateStandardExecutor());
            break;
        }

        // ── Mass trained AI simulations ──────────────────────────────────────────
        case ExecutionMode.MassTrainedAISimulation:
        {
            using var scope = host.Services.CreateScope();
            var executorFactory  = scope.ServiceProvider.GetRequiredService<IExecutorFactory>();
            var batchFileManager = scope.ServiceProvider.GetRequiredService<IFileDataManager<GeneralBatchRunInformation>>();

            string massRunStatsFolder = System.IO.Path.Combine(
                fileSourceConfig.Value.FileStorage.BasePath,
                fileSourceConfig.Value.FileStorage.SavedSimulations);
            var statisticFileManager = new StatisticFileDataManager(massRunStatsFolder);

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

        // ── Not yet implemented ────────────────────────────────────────────────────
        case ExecutionMode.LoadSimulation:
            throw new NotImplementedException("LoadSimulation is not yet implemented.");

        // ── Aggregation run ────────────────────────────────────────────────────────
        case ExecutionMode.AggregationRun:
        {
            string aggStatsFolder = System.IO.Path.Combine(
                fileSourceConfig.Value.FileStorage.BasePath,
                fileSourceConfig.Value.FileStorage.SavedSimulations);
            var aggStatisticFileManager = new StatisticFileDataManager(aggStatsFolder);

            string algorithmsFolderPath = System.IO.Path.Combine(
                fileSourceConfig.Value.FileStorage.BasePath,
                fileSourceConfig.Value.FileStorage.TrainedAlgorithms);

            var aggRunner = new AggregationRunner(
                host.Services,
                host.Services.GetRequiredService<TrainingSettings>(),
                host.Services.GetRequiredService<Sb3AlgorithmTypeProvider>(),
                host.Services.GetRequiredService<IPolicyTrainerClient>(),
                host.Services.GetRequiredService<GymBrokerRegistry>(),
                aggStatisticFileManager,
                sandboxConfiguration,
                algorithmsFolderPath);

            // Map aggregation steps from settings.
            var aggStepConfigs = aggregationSettings.Steps
                .Select(s => new AggregationStepConfiguration(
                    s.Name,
                    Enum.TryParse<ExecutionMode>(s.Mode, ignoreCase: true, out var parsedMode)
                        ? parsedMode
                        : throw new InvalidOperationException(
                            $"Unknown ExecutionMode '{s.Mode}' in aggregation-settings.json.")))
                .ToList();

            var aggIncrementalProperties = new SimulationIncrementalPropertiesSettings
            {
                SimulationCount = startupSettings.IncrementalProperties.SimulationCount,
                Properties      = startupSettings.IncrementalProperties.Properties,
            };

            await aggRunner.RunAggregationAsync(
                aggStepConfigs,
                startupSettings.StandardSimulationCount,
                aggIncrementalProperties,
                selectedAlgorithm ?? startupSettings.Algorithm,
                startupSettings.PolicyType,
                host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping);
            break;
        }
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
    services.AddPolicyTrainerClient(configuration);
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

