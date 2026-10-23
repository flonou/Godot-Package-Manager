

namespace GodotPackageManager.Core;

public sealed class LockFileHandler: JsonFileHandler<LockfileData>
{

    public LockFileHandler(string filePath): base (filePath)
    {        
    }
}