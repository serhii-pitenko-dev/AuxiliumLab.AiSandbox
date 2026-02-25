namespace AuxiliumLab.AiSandbox.ApplicationServices.Queries.Maps.GetAffectedCells;

public interface IAffectedCells
{
    AffectedCellsResponse GetFromMemory(Guid playgroundId, Guid objectId);
}