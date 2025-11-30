namespace AiSandBox.Infrastructure.Configuration;

public class FileSource
{
   public bool IsEnable { get; set; }

   public Guid MapId { get; set; }

   public string Path { get; set; } = string.Empty;
}

