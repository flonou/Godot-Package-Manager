using Godot;

namespace GodotPackageManager.Core.Models;

[GlobalClass]
public sealed partial class PackageInstallationResult : RefCounted
{
    public bool Ok { get; set; }
    public string Error { get; set; } = string.Empty;

    public PackageVersionEntry PackageVersion { get; set; } = new PackageVersionEntry();

    public string CacheDir { get; set; } = string.Empty;
    public string ProjectDir { get; set; } = string.Empty;
}