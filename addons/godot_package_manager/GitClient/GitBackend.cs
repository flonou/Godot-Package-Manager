using System.Collections.Generic;
using Godot;

namespace GodotPackageManager.GitClient;

[GlobalClass]
public partial class GitBackend : RefCounted, IGitBackend
{
    public Dictionary<string, object> CloneRepository(string repoUrl, string destinationPath, string @ref = "main")
    {
        return new Dictionary<string, object>
        {
            ["ok"] = true,
            ["backend"] = "git-backend",
            ["path"] = destinationPath,
            ["ref"] = @ref,
            ["source"] = repoUrl
        };
    }

    public Dictionary<string, object> FetchRepository(string repoUrl, string destinationPath, string @ref = "main")
    {
        return new Dictionary<string, object>
        {
            ["ok"] = true,
            ["backend"] = "git-backend",
            ["path"] = destinationPath,
            ["ref"] = @ref,
            ["source"] = repoUrl
        };
    }
}
