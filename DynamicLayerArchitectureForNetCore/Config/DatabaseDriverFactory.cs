using System.Data;
using System.Net;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using DynamicLayerArchitectureForNetCore.Exceptions;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;
using NuGet.Versioning;
using ILogger = NuGet.Common.ILogger;

namespace DynamicLayerArchitectureForNetCore.Config;

public static class DatabaseDriverFactory
{
    private const char ForwardSlash = '/';
    private const char BackSlash = '\\';
    private const char Comma = ',';

    private const string DriverFolderName = "packages";
    private const string DriverFileExtension = ".dll";
        
    private static readonly Dictionary<string, string> DriverDictionary = new()
    {
        { "SqlClient", "SqlClient.SqlConnection" },
        { "MySqlConnector", "MySqlConnector.MySqlConnection" }
    };

    private static readonly ILogger Logger = NullLogger.Instance;
    private static readonly CancellationToken CancellationToken = CancellationToken.None;
    private static readonly SourceCacheContext CacheContext = new SourceCacheContext();

    public static async Task InstallDriver(IConfiguration configuration, WebApplicationBuilder builder)
    {
        var sqlDriverName = configuration.GetValue<string>("SqlDriver");
        if (!DriverDictionary.ContainsKey(sqlDriverName))
        {
            throw new DriverInvalidException($"{sqlDriverName} not found or invalid name");
        }
            
        ServicePointManager.Expect100Continue = true;
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

        var repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
        var resource = repository.GetResourceAsync<PackageMetadataResource>(CancellationToken).Result;

        var metadata =
            await resource.GetMetadataAsync(sqlDriverName, true, true, CacheContext, Logger, CancellationToken.None);
            
        var packageSearchMetaData = metadata as IPackageSearchMetadata[] ?? metadata.ToArray();
        var versions = packageSearchMetaData.Select(m => m.Identity.Version)
            .OrderByDescending(v => v);
        var targetFrameworkAttribute = Assembly.GetExecutingAssembly()
            .GetCustomAttributes(typeof(TargetFrameworkAttribute), false)
            .SingleOrDefault() as TargetFrameworkAttribute;
        var nuGetFramework = NuGetFramework.ParseFrameworkName(targetFrameworkAttribute?.FrameworkName ?? throw new InvalidOperationException(),
            new DefaultFrameworkNameProvider());

        NuGetVersion packageVersion = null!;

        foreach (var version in versions)
        {
            var package = packageSearchMetaData.First(m => m.Identity.Version == version);
            var frameworks = package.DependencySets.Select(group => group.TargetFramework).ToList();
            if (!frameworks.Exists(framework => framework.Equals(nuGetFramework))) continue;
            packageVersion = new NuGetVersion(version);
            break;
        }
            
        await CreateSource(sqlDriverName, 
            packageVersion ?? throw new InvalidOperationException(), 
            nuGetFramework, 
            sqlDriverName,
            configuration, 
            builder);
    }
        
    private static async Task CreateSource(string packageId, NuGetVersion version, 
        NuGetFramework nuGetFramework, string sqlDriverName, IConfiguration configuration, WebApplicationBuilder builder)
    {
        var setting = Settings.LoadDefaultSettings(root: null);
        var packageSourceProvider = new PackageSourceProvider(setting);

        var sourceProvider = new SourceRepositoryProvider(packageSourceProvider, Repository.Provider.GetCoreV3());

        var packagePathResolver = new PackagePathResolver(Path.GetFullPath(DriverFolderName));
        var packageExtractionContext = new PackageExtractionContext(PackageSaveMode.Defaultv3,
            XmlDocFileSaveMode.None, 
            ClientPolicyContext.GetClientPolicy(setting, Logger), 
            Logger);

        var repositories = sourceProvider.GetRepositories();
        var availablePackages = new HashSet<SourcePackageDependencyInfo>(PackageIdentityComparer.Default);

        await GetPackageDependencies(new PackageIdentity(packageId, version), 
            nuGetFramework, CacheContext, Logger, repositories.ToList(), availablePackages);

        var resolverContext = new PackageResolverContext(
            DependencyBehavior.Lowest,
            new[] { packageId },
            Enumerable.Empty<string>(),
            Enumerable.Empty<PackageReference>(), 
            Enumerable.Empty<PackageIdentity>(), 
            availablePackages, 
            sourceProvider.GetRepositories().Select(s => s.PackageSource),
            Logger);

        var resolver = new PackageResolver();
        var packagesToInstall = resolver.Resolve(resolverContext, CancellationToken.None)
            .Select(packageIdentity => availablePackages.Single(availablePackage => PackageIdentityComparer.Default.Equals(availablePackage, packageIdentity)));
        var frameworkReducer = new FrameworkReducer();

        var dlls = new List<string>();
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            return Assembly.Load(File.ReadAllBytes(dlls.Find(dllPath =>
                dllPath.EndsWith(args.Name.Split(Comma)[0] + DriverFileExtension)) ?? throw new InvalidOperationException()));
        };
            
        foreach (var packageToInstall in packagesToInstall)
        {
            PackageReaderBase packageReader;
            var installedPath = packagePathResolver.GetInstalledPath(packageToInstall);
            if (installedPath == null)
            {
                var downloadResource = await 
                    packageToInstall.Source.GetResourceAsync<DownloadResource>(CancellationToken.None);
                    
                var downloadResult = await downloadResource.GetDownloadResourceResultAsync(
                    packageToInstall, 
                    new PackageDownloadContext(new SourceCacheContext()),
                    SettingsUtility.GetGlobalPackagesFolder(setting),
                    NullLogger.Instance, 
                    CancellationToken.None);
                await PackageExtractor.ExtractPackageAsync(
                    downloadResult.PackageSource,
                    downloadResult.PackageReader,
                    downloadResult.PackageStream,
                    packagePathResolver,
                    packageExtractionContext,
                    CancellationToken.None);
                    
                packageReader = downloadResult.PackageReader;
                installedPath = packagePathResolver.GetInstalledPath(packageToInstall);
            }
            else
            {
                packageReader = new PackageFolderReader(installedPath);
            }
                
            var frameworkSpecificGroups = packageReader.GetLibItems().ToList();
            var nearest = frameworkReducer.GetNearest(
                nuGetFramework, 
                frameworkSpecificGroups.Select(frameworkSpecificGroup => frameworkSpecificGroup.TargetFramework));
                
            var dll = frameworkSpecificGroups
                .Where(specificGroup => specificGroup.TargetFramework.Equals(nearest))
                .SelectMany(specificGroup => specificGroup.Items)
                .Where(name => name.EndsWith(DriverFileExtension));
                
            var dllPath = new StringBuilder(installedPath).Append(BackSlash).Append(dll.FirstOrDefault())
                .Replace(ForwardSlash.ToString(), BackSlash.ToString()).ToString();
            dlls.Add(dllPath);
                
            if (Path.GetFileName(dllPath).StartsWith(sqlDriverName))
            {
                    
                var a = Assembly.Load(await File.ReadAllBytesAsync(dllPath));
                var connectionType = a.GetType(DriverDictionary[sqlDriverName]);
                builder.Services.AddTransient(typeof(IDbConnection), 
                    _ => Activator.CreateInstance(connectionType ?? throw new InvalidOperationException(),
                        configuration.GetValue<string>("connectionString")) ?? throw new InvalidOperationException());
            }
            else
            {
                Assembly.Load(await File.ReadAllBytesAsync(dllPath));
            }
        }
    }

    private static async Task GetPackageDependencies(PackageIdentity packageIdentity, 
        NuGetFramework framework, 
        SourceCacheContext cache,
        ILogger logger,
        List<SourceRepository> repositories, 
        ISet<SourcePackageDependencyInfo> availablePackages)
    {
        if (availablePackages.Contains(packageIdentity)) return;

        foreach (var sourceRepository in repositories)
        {
            var dependencyInfoResource = await sourceRepository.GetResourceAsync<DependencyInfoResource>();
            var dependencyInfo = await dependencyInfoResource.ResolvePackage(packageIdentity, framework, cache, logger, CancellationToken.None);
                
            if (dependencyInfo == null) continue;

            availablePackages.Add(dependencyInfo);

            foreach (var dependency in dependencyInfo.Dependencies)
            {
                await GetPackageDependencies(new PackageIdentity(dependency.Id, dependency.VersionRange.MinVersion),
                    framework, cache, logger, repositories, availablePackages);
            }
        }
            
    } 
}