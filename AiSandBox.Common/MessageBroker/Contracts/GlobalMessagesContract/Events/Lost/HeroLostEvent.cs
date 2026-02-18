using AiSandBox.SharedBaseTypes.MessageTypes;

namespace AiSandBox.Common.MessageBroker.Contracts.GlobalMessagesContract.Events.Lost;

public record HeroLostEvent(Guid Id, Guid PlaygroundId, LostReason LostReason): Event(Id);