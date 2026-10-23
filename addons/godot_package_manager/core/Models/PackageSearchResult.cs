using Godot;
using Godot.Collections; 

namespace GodotPackageManager.Core.Models;

[GlobalClass]
public sealed partial class PackageSearchResult : RefCounted
{
    public bool Ok { get; set; }
    public string Error { get; set; } = string.Empty;
    public NpmRegistryEntry Registry { get; set; } = new();
    public string Search { get; set; } = string.Empty;
    public int Count { get; set; }
    public Array<PackageSummary> Packages { get; set; } = new();

}