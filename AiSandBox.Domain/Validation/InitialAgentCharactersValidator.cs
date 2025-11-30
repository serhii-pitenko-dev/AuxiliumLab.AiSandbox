using AiSandBox.Domain.Agents.Entities;

namespace AiSandBox.Domain.Validation;

internal static class InitialAgentCharactersValidator
{
    public static void Validate(InitialAgentCharacters characters)
    {
        if (characters.Speed <= 0)
            throw new ArgumentException("Speed must be positive", nameof(characters.Speed));
        if (characters.SightRange < 0)
            throw new ArgumentException("SightRange cannot be negative", nameof(characters.SightRange));
        if (characters.Stamina <= 0)
            throw new ArgumentException("Stamina must be positive", nameof(characters.Stamina));
    }
}