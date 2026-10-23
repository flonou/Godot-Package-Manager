using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

using GodotPackageManager.Core;
using GodotPackageManager.Core.Models;

using NetHttpClient = System.Net.Http.HttpClient;

namespace GodotPackageManager.NpmRegistryClient;

[GlobalClass]
public partial class HttpPackageRegistryClient : RefCounted, IPackageRegistryClient
{
    private static readonly NetHttpClient Client = new();

    public PackageSearchResult QueryPackages(NpmRegistryEntry registryEntry, int limit = 100)
    {
        if (string.IsNullOrWhiteSpace(registryEntry.Url))
            return new PackageSearchResult { Ok = false, Error = "Registry URL is required" };

        try
        {
            var normalizedUrl = registryEntry.Url.TrimEnd('/');
            var packages = new Godot.Collections.Array<PackageSummary>();
            foreach (var scopeTerm in registryEntry.Scopes)
            {
                var requestUrl = $"{normalizedUrl}/-/v1/search?text={Uri.EscapeDataString(scopeTerm)}&size={Math.Max(1, limit)}";
                
                var response = Client.GetAsync(requestUrl).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                    return new PackageSearchResult { Ok = false, Error = $"HTTP {response.StatusCode}", Registry = registryEntry, Search = scopeTerm };

                var payload = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                
                if (string.IsNullOrWhiteSpace(payload))
                    return new PackageSearchResult { Ok = false, Error = "Empty registry search response", Registry = registryEntry, Search = scopeTerm };

                using var document = JsonDocument.Parse(payload);
                if (document.RootElement.TryGetProperty("objects", out var objectsElement) && objectsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in objectsElement.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Object)
                            continue;

                        var metadata = new Dictionary<string, object>();
                        if (item.TryGetProperty("package", out var packageElement) && packageElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var property in packageElement.EnumerateObject())
                            {
                                metadata[property.Name] = JsonElementToObject(property.Value);
                            }
                        }
                        var packageName = metadata.TryGetValue("name", out var packageNameObj) ? packageNameObj?.ToString() ?? "" : "";
                        var lastestVersion = metadata.TryGetValue("version", out var versionObj) ? versionObj?.ToString() ?? "" : "";
                        var description = metadata.TryGetValue("description", out var descriptionObj) ? descriptionObj?.ToString() ?? "" : "";

                        var packageSummary = PackageManagerCore.GetInstance().GetOrCreatePackageFromCache(packageName);
                        packageSummary.Name = packageName;
                        packageSummary.LatestVersion = lastestVersion;
                        packageSummary.Description = description;
                        packageSummary.Scope = metadata.TryGetValue("scope", out var scopeObj) ? scopeObj?.ToString() ?? "" : "";
                        packageSummary.Registry = normalizedUrl;

                        var versionResult = FetchPackageMetadata(packageSummary).ToPackageVersionResult(packageSummary);
                        if (versionResult.Ok && !string.IsNullOrWhiteSpace(packageSummary.LatestVersion) && versionResult.Versions != null)
                        {
                            if (versionResult.Versions.TryGetValue(packageSummary.LatestVersion, out var latestVersionEntry))
                            {
                                packageSummary.LatestVersionData = latestVersionEntry;
                            }
                            else if (versionResult.Versions.Count > 0)
                            {
                                packageSummary.LatestVersionData = versionResult.Versions.Values.FirstOrDefault();
                            }
                        }

                        packages.Add(packageSummary);
                    }
                }
            }

            return new PackageSearchResult
            {
                Ok = true,
                Registry = registryEntry,
                Count = packages.Count,
                Packages = packages
            };
        }
        catch (Exception ex)
        {
            GD.PrintErr(ex);
            return new PackageSearchResult { Ok = false, Error = ex.Message };
        }
    }


    public PackageMetadataResult FetchPackageMetadata(PackageSummary package)
    {
        if (package == null)
            return new PackageMetadataResult { Ok = false, Error = "Invalid package summary" };

        var packageName = package.Name;

        try
        {
            var normalizedUrl = package.Registry.TrimEnd('/');
            var encodedPackageName = Uri.EscapeDataString(packageName.Trim());
            var url = $"{normalizedUrl}/{encodedPackageName}";

            var response = Client.GetAsync(url).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
                return new PackageMetadataResult { Ok = false, Error = $"HTTP {response.StatusCode}", Name = packageName, Url = url };

            var payload = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            if (string.IsNullOrWhiteSpace(payload))
                return new PackageMetadataResult { Ok = false, Error = "Empty registry response", Name = packageName, Url = url };

            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new PackageMetadataResult { Ok = false, Error = "Invalid JSON from registry", Name = packageName, Url = url };

            var data = new Dictionary<string, object>();
            foreach (var property in root.EnumerateObject())
                data[property.Name] = JsonElementToObject(property.Value);

            return new PackageMetadataResult { Ok = true, Url = url, Name = packageName, Registry = normalizedUrl, Data = data };
        }
        catch (Exception ex)
        {
            return new PackageMetadataResult { Ok = false, Error = ex.Message, Name = packageName };
        }

    }


    private static List<string> SplitScopes(string scopesCsv)
    {
        if (string.IsNullOrWhiteSpace(scopesCsv))
            return new List<string>();

        return scopesCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim())
            .ToList();
    }

    private static object JsonElementToObject(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                if (element.TryGetInt64(out var int64Value))
                    return int64Value;
                return element.GetDouble();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
                return null;
            case JsonValueKind.Array:
                var list = new List<object>();
                foreach (var item in element.EnumerateArray())
                    list.Add(JsonElementToObject(item));
                return list;
            case JsonValueKind.Object:
                var map = new Dictionary<string, object>();
                foreach (var property in element.EnumerateObject())
                    map[property.Name] = JsonElementToObject(property.Value);
                return map;
            default:
                return element.ToString();
        }
    }
}
