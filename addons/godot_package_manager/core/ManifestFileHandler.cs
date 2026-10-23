
namespace GodotPackageManager.Core;

public sealed class ManifestFileHandler : JsonFileHandler<ManifestData>
{

    public ManifestFileHandler(string filePath): base (filePath)
    {        
    }
}