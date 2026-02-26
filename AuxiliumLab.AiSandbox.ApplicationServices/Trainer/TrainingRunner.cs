using AuxiliumLab.AiSandbox.Ai;
using AuxiliumLab.AiSandbox.Ai.Configuration;
using AuxiliumLab.AiSandbox.AiTrainingOrchestrator.Configuration;
using AuxiliumLab.AiSandbox.AiTrainingOrchestrator.GrpcClients;
using AuxiliumLab.AiSandbox.AiTrainingOrchestrator.Trainers;
using AuxiliumLab.AiSandbox.ApplicationServices.Executors;
using AuxiliumLab.AiSandbox.ApplicationServices.Runner.LogsDto;
using AuxiliumLab.AiSandbox.ApplicationServices.Runner.LogsDto.Performance;
using AuxiliumLab.AiSandbox.ApplicationServices.Runner.TestPreconditionSet;
using AuxiliumLab.AiSandbox.ApplicationServices.Saver.Persistence.Sandbox.Mappers;
using AuxiliumLab.AiSandbox.ApplicationServices.Saver.Persistence.Sandbox.States;
using AuxiliumLab.AiSandbox.Common.MessageBroker;
using AuxiliumLab.AiSandbox.Domain.Playgrounds;
using AuxiliumLab.AiSandbox.Infrastructure.Configuration.Preconditions;
using AuxiliumLab.AiSandbox.Infrastructure.FileManager;
using AuxiliumLab.AiSandbox.Infrastructure.MemoryManager;
using AuxiliumLab.AiSandbox.SharedBaseTypes.AiContract.Dto;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AuxiliumLab.AiSandbox.ApplicationServices.Trainer;

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

        // 3. Resolve shared singleton dependencies (must not contain per-gym mutable state)
        var playgroundRepo = _serviceProvider.GetRequiredService<IMemoryDataManager<StandardPlayground>>();
        var playgroundStateFileRepo = _serviceProvider.GetRequiredService<IFileDataManager<StandardPlaygroundState>>();
        var mapper = _serviceProvider.GetRequiredService<IStandardPlaygroundMapper>();
        var rawDataRepo = _serviceProvider.GetRequiredService<IFileDataManager<RawDataLog>>();
        var turnPerfRepo = _serviceProvider.GetRequiredService<IFileDataManager<TurnExecutionPerformance>>();
        var sbxPerfRepo = _serviceProvider.GetRequiredService<IFileDataManager<SandboxExecutionPerformance>>();
        var testPreconditionData = _serviceProvider.GetRequiredService<ITestPreconditionData>();
        var sandboxConfig = _serviceProvider.GetRequiredService<IOptions<SandBoxConfiguration>>();

        // 4. Create one executor + Sb3Actions pair per physical core.
        int nEnvs = Math.Max(1, training.PhysicalCores);
        var executorTasks = new List<Task>();
        var gymIds = new List<Guid>();

        for (int i = 0; i < nEnvs; i++)
        {
            // Each executor requires a scoped IPlaygroundCommandsHandleService
            var scope = _serviceProvider.CreateScope();
            var playgroundCommands = scope.ServiceProvider
                .GetRequiredService<Commands.Playground.IPlaygroundCommandsHandleService>();

            // Each gym gets its own isolated broker + rpc client so that:
            //   1. Events from one gym's executor cannot leak to another gym's Sb3Actions.
            //   2. There is no lock contention between gyms on a shared broker.
            var gymBroker = new MessageBroker();
            var gymRpcClient = new BrokerRpcClient(gymBroker);
            var gymAgentStateRepo = new MemoryDataManager<AgentStateForAIDecision>();

            // Create a dedicated Sb3Actions for this gym
            var rewards = _trainingSettings.Rewards;
            var sb3 = _algorithmTypeProvider.Create(
                algorithmType, gymBroker, gymAgentStateRepo,
                rewards.StepPenalty, rewards.WinReward, rewards.LossReward);

            // Track the gym's unique ID so it can be passed to Python
            gymIds.Add(sb3.GymId);

            // Each episode gets a fresh executor so no per-episode state
            // (sandboxStatus, _playground, _agentsToAct, etc.) leaks between runs.
            // Sb3Actions stays alive for the full training session since it owns
            // the Python gRPC channel.
            StandardExecutor CreateEpisodeExecutor() => new StandardExecutor(
                playgroundCommands,
                playgroundRepo,
                sb3,
                sandboxConfig,
                playgroundStateFileRepo,
                gymAgentStateRepo,
                gymBroker,
                gymRpcClient,
                mapper,
                rawDataRepo,
                turnPerfRepo,
                sbxPerfRepo,
                testPreconditionData);

            // Set the episode callback so Sb3Actions can restart episodes
            sb3.SetEpisodeCallback(() => CreateEpisodeExecutor().RunAsync());

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
        Console.WriteLine($"[Training] Gym IDs: {string.Join(", ", gymIds.Select(g => g.ToString("N")[..8]))}");
        await training.Run(_policyTrainerClient, gymIds);

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
