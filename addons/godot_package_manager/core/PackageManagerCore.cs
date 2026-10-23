using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;

using GodotPackageManager.Core.Models;
using GodotPackageManager.NpmRegistryClient;

namespace GodotPackageManager.Core;

[GlobalClass]
public partial class PackageManagerCore : RefCounted
{
    private static PackageManagerCore _instance = new();
    public static PackageManagerCore GetInstance() => _instance;

    public const string ManifestPath = "res://project-manifest.json";
    public const string LockFilePath = "res://packages-lock.json";
    public const string RootPackagePath = "res://.godot/packages";
    public const string RootPackageLinkPath = "res://packages";

    private ManifestData _manifestData;
    private readonly ManifestFileHandler _manifestFileHandler;
    private LockfileData _lockfileData;
    private readonly LockFileHandler _lockfileHandler;
    
    private readonly Dictionary<string, PackageSummary> _availablePackagesCache = new Dictionary<string, PackageSummary>();

    private readonly Dictionary<string, Godot.Collections.Array<PackageSummary>> _availablePackagesByRegistryCache = new ();

    private readonly PackageInstaller _packageInstaller = new();


    public PackageManagerCore()
    {
        _instance = this;
        _manifestFileHandler = new ManifestFileHandler(ManifestPath);
        _lockfileHandler = new LockFileHandler(LockFilePath);
        RefreshManifestData();
        RefreshAvailablePackagesCache();
        RefreshInstalledPackages();
    }


    public Godot.Collections.Array<PackageSummary> GetAllAvailablePackages()
    {
        return new Godot.Collections.Array<PackageSummary>(_availablePackagesCache.Values.ToArray());
    }

    public PackageSummary GetOrCreatePackageFromCache(string packageName)
    {
        if (!_availablePackagesCache.TryGetValue(packageName, out PackageSummary res))
        {
            res = new PackageSummary()
            {
                Name = packageName
            };
            _availablePackagesCache[packageName] = res;
        }
        return res;
    }

    public bool TryGetPackageFromCache(string packageName, out PackageSummary res)
    {
        return _availablePackagesCache.TryGetValue(packageName, out res);
    }

    public PackageSearchResult QueryPackages(NpmRegistryEntry registryEntry, int limit = 100)
    {
        var client = new HttpPackageRegistryClient();
        PackageSearchResult res = client.QueryPackages(registryEntry, limit);
        // Cache the result
        foreach (PackageSummary package in res.Packages)
            _availablePackagesCache[package.Name] = package;
        return res;
    }

    public PackageVersionResult GetPackageVersions(string registryUrl, string packageName)
    {
        var package = GetOrCreatePackageFromCache(packageName);
        package.Registry = registryUrl;
        return GetPackageVersions(package);
    }

    public PackageVersionResult GetPackageVersions(PackageSummary package)
    {
        if (package == null || string.IsNullOrWhiteSpace(package.Registry) || string.IsNullOrWhiteSpace(package.Name))
            return new PackageVersionResult { Ok = false, Error = "Invalid package summary" };

        var client = new HttpPackageRegistryClient();
        PackageMetadataResult metadata = client.FetchPackageMetadata(package);

        if (!metadata.Ok)
            return new PackageVersionResult { Ok = false, Error = metadata.Error };

        return metadata.ToPackageVersionResult(package);
    }

    public PackageVersionResult GetPackageVersions(PackageVersionEntry packageVersion)
    {
        if (packageVersion == null)
            return new PackageVersionResult { Ok = false, Error = "Invalid package version" };

        return GetPackageVersions(packageVersion.Package);
    }

    public PackageVersionEntry GetInstalledPackageVersion(string packageName)
    {
        if (string.IsNullOrEmpty(packageName))
            return null;

        if (_lockfileData.Packages.TryGetValue(packageName, out var cachedPackage))
            return cachedPackage;

        return _lockfileData.Packages.TryGetValue(packageName, out var refreshedPackage) ? refreshedPackage : null;
    }

    public Dictionary<string, PackageVersionEntry> GetInstalledPackages()
    {
        return new Dictionary<string, PackageVersionEntry>(_lockfileData.Packages, StringComparer.OrdinalIgnoreCase);
    }

    public void RefreshManifestData()
    {
        _manifestData = _manifestFileHandler.Load();
    }

    public void SaveManifestData()
    {
        _manifestFileHandler.Save(_manifestData);
    }

    public void RefreshAvailablePackagesCache()
    {
        _availablePackagesCache.Clear();
        _availablePackagesByRegistryCache.Clear();
        foreach (var registry in GetScopedRegistries())
        {
            var result = QueryPackages(registry, 100);
            if (result == null || !result.Ok)
            {
                GD.PrintErr("Could not query registry: ", result != null ? result.Error  : "Null response");
            }
            else
            {
                _availablePackagesByRegistryCache[registry.Name] = result.Packages;
                foreach(var package in result.Packages)
                    _availablePackagesCache[package.Name] = package;
            }
        }
    }

    public Godot.Collections.Array<PackageSummary> GetAvailablePackagesFromRegistry(string registryName)
    {
        if (!_availablePackagesByRegistryCache.TryGetValue(registryName, out Godot.Collections.Array<PackageSummary> res))
        {
            return new Godot.Collections.Array<PackageSummary>();
        }
        return res;
    }

    /// <summary>
    /// The main idea is to check if the lockfile is up to date with the manifest.
    /// If not, reinstall all packages in the manifest
    /// Then check that all supposedly installed packages from the updated lockfile are indeed correclty installed
    /// </summary>
    public void RefreshInstalledPackages()
    {    

        if (!_lockfileHandler.FileExists)
        {
            InstallPackagesFromManifest();
        }

        _lockfileData = _lockfileHandler.Load();

        // Check that the lock file is up to date with the manifest file
        foreach (var pair in _manifestData.Dependencies)
        {
            // Check that a repository knows about the package
            if (!_availablePackagesCache.TryGetValue(pair.Key, out var package))
            {
                GD.PrintErr($"Could not resolve package {pair.Key}");
                continue;
            }
            // If the package is not installed, install it
            if (!_lockfileData.Packages.TryGetValue(pair.Key, out var packageVersion))
            {
                GD.Print("not intalled from the lock file data");
                InstallPackage(package, pair.Value);
                continue;
            }
            // If the wrong package version is installed, remove and install the correct one
            if (packageVersion.Version != pair.Value)
            {
                GD.Print("wrong version intalled from the lock file data");
                RemovePackage(pair.Key);
                InstallPackage(package, pair.Value);
            }
        }

        // Check that all the packages in the lock file are correctly installed
        foreach (var pair in _lockfileData.Packages)
        {
            
            var packageName = pair.Key;

            // Check that the package is installed
            if (!_packageInstaller.IsPackageVersionInstalled(pair.Value))
            {
                var packageInstallationResult = InstallPackage(pair.Value);
                if (!packageInstallationResult.Ok)
                {
                    GD.PrintErr($"Could not install missing package {pair.Value.Name}-{pair.Value.Version}: {packageInstallationResult.Error}");
                    continue;
                }
            }

        }
    }

    public void InstallPackagesFromManifest()
    {
        _lockfileData = new LockfileData();
        foreach (var pair in _manifestData.Dependencies)
        {
            if (_availablePackagesCache.TryGetValue (pair.Key, out var package))
            {
                var getVersionsResults = GetPackageVersions(package);
                if (getVersionsResults.Ok)
                {
                    if (getVersionsResults.Versions.TryGetValue(pair.Value, out var parckageVersion))
                    {
                        var packageInstallationResult = InstallPackage(parckageVersion);
                        if (!packageInstallationResult.Ok)
                        {
                            GD.PrintErr($"Could not install missing package {pair.Key}-{pair.Value}: {packageInstallationResult.Error}");
                        }
                    }
                } else
                {
                    GD.PrintErr($"Could not fetch package {pair.Key} versions in registry {package.Registry}");    
                }
            } else
            {
                GD.PrintErr($"Could not find package named {pair.Key} in registries");
            }
            
        }
    }

    public void AddOrUpdateInstalledPackage(PackageVersionEntry packageVersion)
    {
        if (packageVersion == null || string.IsNullOrWhiteSpace(packageVersion.Name))
            return;

        _lockfileData.Packages[packageVersion.Name] = packageVersion;
        _lockfileHandler.Save(_lockfileData);
    }

    public void RemoveInstalledPackage(string packageName)
    {
        if (string.IsNullOrWhiteSpace(packageName))
            return;

        RemoveDependency(packageName);
        _lockfileData.Packages.Remove(packageName);
        _lockfileHandler.Save(_lockfileData);
    }

    public bool HasInstalledPackage(string packageName)
    {
        return !string.IsNullOrWhiteSpace(packageName) && _lockfileData.Packages.ContainsKey(packageName);
    }

    public PackageInstallationResult DownloadPackage(string registryUrl, string packageName, string version)
    {
        if (string.IsNullOrWhiteSpace(registryUrl) || string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(version))
            return new PackageInstallationResult { Ok = false, Error = "Invalid package download request" };

        var package = GetOrCreatePackageFromCache(packageName);
        package.Registry = registryUrl;

        return DownloadPackage(package, version );
    }

    public PackageInstallationResult DownloadPackage(PackageSummary package, string version)
    {
        if (package == null)
            return new PackageInstallationResult { Ok = false, Error = "Missing package summary" };

        var versionsResult = GetPackageVersions(package);
        if (!versionsResult.Ok)
            return new PackageInstallationResult { Ok = false, Error = versionsResult.Error };

        if (!versionsResult.Versions.TryGetValue(version, out var versionEntry))
            return new PackageInstallationResult { Ok = false, Error = $"Version {version} was not found for {package.Name}" };

        return DownloadPackage(versionEntry);
    }

    public PackageInstallationResult DownloadPackage(PackageVersionEntry packageVersion)
    {
        return _packageInstaller.DownloadPackage(packageVersion);
    }


    public PackageInstallationResult InstallPackage(PackageVersionEntry packageVersion)
    {
        var result = _packageInstaller.InstallPackage(packageVersion, RootPackagePath);
        if (result != null && result.Ok)
        {
            var installedEntry = result.PackageVersion ?? packageVersion;
            if (installedEntry != null)
                AddOrUpdateInstalledPackage(installedEntry);
        }

        return result;
    }

    public PackageInstallationResult InstallPackage(PackageSummary package, string version)
    {
        if (package == null)
            return new PackageInstallationResult { Ok = false, Error = "Missing package summary" };

        var versionsResult = GetPackageVersions(package);
        if (!versionsResult.Ok)
            return new PackageInstallationResult { Ok = false, Error = versionsResult.Error };
        if (!versionsResult.Versions.TryGetValue(version, out var versionEntry))
            return new PackageInstallationResult { Ok = false, Error = $"Version {version} was not found for {package.Name}" };

        return InstallPackage(versionEntry);
    }

    public PackageInstallationResult InstallPackage(string registryUrl, string packageName, string version)
    {
        if (string.IsNullOrWhiteSpace(registryUrl) || string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(version))
            return new PackageInstallationResult { Ok = false, Error = "Invalid package download request" };


        var package = GetOrCreatePackageFromCache(packageName);
        package.Registry = registryUrl;
        return InstallPackage(package, version );
    }

    public bool RemovePackage(PackageVersionEntry packageVersion)
    {
        return RemovePackage(packageVersion.Name, packageVersion.Version);
    }

    public bool RemovePackage(string packageName)
    {
        if (_lockfileData.Packages.TryGetValue(packageName, out var packageVersion))
        {
            return RemovePackage(packageVersion);
        }
        return false;
    }

    public bool RemovePackage(string packageName, string version)
    {
        var removed = _packageInstaller.RemovePackage(packageName, version, RootPackagePath);
        if (removed)
            RemoveInstalledPackage(packageName);

        return removed;
    }


    public bool AddDependency(string packageName, string version)
    {
        if (string.IsNullOrWhiteSpace(packageName))
            return false;
        if (HasDependency(packageName))
            return false;

        version = string.IsNullOrWhiteSpace(version) ? "1.0.0" : version;

        _manifestData.Dependencies.Add(packageName, version);
        _manifestFileHandler.Save(_manifestData);
        return true;
    }

    public bool AddDependency(PackageVersionEntry packageVersion)
    {
        if (packageVersion == null)
            return false;

        var packageName = packageVersion.Package?.Name;
        if (string.IsNullOrWhiteSpace(packageName))
            packageName = packageVersion.Name;

        return AddDependency(packageName ?? string.Empty, packageVersion.Version);
    }

    public bool RemoveDependency(string packageName)
    {
        if (string.IsNullOrWhiteSpace(packageName))
            return false;

        bool res = _manifestData.Dependencies.Remove(packageName);
        if (res)
        {
            _manifestFileHandler.Save(_manifestData);
        }
        return res;
    }

    public bool RemoveDependency(PackageSummary package)
    {
        if (package == null)
            return false;

        return RemoveDependency(package.Name);
    }

    public bool HasDependency(string packageName)
    {
        return _manifestData.Dependencies.ContainsKey(packageName);
    }

    public bool HasDependency(PackageSummary package)
    {
        if (package == null)
            return false;

        return HasDependency(package.Name);
    }

    public Godot.Collections.Array<NpmRegistryEntry> GetScopedRegistries()
    {
        var nativeList = _manifestData.ScopedRegistries;
    
        return new Godot.Collections.Array<NpmRegistryEntry>(nativeList);
    }

    public NpmRegistryEntry GetScopedRegistry(string name)
    {
        return _manifestData.ScopedRegistries.FirstOrDefault(entry => entry.Name == name);
    }

    public void AddScopedRegistry(NpmRegistryEntry registry)
    {
        _manifestData.ScopedRegistries.Add(registry);
        _manifestFileHandler.Save(_manifestData);
    }

    public bool RemoveScopedRegistry(NpmRegistryEntry entry)
    {
        if (entry == null)
            return false;
        bool res = _manifestData.ScopedRegistries.Remove(entry);
        if (res)
        {
            _manifestFileHandler.Save(_manifestData);
        }
        return res;
    }

    
}

