using AuxiliumLab.AiSandbox.Ai.Configuration;
using AuxiliumLab.AiSandbox.AiTrainingOrchestrator.Configuration;
using AuxiliumLab.AiSandbox.AiTrainingOrchestrator.GrpcClients;
using AuxiliumLab.AiSandbox.AiTrainingOrchestrator.PolicyTrainer;

namespace AuxiliumLab.AiSandbox.AiTrainingOrchestrator.Trainers;

public interface ITraining
{
    int PhysicalCores { get; }
    ModelType AlgorithmType { get; }
    Task Run(IPolicyTrainerClient policyTrainerClient);
    string BuildExperimentId();
    TrainingRequest BuildTrainingRequest(TrainingAlgorithmSettings settings, int nEnvs);
}