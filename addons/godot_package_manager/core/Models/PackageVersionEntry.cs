
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Godot;

namespace GodotPackageManager.Core.Models;

[GlobalClass]
public sealed partial class PackageVersionEntry : RefCounted
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
    public string GodotVersion { get; set; } = ">=4.0";
    

    [JsonPropertyName("name")]
    public string Name
    {
        get
        {
            return Package?.Name;
        }
        set
        {
            Package.Name = value;
        }
    } 
    
    public PackageSummary Package { get; set; } = new PackageSummary();
    [JsonPropertyName("resolved")]
    public string TarballUrl { get; set; } = string.Empty;

    [JsonPropertyName("dependencies")]
    public Godot.Collections.Dictionary<string, string> Dependencies  { get; set; } = new();

    public static PackageVersionEntry FromLockfileDictionary(string packageName, Dictionary<string, object> packageVersionMap)
    {
        if (string.IsNullOrWhiteSpace(packageName) || packageVersionMap == null)
            return null;

        var version = packageVersionMap.TryGetValue("version", out var versionValue)
            ? versionValue?.ToString() ?? string.Empty
            : string.Empty;

        if (string.IsNullOrWhiteSpace(version))
            return null;

        var source = packageVersionMap.TryGetValue("source", out var sourceValue) && sourceValue is Dictionary<string, object> sourceMap
            ? sourceMap
            : new Dictionary<string, object>();

        var registry = source.TryGetValue("registry", out var registryValue)
            ? registryValue?.ToString() ?? string.Empty
            : string.Empty;

        var tarballUrl = source.TryGetValue("tarball", out var tarballValue)
            ? tarballValue?.ToString() ?? string.Empty
            : source.TryGetValue("url", out var urlValue)
                ? urlValue?.ToString() ?? string.Empty
                : string.Empty;

        var dependencies = new Godot.Collections.Dictionary<string, string>();
        if (packageVersionMap.TryGetValue("dependencies", out var dependenciesValue) && dependenciesValue is Dictionary<string, object> dependencyMap)
        {
            foreach (var dependency in dependencyMap)
            {
                dependencies[dependency.Key] = dependency.Value?.ToString() ?? string.Empty;
            }
        }

        var summary = new PackageSummary
        {
            Name = packageName,
            DisplayName = packageName,
            LatestVersion = version,
            Registry = registry,
            Scope = string.Empty,
            Description = string.Empty
        };

        return new PackageVersionEntry
        {
            Version = version,
            Package = summary,
            TarballUrl = tarballUrl,
            Dependencies = dependencies
        };
    }
}
