namespace AiSandBox.Infrastructure.FileManager;

public interface IFileDataManager<T>
{
    void AddOrUpdate(Guid id, T obj);
    T LoadObject(Guid id);
    bool DeleteObject(Guid id);
    IEnumerable<Guid> GetAvailableVersions();
    void Clear();
}