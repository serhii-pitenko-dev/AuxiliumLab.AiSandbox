namespace AiSandBox.ApplicationServices.Queries.Maps.GetMapInitialPeconditions;

public interface IInitialPreconditions
{
    public PreconditionsResponse Get(Guid guid);
}