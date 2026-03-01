using AuxiliumLab.AiSandbox.Ai;
using AuxiliumLab.AiSandbox.Ai.Configuration;
using AuxiliumLab.AiSandbox.AiTrainingOrchestrator;
using AuxiliumLab.AiSandbox.AiTrainingOrchestrator.Configuration;
using AuxiliumLab.AiSandbox.AiTrainingOrchestrator.GrpcClients;
using AuxiliumLab.AiSandbox.AiTrainingOrchestrator.PolicyTrainer;
using AuxiliumLab.AiSandbox.AiTrainingOrchestrator.Trainers;
using AuxiliumLab.AiSandbox.ApplicationServices.Executors;
using AuxiliumLab.AiSandbox.ApplicationServices.Runner.LogsDto;
using AuxiliumLab.AiSandbox.ApplicationServices.Runner.LogsDto.Performance;
using AuxiliumLab.AiSandbox.ApplicationServices.Runner.TestPreconditionSet;
using AuxiliumLab.AiSandbox.ApplicationServices.Saver.Persistence.Sandbox.Mappers;
using AuxiliumLab.AiSandbox.ApplicationServices.Saver.Persistence.Sandbox.States;
using AuxiliumLab.AiSandbox.Common.MessageBroker;
using AuxiliumLab.AiSandbox.Common.MessageBroker.Contracts.Sb3Contract.Commands;
using AuxiliumLab.AiSandbox.Domain.Playgrounds;
using AuxiliumLab.AiSandbox.Infrastructure.Configuration.Preconditions;
using AuxiliumLab.AiSandbox.Domain.Statistics.Result;
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
    private readonly GymBrokerRegistry _gymBrokerRegistry;

    public TrainingRunner(
        IServiceProvider serviceProvider,
        TrainingSettings trainingSettings,
        Sb3AlgorithmTypeProvider algorithmTypeProvider,
        IPolicyTrainerClient policyTrainerClient,
        GymBrokerRegistry gymBrokerRegistry)
    {
        _serviceProvider = serviceProvider;
        _trainingSettings = trainingSettings;
        _algorithmTypeProvider = algorithmTypeProvider;
        _policyTrainerClient = policyTrainerClient;
        _gymBrokerRegistry = gymBrokerRegistry;
    }

    public async Task<TrainingRunInfo> RunTrainingAsync(ModelType algorithmType, CancellationToken cancellationToken = default)
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
        var gymCtsList    = new List<CancellationTokenSource>();
        var linkedCtsList = new List<CancellationTokenSource>();

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

            // Register the per-gym broker so SimulationService can route Reset/Step/Close
            // to the correct Sb3Actions instance (instead of the shared singleton broker).
            _gymBrokerRegistry.Register(sb3.GymId, gymBroker);

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

            // Cancel when Python closes this gym's connection (signals training complete). 
            var gymCloseCts = new CancellationTokenSource();
            var linkedCts   = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, gymCloseCts.Token);
            gymCtsList.Add(gymCloseCts);
            linkedCtsList.Add(linkedCts);

            var capturedGymId = sb3.GymId;
            gymBroker.Subscribe<RequestSimulationCloseCommand>(cmd =>
            {
                if (cmd.GymId == capturedGymId)
                    gymCloseCts.Cancel();
            });

            // Keep running until Python closes this gym (training complete) or app is stopped.
            var execTask = Task.Run(async () =>
            {
                // The first episode is started by Python calling Reset(gymId).
                // Subsequent episodes are also started via the episode callback.
                // TrainingRunner exits when Python closes all gyms after training finishes.
                try
                {
                    await Task.Delay(Timeout.Infinite, linkedCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
            }, CancellationToken.None);

            executorTasks.Add(execTask);
        }

        // 5. Negotiate environment contract with Python before starting training.
        //    This replaces the old silent coupling where obs_dim was hard-coded
        //    on both sides. Any mismatch is now a hard error here.
        string experimentId = training.BuildExperimentId();
        var spec = EnvironmentSpecBuilder.Build(sandboxConfig.Value, experimentId);
        var negotiationCt = new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token;
        NegotiateEnvironmentResponse negotiation;
        try
        {
            negotiation = await _policyTrainerClient.NegotiateEnvironmentAsync(
                new NegotiateEnvironmentRequest { ExperimentId = experimentId, Spec = spec },
                negotiationCt);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"[Training] NegotiateEnvironment RPC failed for experiment '{experimentId}': {ex.Message}", ex);
        }

        if (!negotiation.Accepted)
            throw new InvalidOperationException(
                $"[Training] Python RL service rejected environment spec for experiment '{experimentId}': "
                + negotiation.Message);

        EnvironmentSpecBuilder.AssertEchoMatches(spec, negotiation.EchoedSpec, experimentId);

        Console.WriteLine(
            $"[Training] Environment spec negotiated: obs_dim={spec.ObservationDim}, "
            + $"action_dim={spec.ActionDim}, sight_range={spec.SightRange}.");

        // 6. Start training on the Python side
        Console.WriteLine($"[Training] Starting {algorithmType} training with {nEnvs} gym(s)...");
        Console.WriteLine($"[Training] Experiment: {experimentId}");
        Console.WriteLine($"[Training] Gym IDs: {string.Join(", ", gymIds.Select(g => g.ToString("N")[..8]))}");
        await training.Run(_policyTrainerClient, gymIds);

        // 7. Wait until all gyms close (Python training complete) or app is stopped.
        try
        {
            await Task.WhenAll(executorTasks).ConfigureAwait(false);
            Console.WriteLine("[Training] All gyms closed — training complete.");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[Training] Training cancelled by application stop.");
        }
        finally
        {
            // Clean up broker registrations so stale gym IDs don't linger
            foreach (var id in gymIds)
                _gymBrokerRegistry.Unregister(id);

            // Dispose per-gym cancellation tokens
            foreach (var cts in linkedCtsList) cts.Dispose();
            foreach (var cts in gymCtsList)    cts.Dispose();
        }

        // Return training metadata so callers (e.g. AggregationRunner) can include it in reports.
        var parameterDict = algoSettings.Parameters
            .ToDictionary(p => p.Name, p => p.Value);
        return new TrainingRunInfo(algorithmName, experimentId, parameterDict);
    }
}
