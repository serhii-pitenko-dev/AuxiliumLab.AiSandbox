using AuxiliumLab.AiSandbox.ApplicationServices.Queries.Maps.GetAffectedCells;
using AuxiliumLab.AiSandbox.ApplicationServices.Queries.Maps.GetMapLayout;

namespace AuxiliumLab.AiSandbox.ApplicationServices.Queries.Maps;

public class MapQueriesHandleService(
    IMapLayout mapLayoutQuery,
    IAffectedCells affectedCellsQuery) : IMapQueriesHandleService
{
    public required IMapLayout MapLayoutQuery { get; set; } = mapLayoutQuery;
    public required IAffectedCells AffectedCellsQuery { get; set; } = affectedCellsQuery;
}