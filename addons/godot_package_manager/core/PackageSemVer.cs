using System;
using System.Collections.Generic;
using System.Linq;

namespace GodotPackageManager.Core;

public static class PackageSemVer
{
    public static int Compare(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right))
            return 0;
        if (string.IsNullOrWhiteSpace(left))
            return -1;
        if (string.IsNullOrWhiteSpace(right))
            return 1;

        var leftVersion = ParseVersion(left);
        var rightVersion = ParseVersion(right);

        if (leftVersion.Major != rightVersion.Major)
            return leftVersion.Major.CompareTo(rightVersion.Major);
        if (leftVersion.Minor != rightVersion.Minor)
            return leftVersion.Minor.CompareTo(rightVersion.Minor);
        if (leftVersion.Patch != rightVersion.Patch)
            return leftVersion.Patch.CompareTo(rightVersion.Patch);

        if (string.IsNullOrEmpty(leftVersion.Prerelease) && string.IsNullOrEmpty(rightVersion.Prerelease))
            return 0;
        if (string.IsNullOrEmpty(leftVersion.Prerelease))
            return 1;
        if (string.IsNullOrEmpty(rightVersion.Prerelease))
            return -1;

        var leftIdentifiers = leftVersion.Prerelease.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var rightIdentifiers = rightVersion.Prerelease.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var max = Math.Max(leftIdentifiers.Length, rightIdentifiers.Length);
        for (var i = 0; i < max; i++)
        {
            if (i >= leftIdentifiers.Length)
                return -1;
            if (i >= rightIdentifiers.Length)
                return 1;

            var leftIdentifier = leftIdentifiers[i];
            var rightIdentifier = rightIdentifiers[i];

            var leftIsNumeric = int.TryParse(leftIdentifier, out var leftNumericValue);
            var rightIsNumeric = int.TryParse(rightIdentifier, out var rightNumericValue);

            if (leftIsNumeric && rightIsNumeric)
            {
                var numericCompare = leftNumericValue.CompareTo(rightNumericValue);
                if (numericCompare != 0)
                    return numericCompare;
                continue;
            }

            if (leftIsNumeric != rightIsNumeric)
                return leftIsNumeric ? -1 : 1;

            var textCompare = string.CompareOrdinal(leftIdentifier, rightIdentifier);
            if (textCompare != 0)
                return textCompare;
        }

        return 0;
    }

    public static bool Satisfies(string version, string range)
    {
        if (string.IsNullOrWhiteSpace(range) || range == "*" || range == "x" || range == "X")
            return true;

        var normalized = range.Trim();

        if (normalized.Contains("||"))
        {
            return normalized.Split("||", StringSplitOptions.TrimEntries).Any(part => Satisfies(version, part));
        }

        if (normalized.Contains(","))
        {
            return normalized.Split(',', StringSplitOptions.TrimEntries).All(part => Satisfies(version, part));
        }

        normalized = NormalizeVersion(normalized);

        if (normalized.StartsWith("^", StringComparison.Ordinal))
        {
            var baseVersion = normalized[1..].Trim();
            return Compare(version, baseVersion) >= 0 && Compare(version, GetCaretUpperBound(baseVersion)) < 0;
        }

        if (normalized.StartsWith("~", StringComparison.Ordinal))
        {
            var baseVersion = normalized[1..].Trim();
            return Compare(version, baseVersion) >= 0 && Compare(version, GetTildeUpperBound(baseVersion)) < 0;
        }
        if (normalized.StartsWith(">=", StringComparison.Ordinal))
        {
            return Compare(version, normalized[2..].Trim()) >= 0;
        }

        if (normalized.StartsWith(">", StringComparison.Ordinal))
        {
            return Compare(version, normalized[1..].Trim()) > 0;
        }

        if (normalized.StartsWith("<=", StringComparison.Ordinal))
        {
            return Compare(version, normalized[2..].Trim()) <= 0;
        }

        if (normalized.StartsWith("<", StringComparison.Ordinal))
        {
            return Compare(version, normalized[1..].Trim()) < 0;
        }

        return Compare(version, normalized) == 0;
    }

    public static string ResolveHighestSatisfying(IEnumerable<string> versions, string range)
    {
        var candidates = versions
            .Where(v => !string.IsNullOrWhiteSpace(v) && Satisfies(v, range))
            .OrderBy(v => v, Comparer<string>.Create((left, right) => -Compare(left, right)))
            .ToList();

        return candidates.FirstOrDefault() ?? string.Empty;
    }

    private static VersionData ParseVersion(string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return new VersionData(0, 0, 0, string.Empty);

        var core = trimmed;
        var prerelease = string.Empty;

        var plusIndex = core.IndexOf('+');
        if (plusIndex >= 0)
            core = core.Substring(0, plusIndex);

        var hyphenIndex = core.IndexOf('-');
        if (hyphenIndex >= 0)
        {
            prerelease = core.Substring(hyphenIndex + 1);
            core = core.Substring(0, hyphenIndex);
        }

        var parts = core.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var major = 0;
        var minor = 0;
        var patch = 0;

        if (parts.Length > 0 && int.TryParse(parts[0], out var majorValue))
            major = majorValue;
        if (parts.Length > 1 && int.TryParse(parts[1], out var minorValue))
            minor = minorValue;
        if (parts.Length > 2 && int.TryParse(parts[2], out var patchValue))
            patch = patchValue;

        return new VersionData(major, minor, patch, prerelease);
    }

    private static string NormalizeVersion(string version)
    {
        var parts = version.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1) return $"{parts[0]}.0.0";
        if (parts.Length == 2) return $"{parts[0]}.{parts[1]}.0";
        return version;
    }

    private static string GetCaretUpperBound(string version)
    {
        var v = ParseVersion(version);
        if (v.Major > 0) return $"{v.Major + 1}.0.0";
        if (v.Minor > 0) return $"0.{v.Minor + 1}.0";
        return $"0.0.{v.Patch + 1}";
    }

    private static string GetTildeUpperBound(string version)
    {
        var v = ParseVersion(version);
        return $"{v.Major}.{v.Minor + 1}.0";
    }

    private readonly struct VersionData
    {
        public VersionData(int major, int minor, int patch, string prerelease)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            Prerelease = prerelease ?? string.Empty;
        }

        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public string Prerelease { get; }
    }
}
