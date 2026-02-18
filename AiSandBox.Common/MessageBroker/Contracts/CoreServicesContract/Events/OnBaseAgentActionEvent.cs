using AiSandBox.SharedBaseTypes.MessageTypes;
using AiSandBox.SharedBaseTypes.ValueObjects;

namespace AiSandBox.Common.MessageBroker.Contracts.CoreServicesContract.Events;

public record OnBaseAgentActionEvent(
    Guid Id, 
    Guid PlaygroundId, 
    Guid AgentId, 
    AgentSnapshot AgentSnapshot) : Event(Id);