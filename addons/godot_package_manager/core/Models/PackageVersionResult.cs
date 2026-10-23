using Godot;
using Godot.Collections;

namespace GodotPackageManager.Core.Models;

[GlobalClass]
public sealed partial class PackageVersionResult : RefCounted
{
    public bool Ok { get; set; }
    public string Error { get; set; } = string.Empty;
    public string Name => Package?.Name;
    public PackageSummary Package { get; set; }
    public Dictionary<string, PackageVersionEntry> Versions { get; set; } = new();
}