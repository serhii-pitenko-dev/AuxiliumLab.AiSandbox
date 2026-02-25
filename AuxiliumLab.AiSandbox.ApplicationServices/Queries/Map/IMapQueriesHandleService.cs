using AuxiliumLab.AiSandbox.ApplicationServices.Queries.Maps.GetAffectedCells;
using AuxiliumLab.AiSandbox.ApplicationServices.Queries.Maps.GetMapLayout;

namespace AuxiliumLab.AiSandbox.ApplicationServices.Queries.Maps;

public interface IMapQueriesHandleService
{
    public IMapLayout MapLayoutQuery { get; set; }

    public IAffectedCells AffectedCellsQuery { get; set; }
}