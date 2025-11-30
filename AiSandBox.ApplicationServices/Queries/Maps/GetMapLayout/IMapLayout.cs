namespace AiSandBox.ApplicationServices.Queries.Maps.GetMapLayout;

public interface IMapLayout
{
    public MapLayoutResponse GetFromMemory(Guid guid);

    public MapLayoutResponse GetFromFile(Guid guid);
}
