using System.Collections.Generic;
using Godot;

namespace GodotPackageManager.Core;

[GlobalClass]
public partial class PackageSemVerResolver : RefCounted
{
    public int Compare(string left, string right)
    {
        return PackageSemVer.Compare(left, right);
    }

    public bool Satisfies(string versionString, string rangeString)
    {
        return PackageSemVer.Satisfies(versionString, rangeString);
    }

    public string ResolveHighestSatisfying(IEnumerable<string> versions, string rangeString)
    {
        return PackageSemVer.ResolveHighestSatisfying(versions, rangeString);
    }
}
