using AuxiliumLab.AiSandbox.Ai.Configuration;
using AuxiliumLab.AiSandbox.SharedBaseTypes.ValueObjects.StartupSettings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuxiliumLab.AiSandbox.Startup.Configuration;

public class StartupSettings
{
    public bool IsPreconditionStart { get; set; }
    public PresentationMode PresentationMode { get; set; }
    public ExecutionMode ExecutionMode { get; set; }

    /// <summary>
    /// Absolute path to the pre-trained model .zip file used by
    /// SingleTrainedAISimulation and MassTrainedAISimulation modes.
    /// Example: E:\\FILE_STORAGE\\TRAINED_ALGORITHMS\\PPO\\ppo_100000_2048_64_0.0003_20260227.zip
    /// </summary>
    public string TrainedModelPath { get; set; } = string.Empty;
    public bool TestPreconditionsEnabled { get; set; }
    public bool IsWebApiEnabled { get; set; }
    public AiPolicy PolicyType { get; set; }
    public int StandardSimulationCount { get; set; } = 1;
    public IncrementalPropertiesSettings IncrementalProperties { get; set; } = new();
}

