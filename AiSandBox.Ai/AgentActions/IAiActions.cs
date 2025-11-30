using AiSandBox.Domain.Agents.Entities;
using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.Ai.AgentActions;

public interface IAiActions
{
    event Action<Guid>? LostGame;

    event Action<Guid>? WinGame;

    List<Coordinates> Action(Agent agent, Guid playgroundId);

    void Move(Agent agent, Guid playgroundId);

    void UseAbilities(Agent agent, EAbility[] abilities);
}

