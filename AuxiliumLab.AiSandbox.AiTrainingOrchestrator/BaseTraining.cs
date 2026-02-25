using AuxiliumLab.AiSandbox.Ai.Configuration;
using AuxiliumLab.AiSandbox.AiTrainingOrchestrator.Configuration;
using AuxiliumLab.AiSandbox.Common.Helpers;

namespace AuxiliumLab.AiSandbox.AiTrainingOrchestrator;

public abstract class BaseTraining
{
    public int PhysicalCores { get; private set; }

    public abstract ModelType AlgorithmType { get; }

    protected BaseTraining(bool isSameMachine)
    {
        if (isSameMachine)
        {
            PhysicalCores = SystemInfo.GetPhysicalCoreCount();
        }
        else
        {
            throw new NotImplementedException("Remote training not implemented yet. Core count detection is only supported for local training.");
        }
    }

    public string BuildExperimentId(TrainingAlgorithmSettings settings)
    {
        string paramPart = string.Join("_", settings.Parameters.Select(p => p.Value));
        string datePart = DateTime.Now.ToString("yyyyMMdd");
        return $"{AlgorithmType.ToString().ToLower()}_{paramPart}_{datePart}";
    }

    public string GetModelSavePath(string experimentId)
        => Path.Combine("E:/FILE_STORAGE/TRAINED_ALGORITHMS", AlgorithmType.ToString(), experimentId);
}
