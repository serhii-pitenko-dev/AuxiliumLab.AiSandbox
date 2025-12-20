using AiSandBox.SharedBaseTypes.ValueObjects;


namespace AiSandBox.SharedBaseTypes.GlobalEvents.Actions.Agent;

public record class AgentMoveActionEvent(
    Guid AgentId,
    EObjectType Type,
    Coordinates From, 
    Coordinates To,
    bool IsSuccess) : BaseAgentActionEvent(AgentId, EAction.Run, IsSuccess);

