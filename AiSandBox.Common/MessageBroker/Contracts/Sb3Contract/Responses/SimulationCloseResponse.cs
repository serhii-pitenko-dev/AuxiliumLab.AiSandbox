using AiSandBox.SharedBaseTypes.MessageTypes;

namespace AiSandBox.Common.MessageBroker.Contracts.Sb3Contract.Responses;

public record SimulationCloseResponse(
    Guid Id,
    Guid GymId,
    Guid CorrelationId,
    bool Success) : Response(Id, CorrelationId);
