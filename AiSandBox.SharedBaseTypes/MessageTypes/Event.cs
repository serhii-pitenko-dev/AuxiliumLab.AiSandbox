namespace AiSandBox.SharedBaseTypes.MessageTypes;

public record Event(Guid Id) : Message(Id);