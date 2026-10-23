using System.Collections.Generic;
using System.Text.Json.Serialization;
using GodotPackageManager.Core.Models;

namespace GodotPackageManager.Core;

public class LockfileData
{
    [JsonPropertyName("packages")]
    public Dictionary<string, PackageVersionEntry> Packages { get; set; } = new();
}