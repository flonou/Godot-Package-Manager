using System.Collections.Generic;
using Godot;

namespace GodotPackageManager.GitClient;

[GlobalClass]
public partial class LocalGitBackend : RefCounted, IGitBackend
{
    public Dictionary<string, object> CloneRepository(string repoUrl, string destinationPath, string @ref = "main")
    {
        var args = new List<string> { "clone", "--depth", "1" };
        if (!string.IsNullOrWhiteSpace(@ref))
        {
            args.Add("--branch");
            args.Add(@ref);
        }

        args.Add(repoUrl);
        args.Add(destinationPath);

        var exitCode = Godot.OS.Execute("git", args.ToArray());
        if (exitCode != 0)
            return new Dictionary<string, object> { ["ok"] = false, ["error"] = "git clone failed", ["exit_code"] = exitCode };

        return new Dictionary<string, object> { ["ok"] = true, ["backend"] = "local-git", ["path"] = destinationPath, ["ref"] = @ref, ["source"] = repoUrl };
    }

    public Dictionary<string, object> FetchRepository(string repoUrl, string destinationPath, string @ref = "main")
    {
        var args = new[] { "-C", destinationPath, "fetch", "--depth", "1", "origin", @ref };
        var exitCode = Godot.OS.Execute("git", args);
        if (exitCode != 0)
            return new Dictionary<string, object> { ["ok"] = false, ["error"] = "git fetch failed", ["exit_code"] = exitCode };

        return new Dictionary<string, object> { ["ok"] = true, ["backend"] = "local-git", ["path"] = destinationPath, ["ref"] = @ref, ["source"] = repoUrl };
    }
}
