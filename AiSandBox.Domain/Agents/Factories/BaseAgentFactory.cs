using AiSandBox.Domain.Agents.Entities;
using AiSandBox.Domain.Validation.Agents;

namespace AiSandBox.Domain.Agents.Factories;

public class BaseAgentFactory
{
    public BaseAgentFactory(InitialAgentCharacters characters)
    {
        InitialAgentCharactersValidator.Validate(characters);
    }
}

