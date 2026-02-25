using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AiSandBox.Statistics.StatisticDataManager;

public interface IStatisticFileDataManager
{
    /// <summary>Serialises <paramref name="data"/> as JSON and appends it to the file.</summary>
    public Task AppendDataToFileAsync(string fileName, object data);

    /// <summary>
    /// Appends the pre-formatted <paramref name="csvContent"/> string to the file.
    /// Creates the file (and any parent directories) if it does not yet exist.
    /// </summary>
    public Task ConvertToCsvAndAppendAsync(string fileName, string csvContent);
}

