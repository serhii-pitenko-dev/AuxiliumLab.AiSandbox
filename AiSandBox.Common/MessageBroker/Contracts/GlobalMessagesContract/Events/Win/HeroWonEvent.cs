using AiSandBox.SharedBaseTypes.MessageTypes;

namespace AiSandBox.Common.MessageBroker.Contracts.GlobalMessagesContract.Events.Win;

public record class HeroWonEvent(Guid Id, Guid PlaygroundId, WinReason WinReason): Event(Id);
