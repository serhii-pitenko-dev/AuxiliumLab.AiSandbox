using AiSandBox.Ai.Configuration;
using AiSandBox.AiTrainingOrchestrator.Configuration;
using AiSandBox.AiTrainingOrchestrator.GrpcClients;
using AiSandBox.AiTrainingOrchestrator.PolicyTrainer;

namespace AiSandBox.AiTrainingOrchestrator.Trainers;

public interface ITraining
{
    int PhysicalCores { get; }
    ModelType AlgorithmType { get; }
    Task Run(IPolicyTrainerClient policyTrainerClient);
    string BuildExperimentId();
    TrainingRequest BuildTrainingRequest(TrainingAlgorithmSettings settings, int nEnvs);
}