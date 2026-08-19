using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using SkeletonKey.Catalog;
using SkeletonKey.Catalog.Validation;
using SkeletonKey.Handlers;
using SkeletonKey.Runtime.Plugins;
using SkeletonKey.Runtime.Resources;

namespace SkeletonKey.Runner.Core.Plugins;

/// <summary>Loads closed, hash-verified plugin manifests from explicitly supplied local directories.</summary>
public static class SkeletonKeyPluginLoader
{
    private const int _maximumDirectories = 8;
    private const int _maximumManifests = 64;
    private const int _maximumManifestBytes = 64 * 1024;
    private const long _maximumAssemblyBytes = 32L * 1024 * 1024;
    private const int _maximumDefinitions = 256;
    private const int _maximumHandlers = 256;
    private const int _maximumProviders = 64;
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>Loads and validates plugins from top-level manifests in the supplied directories.</summary>
    public static async ValueTask<SkeletonKeyPluginLoadResult> LoadAsync(
        IReadOnlyList<string> directories,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(directories);
        if (directories.Count == 0)
        {
            return SkeletonKeyPluginLoadResult.Empty;
        }

        if (directories.Count > _maximumDirectories)
        {
            throw Failure("SKP2201", "At most 8 plugin directories may be supplied.");
        }

        StringComparer pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        HashSet<string> uniqueDirectories = new(pathComparer);
        List<string> manifests = [];
        foreach (string suppliedDirectory in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string directory = Path.GetFullPath(suppliedDirectory);
            if (!uniqueDirectories.Add(directory))
            {
                throw Failure("SKP2201", "A plugin directory was supplied more than once.");
            }

            DirectoryInfo directoryInfo = new(directory);
            if (!directoryInfo.Exists || HasReparsePoint(directoryInfo))
            {
                throw Failure("SKP2201", "Plugin directory does not exist or is a reparse point: " + directory + ".");
            }

            manifests.AddRange(Directory.GetFiles(directory, "*.skeletonkey-plugin.json", SearchOption.TopDirectoryOnly));
        }

        manifests.Sort(StringComparer.Ordinal);
        if (manifests.Count > _maximumManifests)
        {
            throw Failure("SKP2202", "Explicit plugin directories contain more than 64 manifests.");
        }

        List<SkeletonKeyPluginDescriptor> descriptors = [];
        List<WorkflowNodeDefinition> definitions = [];
        List<INodeHandler> handlers = [];
        List<IWorkflowRuntimeResourceProvider> providers = [];
        HashSet<string> pluginIds = new(StringComparer.Ordinal);
        HashSet<WorkflowNodeDefinitionKey> definitionKeys = [];
        HashSet<WorkflowNodeDefinitionKey> handlerKeys = [];
        HashSet<string> providerKinds = new(StringComparer.Ordinal);

        foreach (string manifestPath in manifests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileInfo manifestInfo = new(manifestPath);
            if (HasReparsePoint(manifestInfo) || manifestInfo.Length is <= 0 or > _maximumManifestBytes)
            {
                throw Failure("SKP2202", "Plugin manifest is empty, too large, or a reparse point: " + manifestInfo.Name + ".");
            }

            LocalPluginManifest manifest = await ReadManifestAsync(manifestPath, cancellationToken).ConfigureAwait(false);
            ValidateManifest(manifest, manifestInfo.Name);
            if (!pluginIds.Add(manifest.Id))
            {
                throw Failure("SKP2208", "Duplicate plugin identifier: " + manifest.Id + ".");
            }

            string assemblyPath = Path.Combine(manifestInfo.DirectoryName!, manifest.Assembly);
            FileInfo assemblyInfo = new(assemblyPath);
            if (!assemblyInfo.Exists || HasReparsePoint(assemblyInfo) || assemblyInfo.Length is <= 0 or > _maximumAssemblyBytes)
            {
                throw Failure("SKP2204", "Plugin assembly is missing, empty, too large, or a reparse point: " + manifest.Assembly + ".");
            }

            string actualHash;
            await using (FileStream stream = new(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
                actualHash = Convert.ToHexStringLower(hash);
            }

            if (!string.Equals(actualHash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw Failure("SKP2205", "Plugin assembly SHA-256 does not match its manifest: " + manifest.Assembly + ".");
            }

            ISkeletonKeyPlugin plugin = ActivatePlugin(assemblyPath, manifest);
            ValidateIdentity(plugin, manifest);
            WorkflowNodeDefinition[] pluginDefinitions = Snapshot(plugin.NodeDefinitions, _maximumDefinitions, "node definitions");
            INodeHandler[] pluginHandlers = Snapshot(plugin.NodeHandlers, _maximumHandlers, "node handlers");
            IWorkflowRuntimeResourceProvider[] pluginProviders = Snapshot(plugin.ResourceProviders, _maximumProviders, "resource providers");
            ValidateContributions(manifest.Id, pluginDefinitions, pluginHandlers, pluginProviders, definitionKeys, handlerKeys, providerKinds);
            ValidateDefinitionSemantics(pluginDefinitions);

            definitions.AddRange(pluginDefinitions);
            handlers.AddRange(pluginHandlers);
            providers.AddRange(pluginProviders);
            descriptors.Add(new SkeletonKeyPluginDescriptor(
                manifest.Id,
                manifest.Version,
                manifest.Assembly,
                manifest.EntryType,
                pluginDefinitions.Length,
                pluginHandlers.Length,
                pluginProviders.Length));
        }

        return new SkeletonKeyPluginLoadResult(descriptors, definitions, handlers, providers);
    }

    private static async ValueTask<LocalPluginManifest> ReadManifestAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            LocalPluginManifest? manifest = await JsonSerializer.DeserializeAsync<LocalPluginManifest>(stream, _jsonOptions, cancellationToken).ConfigureAwait(false);
            return manifest ?? throw Failure("SKP2202", "Plugin manifest must contain a JSON object.");
        }
        catch (SkeletonKeyPluginLoadException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new SkeletonKeyPluginLoadException("SKP2202", "Plugin manifest JSON is invalid or contains unknown properties.", exception);
        }
    }

    private static void ValidateManifest(LocalPluginManifest manifest, string manifestName)
    {
        if (!string.Equals(manifest.SchemaVersion, "0.1", StringComparison.Ordinal) ||
            !IsIdentifier(manifest.Id, 128) ||
            !IsVersion(manifest.Version) ||
            !IsAssemblyFileName(manifest.Assembly) ||
            string.IsNullOrWhiteSpace(manifest.EntryType) || manifest.EntryType.Length > 512 ||
            manifest.EntryType.Any(char.IsControl) ||
            manifest.Sha256 is null || manifest.Sha256.Length != 64 || !manifest.Sha256.All(Uri.IsHexDigit))
        {
            throw Failure("SKP2203", "Plugin manifest has an invalid field: " + manifestName + ".");
        }
    }

    private static ISkeletonKeyPlugin ActivatePlugin(string assemblyPath, LocalPluginManifest manifest)
    {
        try
        {
            string fullPath = Path.GetFullPath(assemblyPath);
            Assembly? assembly = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(candidate =>
                !candidate.IsDynamic && IsLoadedFromPath(candidate, fullPath));
            assembly ??= AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
            Type? type = assembly.GetType(manifest.EntryType, throwOnError: false, ignoreCase: false);
            if (type is null || !type.IsClass || type.IsAbstract || !type.IsPublic || !typeof(ISkeletonKeyPlugin).IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) is null)
            {
                throw Failure("SKP2206", "Plugin entry type must be a public, non-abstract implementation with a public parameterless constructor.");
            }

            return (ISkeletonKeyPlugin)(Activator.CreateInstance(type) ?? throw Failure("SKP2206", "Plugin entry type could not be created."));
        }
        catch (SkeletonKeyPluginLoadException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new SkeletonKeyPluginLoadException("SKP2206", "Plugin assembly or entry type could not be loaded.", exception);
        }
    }

    private static bool IsLoadedFromPath(Assembly assembly, string fullPath)
    {
#pragma warning disable IL3000 // External plugin assemblies loaded from disk retain a location; bundled assemblies are ignored below.
        string location = assembly.Location;
#pragma warning restore IL3000
        return location.Length > 0 && string.Equals(
            Path.GetFullPath(location),
            fullPath,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static void ValidateIdentity(ISkeletonKeyPlugin plugin, LocalPluginManifest manifest)
    {
        try
        {
            if (!string.Equals(plugin.Id, manifest.Id, StringComparison.Ordinal) || !string.Equals(plugin.Version, manifest.Version, StringComparison.Ordinal))
            {
                throw Failure("SKP2207", "Plugin implementation identity does not match its manifest.");
            }
        }
        catch (SkeletonKeyPluginLoadException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new SkeletonKeyPluginLoadException("SKP2207", "Plugin identity could not be read.", exception);
        }
    }

    private static T[] Snapshot<T>(IReadOnlyList<T> contributions, int maximum, string name)
        where T : class
    {
        if (contributions is null || contributions.Count > maximum)
        {
            throw Failure("SKP2207", "Plugin " + name + " are missing or exceed their bounded limit.");
        }

        T[] snapshot = [.. contributions];
        if (snapshot.Any(static contribution => contribution is null))
        {
            throw Failure("SKP2207", "Plugin contribution lists cannot contain null values.");
        }

        return snapshot;
    }

    private static void ValidateContributions(
        string pluginId,
        IReadOnlyList<WorkflowNodeDefinition> definitions,
        IReadOnlyList<INodeHandler> handlers,
        IReadOnlyList<IWorkflowRuntimeResourceProvider> providers,
        HashSet<WorkflowNodeDefinitionKey> allDefinitions,
        HashSet<WorkflowNodeDefinitionKey> allHandlers,
        HashSet<string> allProviderKinds)
    {
        string prefix = pluginId + ".";
        HashSet<WorkflowNodeDefinitionKey> localDefinitions = [];
        foreach (WorkflowNodeDefinition definition in definitions)
        {
            if (string.IsNullOrWhiteSpace(definition.Type) || !definition.Type.StartsWith(prefix, StringComparison.Ordinal) || definition.Version <= 0 || !localDefinitions.Add(definition.Key) || !allDefinitions.Add(definition.Key))
            {
                throw Failure("SKP2208", "Plugin node definitions must be unique, versioned, and namespaced by the plugin identifier.");
            }
        }

        HashSet<WorkflowNodeDefinitionKey> localHandlers = [];
        foreach (INodeHandler handler in handlers)
        {
            if (string.IsNullOrWhiteSpace(handler.Definition.Type) || !localHandlers.Add(handler.Definition) || !allHandlers.Add(handler.Definition))
            {
                throw Failure("SKP2209", "Plugin node handlers must have unique exact definition keys.");
            }
        }

        if (!localDefinitions.SetEquals(localHandlers))
        {
            throw Failure("SKP2209", "Every plugin node definition requires one exact handler and every handler requires one definition.");
        }

        foreach (IWorkflowRuntimeResourceProvider provider in providers)
        {
            if (string.IsNullOrWhiteSpace(provider.Kind) || !provider.Kind.StartsWith(prefix, StringComparison.Ordinal) || provider.Kind.Length > 256 || !allProviderKinds.Add(provider.Kind) ||
                provider.Capabilities is null || provider.Capabilities.Count > 64 ||
                provider.Capabilities.Distinct(StringComparer.Ordinal).Count() != provider.Capabilities.Count ||
                provider.Capabilities.Any(static capability => string.IsNullOrWhiteSpace(capability) || capability.Length > 256))
            {
                throw Failure("SKP2210", "Plugin resource providers must be unique, bounded, and namespaced by the plugin identifier.");
            }
        }
    }

    private static void ValidateDefinitionSemantics(IReadOnlyList<WorkflowNodeDefinition> definitions)
    {
        if (definitions.Count == 0)
        {
            return;
        }

        NodeCatalogDocument document = new(id: "plugin", version: "1.0.0", definitions: definitions);
        NodeCatalogValidationResult result = new NodeCatalogSemanticValidator().Validate(document);
        if (!result.IsValid)
        {
            throw Failure("SKP2208", "Plugin node definitions failed semantic catalog validation: " + result.Issues[0].Code + ".");
        }
    }

    private static bool IsIdentifier(string value, int maximum)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Length <= maximum && value.All(static character =>
            character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '_' or '-');
    }

    private static bool IsVersion(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Length <= 64 && value.All(static character =>
            character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '+' or '-');
    }

    private static bool IsAssemblyFileName(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Length <= 255 && value.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(value, Path.GetFileName(value), StringComparison.Ordinal) && !Path.IsPathRooted(value) && value.IndexOfAny(['/', '\\']) < 0;
    }

    private static bool HasReparsePoint(FileSystemInfo info)
    {
        return (info.Attributes & FileAttributes.ReparsePoint) != 0;
    }

    private static SkeletonKeyPluginLoadException Failure(string code, string message)
    {
        return new SkeletonKeyPluginLoadException(code, message);
    }

    private sealed class LocalPluginManifest
    {
        public required string SchemaVersion { get; init; }

        public required string Id { get; init; }

        public required string Version { get; init; }

        public required string Assembly { get; init; }

        public required string EntryType { get; init; }

        public required string Sha256 { get; init; }
    }
}
