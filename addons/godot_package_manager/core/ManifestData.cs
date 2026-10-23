using System.Collections.Generic;
using System.Text.Json.Serialization;
using GodotPackageManager.Core.Models;

namespace GodotPackageManager.Core;

public sealed class ManifestData
{
    [JsonPropertyName("dependencies")]
    public Dictionary<string, string> Dependencies { get; set; } = new(System.StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("scopedRegistries")]
    public List<NpmRegistryEntry> ScopedRegistries { get; set; } = new();
}

