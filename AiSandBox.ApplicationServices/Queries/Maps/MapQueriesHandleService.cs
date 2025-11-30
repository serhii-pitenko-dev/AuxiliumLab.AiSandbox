using AiSandBox.ApplicationServices.Queries.Maps.GetMapInitialPeconditions;
using AiSandBox.ApplicationServices.Queries.Maps.GetMapLayout;

namespace AiSandBox.ApplicationServices.Queries.Maps;

public class MapQueriesHandleService(IMapLayout mapLayoutQuery, IInitialPreconditions mapInitialPreconditionsQuery) : IMapQueriesHandleService
{
    public required IMapLayout MapLayoutQuery { get; set; } = mapLayoutQuery;
    public required IInitialPreconditions MapInitialPreconditionsQuery { get; set; } = mapInitialPreconditionsQuery;
}
