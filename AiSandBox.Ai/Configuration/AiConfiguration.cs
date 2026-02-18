using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AiSandBox.Ai.Configuration;

public class AiConfiguration
{
    public string Version { get; init; } = string.Empty;
    public ModelType ModelType { get; init; }
    public AiPolicy PolicyType { get; init; }
}

