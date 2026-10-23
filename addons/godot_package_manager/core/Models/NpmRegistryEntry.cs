using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Godot;

namespace GodotPackageManager.Core.Models;

[GlobalClass]
public partial class NpmRegistryEntry : RefCounted
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("url")]
    public string Url { get; set; } = "https://";
    [JsonPropertyName("scopes")]
    public Godot.Collections.Array<string> Scopes { get; set; } = new();
    public Godot.Collections.Dictionary<string, Variant> Auth { get; set; } = new();

    public NpmRegistryEntry()
    {
    }     

    public NpmRegistryEntry(string name, string url, IEnumerable<string> scopes = null, Dictionary<string, object> auth = null)
    {
        var godotAuth = new Godot.Collections.Dictionary<string, Variant>();
        if (auth != null)
        {
            foreach (var kvp in auth)
            {
                godotAuth[kvp.Key] = Variant.From(kvp.Value);
            }   
        }

        Init(name, url, new Godot.Collections.Array<string>(scopes), godotAuth);
    }


    public void Init(string name, string url, Godot.Collections.Array<string> scopes = null, Godot.Collections.Dictionary<string, Variant> auth = null)
    {
        Name = name ?? string.Empty;
        Url = url ?? string.Empty;
        Scopes = new Godot.Collections.Array<string>();

        if (scopes != null)
        {
            foreach (var scope in scopes)
            {
                if (!string.IsNullOrWhiteSpace(scope))
                    Scopes.Add(scope.Trim());
            }
        }
        Auth = auth ?? new Godot.Collections.Dictionary<string, Variant>();
    }

    public bool MatchesPackageName(string packageName)
    {
        if (string.IsNullOrWhiteSpace(packageName))
            return false;

        if (Scopes.Count == 0)
            return true;

        foreach (var scope in Scopes)
        {
            if (!string.IsNullOrWhiteSpace(scope) && packageName.StartsWith(scope, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
