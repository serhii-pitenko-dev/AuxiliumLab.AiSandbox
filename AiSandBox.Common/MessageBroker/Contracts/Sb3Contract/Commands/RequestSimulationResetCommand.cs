using AiSandBox.SharedBaseTypes.MessageTypes;

namespace AiSandBox.Common.MessageBroker.Contracts.Sb3Contract.Commands;

public record RequestSimulationResetCommand(Guid Id, Guid GymId, int? Seed) : Command(Id);
