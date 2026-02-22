using AiSandBox.Ai;
using AiSandBox.Ai.Configuration;
using AiSandBox.AiTrainingOrchestrator.Configuration;
using AiSandBox.AiTrainingOrchestrator.GrpcClients;
using AiSandBox.AiTrainingOrchestrator.Trainers;
using AiSandBox.ApplicationServices.Executors;
using AiSandBox.ApplicationServices.Runner.LogsDto;
using AiSandBox.ApplicationServices.Runner.LogsDto.Performance;
using AiSandBox.ApplicationServices.Runner.TestPreconditionSet;
using AiSandBox.ApplicationServices.Saver.Persistence.Sandbox.Mappers;
using AiSandBox.ApplicationServices.Saver.Persistence.Sandbox.States;
using AiSandBox.Common.MessageBroker;
using AiSandBox.Domain.Playgrounds;
using AiSandBox.Domain.Statistics.Entities;
using AiSandBox.Infrastructure.Configuration.Preconditions;
using AiSandBox.Infrastructure.FileManager;
using AiSandBox.Infrastructure.MemoryManager;
using AiSandBox.SharedBaseTypes.AiContract.Dto;
using AiSandBox.SharedBaseTypes.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiSandBox.ApplicationServices.Trainer;

public class TrainingRunner
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TrainingSettings _trainingSettings;
    private readonly Sb3AlgorithmTypeProvider _algorithmTypeProvider;
    private readonly IPolicyTrainerClient _policyTrainerClient;

    public TrainingRunner(
        IServiceProvider serviceProvider,
        TrainingSettings trainingSettings,
        Sb3AlgorithmTypeProvider algorithmTypeProvider,
        IPolicyTrainerClient policyTrainerClient)
    {
        _serviceProvider = serviceProvider;
        _trainingSettings = trainingSettings;
        _algorithmTypeProvider = algorithmTypeProvider;
        _policyTrainerClient = policyTrainerClient;
    }

    public async Task RunTrainingAsync(ModelType algorithmType, CancellationToken cancellationToken = default)
    {
        // 1. Find the settings for the selected algorithm
        string algorithmName = algorithmType.ToString().ToUpper();
        var algoSettings = _trainingSettings.Algorithms
            .FirstOrDefault(a => a.Algorithm.Equals(algorithmName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"No training settings found for algorithm '{algorithmName}' in training-settings.json.");

        // 2. Instantiate the correct Training class
        ITraining training = algorithmType switch
        {
            ModelType.PPO => new PpoTraining(isSameMachine: true, algoSettings),
            ModelType.A2C => new A2cTraining(isSameMachine: true, algoSettings),
            ModelType.DQN => new DqnTraining(isSameMachine: true, algoSettings),
            _ => throw new NotImplementedException($"Training for algorithm '{algorithmType}' is not implemented.")
        };

        // 3. Resolve shared singleton dependencies
        var messageBroker = _serviceProvider.GetRequiredService<IMessageBroker>();
        var agentStateRepo = _serviceProvider.GetRequiredService<IMemoryDataManager<AgentStateForAIDecision>>();
        var playgroundRepo = _serviceProvider.GetRequiredService<IMemoryDataManager<StandardPlayground>>();
        var statisticsRepo = _serviceProvider.GetRequiredService<IMemoryDataManager<PlayGroundStatistics>>();
        var statFileRepo = _serviceProvider.GetRequiredService<IFileDataManager<PlayGroundStatistics>>();
        var playgroundStateFileRepo = _serviceProvider.GetRequiredService<IFileDataManager<StandardPlaygroundState>>();
        var msgBrokerRpc = _serviceProvider.GetRequiredService<IBrokerRpcClient>();
        var mapper = _serviceProvider.GetRequiredService<IStandardPlaygroundMapper>();
        var rawDataRepo = _serviceProvider.GetRequiredService<IFileDataManager<RawDataLog>>();
        var turnPerfRepo = _serviceProvider.GetRequiredService<IFileDataManager<TurnExecutionPerformance>>();
        var sbxPerfRepo = _serviceProvider.GetRequiredService<IFileDataManager<SandboxExecutionPerformance>>();
        var testPreconditionData = _serviceProvider.GetRequiredService<ITestPreconditionData>();
        var sandboxConfig = _serviceProvider.GetRequiredService<IOptions<SandBoxConfiguration>>();

        // 4. Create PhysicalCores executor + Sb3Actions pairs
        int nEnvs = Math.Max(1, training.PhysicalCores);
        var executorTasks = new List<Task>();

        for (int i = 0; i < nEnvs; i++)
        {
            // Each executor requires a scoped IPlaygroundCommandsHandleService
            var scope = _serviceProvider.CreateScope();
            var playgroundCommands = scope.ServiceProvider
                .GetRequiredService<AiSandBox.ApplicationServices.Commands.Playground.IPlaygroundCommandsHandleService>();

            // Create a dedicated Sb3Actions for this gym
            var sb3 = _algorithmTypeProvider.Create(algorithmType, messageBroker, agentStateRepo);

            // Create StandardExecutor with Sb3Actions injected as IAiActions
            var executor = new StandardExecutor(
                playgroundCommands,
                playgroundRepo,
                sb3,
                sandboxConfig,
                statisticsRepo,
                statFileRepo,
                playgroundStateFileRepo,
                agentStateRepo,
                messageBroker,
                msgBrokerRpc,
                mapper,
                rawDataRepo,
                turnPerfRepo,
                sbxPerfRepo,
                testPreconditionData);

            // Set the episode callback so Sb3Actions can restart episodes
            sb3.SetEpisodeCallback(() => executor.RunAsync());

            // Initialize the Sb3Actions subscriptions
            sb3.Initialize();

            // Keep looping episodes until cancellation
            var execTask = Task.Run(async () =>
            {
                // The first episode is started by Python calling Reset(gymId).
                // Subsequent episodes are also started via the episode callback.
                // TrainingRunner waits until cancellation is requested.
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }, cancellationToken);

            executorTasks.Add(execTask);
        }

        // 5. Start training on the Python side
        Console.WriteLine($"[Training] Starting {algorithmType} training with {nEnvs} gym(s)...");
        Console.WriteLine($"[Training] Experiment: {training.BuildExperimentId()}");
        await training.Run(_policyTrainerClient);

        // 6. Wait until cancellation (training is driven by Python gym calls)
        try
        {
            await Task.WhenAll(executorTasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[Training] Training cancelled.");
        }
    }
}
