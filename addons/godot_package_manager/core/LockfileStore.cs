using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;
using GodotPackageManager.Core.Models;

namespace GodotPackageManager.Core;

[GlobalClass]
public partial class LockfileStore : RefCounted
{
    public Dictionary<string, object> Read(string path = PackageManagerCore.LockFilePath)
    {
        var absolutePath = ProjectSettings.GlobalizePath(path);
        if (!File.Exists(absolutePath))
            return new Dictionary<string, object> { ["schemaVersion"] = 1, ["dependencies"] = new Dictionary<string, object>() };

        try
        {
            var payload = File.ReadAllText(absolutePath);
            if (string.IsNullOrWhiteSpace(payload))
                return new Dictionary<string, object> { ["schemaVersion"] = 1, ["dependencies"] = new Dictionary<string, object>() };

            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return new Dictionary<string, object> { ["schemaVersion"] = 1, ["dependencies"] = new Dictionary<string, object>() };

            var result = ConvertJsonObject(document.RootElement);
            if (!result.ContainsKey("dependencies") || result["dependencies"] is not Dictionary<string, object>)
                result["dependencies"] = new Dictionary<string, object>();
            if (!result.ContainsKey("schemaVersion"))
                result["schemaVersion"] = 1;

            return result;
        }
        catch
        {
            return new Dictionary<string, object> { ["schemaVersion"] = 1, ["dependencies"] = new Dictionary<string, object>() };
        }
    }

    public bool Write(Dictionary<string, object> lockData, string path = PackageManagerCore.LockFilePath)
    {
        var absolutePath = ProjectSettings.GlobalizePath(path);
        var directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        try
        {
            var json = JsonSerializer.Serialize(lockData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(absolutePath, json);
            return true;
        }
        catch
        {
            return false;
        }
    }  

    public Dictionary<string, object> GetPackages(Dictionary<string, object> lockData)
    {
        if (!lockData.TryGetValue("dependencies", out var dependenciesObj) || dependenciesObj is not Dictionary<string, object> dependencyMap)
        {
            return new Dictionary<string, object>();
        }
        return dependencyMap;
    }

    public void UpsertPackage(Dictionary<string, object> lockData, string packageName, string version, Dictionary<string, object> source, Dictionary<string, object> dependencies)
    {
        if (!lockData.TryGetValue("dependencies", out var dependenciesObj) || dependenciesObj is not Dictionary<string, object> dependencyMap)
        {
            dependencyMap = new Dictionary<string, object>();
            lockData["dependencies"] = dependencyMap;
        }

        dependencyMap[packageName] = new Dictionary<string, object>
        {
            ["version"] = version,
            ["source"] = source,
            ["integrity"] = source.GetValueOrDefault("integrity", "sha256:unknown"),
            ["dependencies"] = dependencies
        };
    }

    public void RemovePackage(Dictionary<string, object> lockData, string packageName)
    {
        if (lockData.TryGetValue("dependencies", out var dependenciesObj) && dependenciesObj is Dictionary<string, object> dependencyMap && dependencyMap.ContainsKey(packageName))
            dependencyMap.Remove(packageName);
    }

    private static string ResolveAbsolutePath(string path)
    {
        var relativeOrAbsolute = string.IsNullOrWhiteSpace(path) ? PackageManagerCore.ManifestPath : path;
        return ProjectSettings.GlobalizePath(relativeOrAbsolute);
    }
    
    private static Dictionary<string, object> ConvertJsonObject(JsonElement element)
    {
        var result = new Dictionary<string, object>();
        foreach (var property in element.EnumerateObject())
            result[property.Name] = ConvertJsonValue(property.Value);

        return result;
    }

    private static object ConvertJsonValue(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                return ConvertJsonObject(element);
            case JsonValueKind.Array:
                var list = new List<object>();
                foreach (var item in element.EnumerateArray())
                    list.Add(ConvertJsonValue(item));
                return list;
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                if (element.TryGetInt64(out var longValue))
                    return longValue;
                return element.GetDouble();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
                return null;
            default:
                return element.ToString();
        }
    }
}
