using System;
using System.Collections.Generic;

namespace GodotPackageManager.Core.Models;

/// <summary>
/// This class contains the raw data of a package metadata.
/// </summary>
public sealed class PackageMetadataResult
{
    public bool Ok { get; set; }
    public string Error { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Registry { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public Dictionary<string, object> Data { get; set; } = new();


    public PackageVersionResult ToPackageVersionResult(PackageSummary package = null)
    {        
        try
        {
            if (!Ok)
                return new PackageVersionResult { Ok = false, Error = Error };

            if (Data == null || Data.Count == 0)
                return new PackageVersionResult { Ok = false, Error = "Invalid registry payload" };

            var packageNameValue = Data.TryGetValue("name", out var nameObj) ? nameObj?.ToString() ?? Name : Name;
            var latestVersionValue = Data.TryGetValue("dist-tags", out var distTagsObj) && distTagsObj is Dictionary<string, object> distTags && distTags.TryGetValue("latest", out var latestObj)
                ? latestObj?.ToString() ?? ""
                : "";

            var packageSummary = package ?? new PackageSummary
            {
                Name = packageNameValue,
                LatestVersion = latestVersionValue,
                Registry = Registry
            };

            Dictionary<string, object> versions = Data.TryGetValue("versions", out var versionsObj) && versionsObj is Dictionary<string, object> versionMap
                ? versionMap
                : new Dictionary<string, object>();

            var mappedVersions = new Godot.Collections.Dictionary<string, PackageVersionEntry>();
            foreach (var versionEntry in versions)
            {
                
                var value = versionEntry.Value as Dictionary<string, object> ?? new Dictionary<string, object>();
                
                var tarball = string.Empty;
                Dictionary<string, object> dist = value.TryGetValue("dist", out var distObj) && distObj is Dictionary<string, object> distMap
                    ? distMap
                    : new Dictionary<string, object>();
                if (dist.TryGetValue("tarball", out var tarballObj) && tarballObj is string taballString)
                    tarball = taballString;
                var dependencies = new Godot.Collections.Dictionary<string, string>();
                if (value.TryGetValue("dependencies", out var dependenciesObj) && dependenciesObj is Dictionary<string, object> dependenciesMap)
                {
                    foreach (var dependency in dependenciesMap)
                        dependencies[dependency.Key] = dependency.Value?.ToString() ?? string.Empty;
                }
                var packageVersionEntry = new PackageVersionEntry
                {
                    Version = versionEntry.Key,
                    Package = packageSummary,
                    TarballUrl = tarball,
                    Dependencies = dependencies
                };
                mappedVersions[versionEntry.Key] = packageVersionEntry;
                if (versionEntry.Key == latestVersionValue)
                {
                    var packageDescriptionValue = value.TryGetValue("description", out var descObj) ? descObj?.ToString() ?? "" : "";
                    var packageDisplayNameValue = value.TryGetValue("displayName", out var dispObj) ? dispObj?.ToString() ?? "" : "";
                    packageSummary.Description = packageDescriptionValue;
                    packageSummary.DisplayName = packageDisplayNameValue;
                    packageSummary.LatestVersionData = packageVersionEntry;
                }
            }

            
            return new PackageVersionResult
            {
                Ok = true,
                Package = packageSummary,
                Versions = mappedVersions
            };
        }
        catch (Exception ex)
        {
            return new PackageVersionResult { Ok = false, Error = ex.Message };
        }
    }
}