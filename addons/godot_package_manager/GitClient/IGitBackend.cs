using System.Collections.Generic;

namespace GodotPackageManager.GitClient;

public interface IGitBackend
{
    Dictionary<string, object> CloneRepository(string repoUrl, string destinationPath, string @ref = "main");
    Dictionary<string, object> FetchRepository(string repoUrl, string destinationPath, string @ref = "main");
}
