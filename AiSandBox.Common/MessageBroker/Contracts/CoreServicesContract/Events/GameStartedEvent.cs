using AiSandBox.SharedBaseTypes.MessageTypes;

namespace AiSandBox.Common.MessageBroker.Contracts.CoreServicesContract.Events;

public record GameStartedEvent(Guid Id, Guid PlaygroundId) : Event(Id);

