using Azure.Storage.Blobs;
using aseeMIS.Services.Interfaces;

namespace aseeMIS.Services;

public sealed class SecureFileStorage(
    IConfiguration configuration,
    IWebHostEnvironment environment) : ISecureFileStorage
{
    private readonly string _provider = configuration["Storage:Provider"]?.Trim() ?? "Local";

    public async Task<byte[]?> ReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var normalizedPath = NormalizeRelativePath(relativePath);
        if (IsAzureBlob())
        {
            var blob = await FindExistingBlobAsync(normalizedPath, cancellationToken);
            if (blob == null)
                return null;

            return (await blob.DownloadContentAsync(cancellationToken)).Value.Content.ToArray();
        }

        var fullPath = GetLocalPath(normalizedPath);
        return File.Exists(fullPath)
            ? await File.ReadAllBytesAsync(fullPath, cancellationToken)
            : null;
    }

    public async Task WriteAsync(string relativePath, byte[] content, CancellationToken cancellationToken = default)
    {
        var normalizedPath = NormalizeRelativePath(relativePath);
        if (IsAzureBlob())
        {
            var container = GetContainerClient();
            await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            await container.GetBlobClient(GetBlobName(normalizedPath))
                .UploadAsync(
                    BinaryData.FromBytes(content),
                    overwrite: true,
                    cancellationToken: cancellationToken);
            return;
        }

        var fullPath = GetLocalPath(normalizedPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);
    }

    public async Task ArchiveIfExistsAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var normalizedPath = NormalizeRelativePath(relativePath);
        var archivedPath = BuildArchivedPath(normalizedPath);

        if (IsAzureBlob())
        {
            var container = GetContainerClient();
            var source = await FindExistingBlobAsync(normalizedPath, cancellationToken);
            if (source == null)
                return;

            var archived = container.GetBlobClient(BuildArchivedPath(source.Name));
            var content = await source.DownloadContentAsync(cancellationToken);
            await archived.UploadAsync(
                content.Value.Content,
                overwrite: false,
                cancellationToken: cancellationToken);
            await source.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            return;
        }

        var sourcePath = GetLocalPath(normalizedPath);
        if (File.Exists(sourcePath))
            File.Move(sourcePath, GetLocalPath(archivedPath));
    }

    private bool IsAzureBlob()
    {
        if (_provider.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase))
            return true;
        if (_provider.Equals("Local", StringComparison.OrdinalIgnoreCase))
            return false;

        throw new InvalidOperationException(
            $"Unsupported Storage:Provider '{_provider}'. Use 'Local' or 'AzureBlob'.");
    }

    private BlobContainerClient GetContainerClient()
    {
        var connectionString = configuration["Storage:BlobConnectionString"];
        var containerName = configuration["Storage:ContainerName"];

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Storage:BlobConnectionString is required when Storage:Provider is AzureBlob.");
        if (string.IsNullOrWhiteSpace(containerName))
            throw new InvalidOperationException(
                "Storage:ContainerName is required when Storage:Provider is AzureBlob.");

        return new BlobContainerClient(connectionString, containerName);
    }

    private async Task<BlobClient?> FindExistingBlobAsync(
        string normalizedPath,
        CancellationToken cancellationToken)
    {
        var container = GetContainerClient();
        foreach (var blobName in GetReadBlobNameCandidates(normalizedPath))
        {
            var blob = container.GetBlobClient(blobName);
            if (await blob.ExistsAsync(cancellationToken))
                return blob;
        }

        return null;
    }

    private IEnumerable<string> GetReadBlobNameCandidates(string normalizedPath)
    {
        if (HasKnownEnvironmentPrefix(normalizedPath))
        {
            yield return normalizedPath;
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in new[]
                 {
                     GetBlobName(normalizedPath),
                     $"Prod/{normalizedPath}",
                     $"Dev/{normalizedPath}",
                     normalizedPath
                 })
        {
            if (seen.Add(candidate))
                yield return candidate;
        }
    }

    private string GetBlobName(string normalizedPath)
    {
        if (HasKnownEnvironmentPrefix(normalizedPath))
            return normalizedPath;

        var environmentFolder = ResolveEnvironmentFolder();
        if (StartsWithFolder(normalizedPath, environmentFolder))
            return normalizedPath;

        return $"{environmentFolder}/{normalizedPath}";
    }

    private string ResolveEnvironmentFolder()
    {
        var configuredFolder = configuration["Storage:EnvironmentFolder"];
        return string.IsNullOrWhiteSpace(configuredFolder)
            ? environment.IsProduction() ? "Prod" : "Dev"
            : configuredFolder.Trim().Trim('/', '\\');
    }

    private static bool HasKnownEnvironmentPrefix(string normalizedPath)
        => StartsWithFolder(normalizedPath, "Prod")
           || StartsWithFolder(normalizedPath, "Dev");

    private static bool StartsWithFolder(string normalizedPath, string folder)
        => !string.IsNullOrWhiteSpace(folder)
           && normalizedPath.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase);

    private string GetLocalPath(string normalizedPath)
    {
        var basePath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "SecureStorage"));
        var fullPath = Path.GetFullPath(
            Path.Combine(basePath, normalizedPath.Replace('/', Path.DirectorySeparatorChar)));

        if (!fullPath.StartsWith(basePath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid secure-storage path.");

        return fullPath;
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("File path is required.", nameof(relativePath));

        var segments = relativePath.Trim().TrimStart('/', '\\').Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
            throw new InvalidOperationException("Invalid secure-storage path.");

        return string.Join('/', segments);
    }

    private static string BuildArchivedPath(string relativePath)
    {
        var directory = Path.GetDirectoryName(relativePath)?.Replace('\\', '/');
        var archivedName =
            $"{Path.GetFileNameWithoutExtension(relativePath)}_archived_{DateTime.UtcNow:yyyyMMddHHmmssfff}{Path.GetExtension(relativePath)}";

        return string.IsNullOrWhiteSpace(directory) ? archivedName : $"{directory}/{archivedName}";
    }
}
