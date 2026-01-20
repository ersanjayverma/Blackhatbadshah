namespace backend.Application.Common.Interfaces;

/// <summary>
/// Abstraction for file storage operations following Open/Closed principle.
/// Implementations can be local file system, Azure Blob, S3, etc.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Saves content to storage and returns the storage path.
    /// </summary>
    Task<string> SaveAsync(string directory, string fileName, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves content to storage and returns the storage path.
    /// </summary>
    Task<string> SaveAsync(string directory, string fileName, Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads content from storage.
    /// </summary>
    Task<string> ReadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads content as stream from storage.
    /// </summary>
    Task<Stream> ReadStreamAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes file from storage.
    /// </summary>
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if file exists.
    /// </summary>
    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures directory exists.
    /// </summary>
    Task EnsureDirectoryAsync(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the full path for a relative path.
    /// </summary>
    string GetFullPath(string relativePath);
}
