using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AiSandBox.Statistics.StatisticDataManager;

public interface IStatisticFileDataManager
{
    public Task AppendDataToFileAsync(string fileName, object data);

}

