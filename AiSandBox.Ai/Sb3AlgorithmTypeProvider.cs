using AiSandBox.Ai.Configuration;
using AiSandBox.Common.MessageBroker;
using AiSandBox.Infrastructure.MemoryManager;
using AiSandBox.SharedBaseTypes.AiContract.Dto;

namespace AiSandBox.Ai;

/// <summary>
/// Factory that creates Sb3Actions instances. Each call to Create produces a
/// distinct instance with a unique GymId, suitable for one parallel gym environment.
/// </summary>
public class Sb3AlgorithmTypeProvider
{
    public Sb3Actions Create(
        ModelType modelType,
        IMessageBroker messageBroker,
        IMemoryDataManager<AgentStateForAIDecision> agentStateMemoryRepository)
    {
        var config = new AiConfiguration
        {
            ModelType = modelType,
            Version = "1.0",
            PolicyType = AiPolicy.MLP
        };

        return new Sb3Actions(
            messageBroker,
            agentStateMemoryRepository,
            modelType,
            config,
            Guid.NewGuid());
    }
}



