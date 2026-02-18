using AiSandBox.SharedBaseTypes.MessageTypes;

namespace AiSandBox.Common.MessageBroker.Contracts.CoreServicesContract.Events;

public record TurnExecutedEvent(Guid Id, Guid PlaygroundId, int TurnNumber) : Event(Id);