namespace aseeMIS.Services.Interfaces;

public interface ISecureFileStorage
{
    Task<byte[]?> ReadAsync(string relativePath, CancellationToken cancellationToken = default);
    Task WriteAsync(string relativePath, byte[] content, CancellationToken cancellationToken = default);
    Task ArchiveIfExistsAsync(string relativePath, CancellationToken cancellationToken = default);
}
