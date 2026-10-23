
using GodotPackageManager.Core.Models;

namespace GodotPackageManager.NpmRegistryClient;

public interface IPackageRegistryClient
{
    PackageMetadataResult FetchPackageMetadata(PackageSummary package);
    PackageSearchResult QueryPackages(NpmRegistryEntry registryEntry, int limit = 100);
}
