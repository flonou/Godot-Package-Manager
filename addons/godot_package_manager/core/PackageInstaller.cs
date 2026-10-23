using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Godot;
using GodotPackageManager.Core.Models;

using NetHttpClient = System.Net.Http.HttpClient;

namespace GodotPackageManager.Core;

[GlobalClass]
public partial class PackageInstaller : RefCounted
{
    private readonly LockfileStore _lockfileStore;
    private static readonly NetHttpClient HttpClient = new();

    public PackageInstaller()
    {
        _lockfileStore = new LockfileStore();
    }

    public PackageInstaller(LockfileStore lockfileStore)
    {
        _lockfileStore = lockfileStore ?? new LockfileStore();
    }

    public string GetCacheRoot()
    {
        var localAppData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "GodotPackageManager", "NpmCache");
    }

    public string BuildPackageDirectoryName(string packageName, string version)
    {
        var normalizedName = NormalizePackageDirectoryName(packageName);
        var cleanedVersion = (version ?? "latest").Trim();

        if (string.IsNullOrWhiteSpace(normalizedName))
            return string.IsNullOrWhiteSpace(cleanedVersion) || cleanedVersion == "latest" ? "package" : $"package-{cleanedVersion}";

        return string.IsNullOrWhiteSpace(version) ? normalizedName : $"{normalizedName}-{cleanedVersion}";
    }

    private static string NormalizePackageDirectoryName(string packageName)
    {
        var normalizedName = (packageName ?? string.Empty).Trim();
        normalizedName = normalizedName.Replace('\\', '/');
        normalizedName = normalizedName.Replace("@", string.Empty);
        normalizedName = normalizedName.Replace("/", "-");
        normalizedName = normalizedName.Replace("--", "-");
        normalizedName = normalizedName.Trim('-');
        return normalizedName;
    }

    private static void DeletePathIfExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
            return;
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void CreateOrUpdateSymbolicLink(string targetPath, string linkPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath) || string.IsNullOrWhiteSpace(linkPath))
            return;

        DeletePathIfExists(linkPath);
        Directory.CreateDirectory(Path.GetDirectoryName(linkPath) ?? linkPath);
        Directory.CreateSymbolicLink(linkPath, targetPath);
    }

    private static string GetSymbolicLinkTarget(string linkPath)
    {
        if (string.IsNullOrWhiteSpace(linkPath))
            return null;
        var targetFileInfo = Directory.ResolveLinkTarget(linkPath, true);
        if (targetFileInfo == null)
            return null;
        return targetFileInfo.FullName;
    }

    public PackageInstallationResult DownloadPackage(PackageVersionEntry packageVersion, string overrideCacheRoot = null)
    {
        if (packageVersion == null)
            return new PackageInstallationResult { Ok = false, Error = "Missing package version information" };

        if (string.IsNullOrWhiteSpace(packageVersion.TarballUrl))
            return new PackageInstallationResult { Ok = false, Error = "Tarball URL was not found in package metadata", PackageVersion = packageVersion };

        var packageName = packageVersion.Package?.Name;

        var version = packageVersion.Version;
        var tarballRoot = string.IsNullOrWhiteSpace(overrideCacheRoot) ? GetTarballCacheRoot() : overrideCacheRoot;
        var extractedRoot = GetExtractedCacheRoot();
        Directory.CreateDirectory(tarballRoot);
        Directory.CreateDirectory(extractedRoot);

        var packageDirName = BuildPackageDirectoryName(packageName, version);
        var tarballPath = Path.Combine(tarballRoot, $"{packageDirName}.tgz");
        var extractedDir = Path.Combine(extractedRoot, packageDirName);

        using var response = HttpClient.GetAsync(packageVersion.TarballUrl).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
            return new PackageInstallationResult
            {
                Ok = false,
                Error = $"HTTP {response.StatusCode} while downloading {packageName}@{version}",
                PackageVersion = packageVersion,
                CacheDir = tarballRoot
            };

        var payload = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
        File.WriteAllBytes(tarballPath, payload);

        if (!ExtractTarballToDirectory(tarballPath, extractedDir))
            return new PackageInstallationResult
            {
                Ok = false,
                Error = $"Unable to extract {packageName}@{version} from {tarballPath}",
                PackageVersion = packageVersion,
                CacheDir = tarballRoot
            };

        return new PackageInstallationResult
        {
            Ok = true,
            PackageVersion = packageVersion,
            CacheDir = extractedDir
        };
    }


    public string GetTarballCacheRoot()
    {
        return Path.Combine(GetCacheRoot(), "tarballs");
    }

    public string GetExtractedCacheRoot()
    {
        return Path.Combine(GetCacheRoot(), "packages");
    }

    public string GetPackagesRootPath(string projectRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(projectRoot))
        {
            var resolved = Path.IsPathRooted(projectRoot) ? projectRoot : ProjectSettings.GlobalizePath(projectRoot);
            if (resolved.EndsWith(".godot/packages", StringComparison.OrdinalIgnoreCase) || resolved.EndsWith(".godot\\packages", StringComparison.OrdinalIgnoreCase))
                return ProjectSettings.GlobalizePath(PackageManagerCore.RootPackagePath);
            return resolved;
        }

        return ProjectSettings.GlobalizePath(PackageManagerCore.RootPackagePath);
    }

    public string GetPackagesLinkRootPath(string projectRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(projectRoot))
        {
            var resolved = Path.IsPathRooted(projectRoot) ? projectRoot : ProjectSettings.GlobalizePath(projectRoot);
            if (resolved.EndsWith(".godot/packages", StringComparison.OrdinalIgnoreCase) || resolved.EndsWith(".godot\\packages", StringComparison.OrdinalIgnoreCase))
                return ProjectSettings.GlobalizePath(PackageManagerCore.RootPackageLinkPath);
            return resolved;
        }

        return ProjectSettings.GlobalizePath(PackageManagerCore.RootPackageLinkPath);
    }

    public string GetPackagePath(string packageName, string version, string projectRoot = null)
    {
        var realRoot = GetPackagesRootPath(projectRoot);
        var normalizedName = NormalizePackageDirectoryName(packageName);
        var normalizedVersion = version ?? string.Empty;
        return Path.Combine(realRoot, BuildPackageDirectoryName(normalizedName, normalizedVersion));
    }

    public string GetPackageLinkPath(string packageName, string projectRoot = null)
    {
        var normalizedName = NormalizePackageDirectoryName(packageName);
        var linkRoot = GetPackagesLinkRootPath(projectRoot);
        return Path.Combine(linkRoot, normalizedName);
    }

    public bool IsPackageInstalled(string packageName, string projectRoot = null)
    {
        return Directory.Exists(GetPackageLinkPath(packageName, projectRoot));
    }

    public bool IsPackageVersionInstalled(PackageVersionEntry packageVersion)
    {
        // Check that the link exists and points to the correct folder
        var localPackageVersionRoot = GetExtractedCacheRoot();
        var packageVersionPath = Path.Combine(localPackageVersionRoot, BuildPackageDirectoryName(packageVersion.Name, packageVersion.Version));
        if (!Directory.Exists(packageVersionPath))
            return false;
        if (!IsPackageInstalled(packageVersion.Name))
            return false;
        return Path.GetFullPath(packageVersionPath) == Path.GetFullPath(GetSymbolicLinkTarget(GetPackageLinkPath(packageVersion.Name)));
    }

    public bool RemovePackage(string packageName, string version, string projectRoot = null)
    {
        var linkPath = GetPackageLinkPath(packageName, projectRoot);
        var realPath = GetPackagePath(packageName, version, projectRoot);
        var removed = Directory.Exists(linkPath) || Directory.Exists(realPath) || File.Exists(linkPath) || File.Exists(realPath);

        DeletePathIfExists(linkPath);
        DeletePathIfExists(realPath);
        return removed;
    }

    public PackageInstallationResult InstallPackage(PackageVersionEntry packageVersion, string projectRoot = null)
    {
        if (packageVersion == null)
            return new PackageInstallationResult { Ok = false, Error = "Missing package version information" };

        var packageName = packageVersion.Name;
        if (string.IsNullOrWhiteSpace(packageName))
            return new PackageInstallationResult { Ok = false, Error = "Package name is required" };

        var version = packageVersion.Version;
        if (string.IsNullOrWhiteSpace(version))
            version = packageVersion.Package?.LatestVersion ?? string.Empty;
        if (string.IsNullOrWhiteSpace(version))
            return new PackageInstallationResult { Ok = false, Error = "Package version is required" };

        var packageLinkPath = GetPackageLinkPath(packageName, projectRoot);
        var packageActualPath = GetPackagePath(packageName, version, projectRoot);
        if (IsPackageVersionInstalled(packageVersion))
            return new PackageInstallationResult { Ok = true, PackageVersion = packageVersion, ProjectDir = packageLinkPath, CacheDir = packageActualPath };


        var extractedRoot = GetExtractedCacheRoot();
        var extractedPath = Path.Combine(extractedRoot, BuildPackageDirectoryName(packageName, version));

        // Found package already extracted
        if (Directory.Exists(extractedPath))
        {
            return CopyPackageToProject(extractedPath, packageActualPath, packageVersion, projectRoot);
        }

        // Found tarball but not extracted yet 
        var tarballPath = Path.Combine(GetTarballCacheRoot(), $"{BuildPackageDirectoryName(packageName, version)}.tgz");
        if (File.Exists(tarballPath))
        {
            if (!ExtractTarballToDirectory(tarballPath, extractedPath))
                return new PackageInstallationResult { Ok = false, Error = $"Unable to extract cached tarball for {packageName}@{version}", PackageVersion = packageVersion };

            return CopyPackageToProject(extractedPath, packageActualPath, packageVersion, projectRoot);
        }

        // Need to Download and extract (downloading will extract)
        var downloaded = DownloadPackage(packageVersion, GetTarballCacheRoot());
        if (!downloaded.Ok)
            return downloaded;

        if (!Directory.Exists(extractedPath))
            return new PackageInstallationResult { Ok = false, Error = $"Package contents were not extracted for {packageName}@{version}", PackageVersion = packageVersion };

        return CopyPackageToProject(extractedPath, packageActualPath, packageVersion, projectRoot);
    }

    private PackageInstallationResult CopyPackageToProject(string extractedSourceDirectory, string packageRealPath, PackageVersionEntry packageVersion, string projectRoot = null)
    {
        if (string.IsNullOrWhiteSpace(extractedSourceDirectory) || !Directory.Exists(extractedSourceDirectory))
            return new PackageInstallationResult { Ok = false, Error = "Imported package content was not found in cache", PackageVersion = packageVersion };

        if (packageVersion == null)
            return new PackageInstallationResult { Ok = false, Error = "Package version is required", PackageVersion = null };

        var sourceRoot = ResolvePackageContentDirectory(extractedSourceDirectory);
        if (string.IsNullOrWhiteSpace(sourceRoot))
            return new PackageInstallationResult { Ok = false, Error = "Package cache directory is empty", PackageVersion = packageVersion };

        var linkPath = GetPackageLinkPath(packageVersion.Package.Name, projectRoot);
        DeletePathIfExists(linkPath);
        DeletePathIfExists(packageRealPath);

        Directory.CreateDirectory(Path.GetDirectoryName(packageRealPath) ?? packageRealPath);
        CopyDirectory(sourceRoot, packageRealPath);
        CreateOrUpdateSymbolicLink(packageRealPath, linkPath);

        return new PackageInstallationResult
        {
            Ok = true,
            PackageVersion = packageVersion,
            CacheDir = extractedSourceDirectory,
            ProjectDir = linkPath
        };
    }

    private static string ResolvePackageContentDirectory(string extractedSourceDirectory)
    {
        if (!Directory.Exists(extractedSourceDirectory))
            return string.Empty;

        var subDirectories = Directory.GetDirectories(extractedSourceDirectory);
        if (subDirectories.Length == 1)
            return subDirectories[0];

        return extractedSourceDirectory;
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var filePath in Directory.GetFiles(sourceDirectory))
        {
            var targetPath = Path.Combine(targetDirectory, Path.GetFileName(filePath));
            File.Copy(filePath, targetPath, true);
        }

        foreach (var childDirectory in Directory.GetDirectories(sourceDirectory))
        {
            var targetPath = Path.Combine(targetDirectory, Path.GetFileName(childDirectory));
            CopyDirectory(childDirectory, targetPath);
        }
    }

    private static bool ExtractTarballToDirectory(string tarballPath, string targetDirectory)
    {
        if (!File.Exists(tarballPath))
            return false;

        Directory.CreateDirectory(targetDirectory);

        try
        {
            var executable = OperatingSystem.IsWindows() ? "tar.exe" : "tar";
            var arguments = OperatingSystem.IsWindows()
                ? $"-xzf \"{tarballPath}\" -C \"{targetDirectory}\""
                : $"-xzf \"{tarballPath}\" -C \"{targetDirectory}\"";

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                RedirectStandardError = false,
                RedirectStandardOutput = false,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process == null)
                return false;

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public Dictionary<string, object> InstallFromResolution(Dictionary<string, object> resolution, string lockFilePath = PackageManagerCore.LockFilePath)
    {
        var lockData = _lockfileStore.Read(lockFilePath);
        var resolved = resolution.TryGetValue("resolved", out var resolvedObj) && resolvedObj is Dictionary<string, object> resolvedMap ? resolvedMap : new Dictionary<string, object>();

        foreach (var dependency in resolved)
        {
            var packageName = dependency.Key;
            if (dependency.Value is not Dictionary<string, object> entry)
                continue;

            var source = new Dictionary<string, object>
            {
                ["type"] = "registry",
                ["registry"] = entry.GetValueOrDefault("registry", "npmjs"),
                ["url"] = entry.GetValueOrDefault("source", "https://registry.npmjs.org"),
                ["integrity"] = "sha256:placeholder"
            };

            var childDependencies = entry.TryGetValue("dependencies", out var childDependenciesObj) && childDependenciesObj is Dictionary<string, object> childMap
                ? childMap
                : new Dictionary<string, object>();

            _lockfileStore.UpsertPackage(lockData, packageName, entry.GetValueOrDefault("version", "0.0.0").ToString(), source, childDependencies);
        }

        var written = _lockfileStore.Write(lockData, lockFilePath);
        if (!written)
            return new Dictionary<string, object> { ["ok"] = false, ["error"] = "Unable to write lockfile" };

        var cacheRoot = ProjectSettings.GlobalizePath("res://.godot/packages");
        Directory.CreateDirectory(cacheRoot);
        return new Dictionary<string, object> { ["ok"] = true, ["cache_root"] = cacheRoot, ["lockfile"] = lockFilePath };
    }
}
