namespace AuxiliumLab.AiSandbox.ApplicationServices.Queries.Maps.GetMapLayout;

public interface IMapLayout
{
    MapLayoutResponse GetFromMemory(Guid guid);

    Task<MapLayoutResponse> GetFromFile(Guid guid);
}
