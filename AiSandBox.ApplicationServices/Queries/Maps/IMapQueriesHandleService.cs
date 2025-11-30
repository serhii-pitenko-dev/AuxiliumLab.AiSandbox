using AiSandBox.ApplicationServices.Queries.Maps.GetMapInitialPeconditions;
using AiSandBox.ApplicationServices.Queries.Maps.GetMapLayout;

namespace AiSandBox.ApplicationServices.Queries.Maps;

public interface IMapQueriesHandleService
{
    public IMapLayout MapLayoutQuery { get; set; }

    public IInitialPreconditions MapInitialPreconditionsQuery { get; set; }
}