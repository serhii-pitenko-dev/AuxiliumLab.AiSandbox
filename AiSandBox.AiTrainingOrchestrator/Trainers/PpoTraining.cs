using AiSandBox.Ai.Configuration;
using AiSandBox.AiTrainingOrchestrator.Configuration;
using AiSandBox.AiTrainingOrchestrator.GrpcClients;
using AiSandBox.AiTrainingOrchestrator.PolicyTrainer;

namespace AiSandBox.AiTrainingOrchestrator.Trainers;

public class PpoTraining : BaseTraining, ITraining
{
    private readonly TrainingAlgorithmSettings _settings;

    public override ModelType AlgorithmType => ModelType.PPO;

    public PpoTraining(bool isSameMachine, TrainingAlgorithmSettings settings)
        : base(isSameMachine)
    {
        _settings = settings;
    }

    public string BuildExperimentId() => BuildExperimentId(_settings);

    public TrainingRequest BuildTrainingRequest(TrainingAlgorithmSettings settings, int nEnvs)
    {
        string experimentId = BuildExperimentId(settings);
        var request = new TrainingRequest
        {
            ExperimentId = experimentId,
            ModelOutputPath = GetModelSavePath(experimentId)
        };
        request.Hyperparameters.Add("n_envs", nEnvs.ToString());
        foreach (var p in settings.Parameters)
        {
            if (p.Name == "total_timesteps")
                request.TotalTimesteps = int.TryParse(p.Value, out int ts) ? ts : 5000;
            else if (p.Name == "seed")
                request.Seed = int.TryParse(p.Value, out int s) ? s : 42;
            else
                request.Hyperparameters.TryAdd(p.Name, p.Value);
        }
        return request;
    }

    public async Task Run(IPolicyTrainerClient policyTrainerClient)
    {
        int nEnvs = Math.Max(1, PhysicalCores);
        var request = BuildTrainingRequest(_settings, nEnvs);
        CancellationToken cancellationToken = new CancellationTokenSource(TimeSpan.FromHours(2)).Token;
        await policyTrainerClient.StartTrainingPPOAsync(request, cancellationToken);
    }
}
