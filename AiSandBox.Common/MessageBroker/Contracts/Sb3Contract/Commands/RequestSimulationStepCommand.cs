using AiSandBox.SharedBaseTypes.MessageTypes;

namespace AiSandBox.Common.MessageBroker.Contracts.Sb3Contract.Commands;

public record RequestSimulationStepCommand(Guid Id, Guid GymId, int Action) : Command(Id);
