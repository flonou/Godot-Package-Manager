using Godot;

namespace GodotPackageManager.Core.Models;

[GlobalClass]
public sealed partial class PackageSummary : RefCounted
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string LatestVersion { get; set; } = string.Empty;
    public PackageVersionEntry LatestVersionData { get; set; }
    public string Scope { get; set; } = string.Empty;
    public string Registry { get; set; } = string.Empty;
}