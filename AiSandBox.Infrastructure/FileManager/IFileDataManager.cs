using System.Threading.Tasks;

namespace AiSandBox.Infrastructure.FileManager;

public interface IFileDataManager<T>
{
    Task SaveOrAppendAsync(Guid id, T obj);
    Task<T> LoadObjectAsync(Guid id);
    bool DeleteObject(Guid id);
    IEnumerable<Guid> GetAvailableVersions();
    void Clear();
}