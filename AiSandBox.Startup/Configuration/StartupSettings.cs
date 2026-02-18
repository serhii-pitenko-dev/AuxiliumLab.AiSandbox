using AiSandBox.Ai.Configuration;
using AiSandBox.SharedBaseTypes.ValueObjects.StartupSettings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AiSandBox.Startup.Configuration;

public class StartupSettings
{
    public bool IsPreconditionStart { get; set; }
    public PresentationMode PresentationMode { get; set; }
    public ExecutionMode ExecutionMode { get; set; }
    public bool TestPreconditionsEnabled { get; set; }
    public bool IsWebApiEnabled { get; set; }
    public AiPolicy PolicyType { get; set; }
    public int SimulationCount { get; set; } = 1;
}

