using AiSandBox.SharedBaseTypes.MessageTypes;

namespace AiSandBox.Common.MessageBroker.Contracts.Sb3Contract.Commands;

public record RequestSimulationCloseCommand(Guid Id, Guid GymId) : Command(Id);
