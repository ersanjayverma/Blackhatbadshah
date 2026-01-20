using System.Text;
using backend.Application.Common.Interfaces;

namespace backend.Infrastructure.Storage;

/// <summary>
/// Local file system implementation of IFileStorageService.
/// Can be swapped for Azure Blob, S3, etc. following Open/Closed principle.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;

    public LocalFileStorageService(IConfiguration configuration)
    {
        _rootPath = configuration["Storage:RootPath"]
            ?? throw new InvalidOperationException("Storage:RootPath not configured");

        // Ensure root directory exists
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(string directory, string fileName, string content, CancellationToken cancellationToken = default)
    {
        var fullDirectory = Path.Combine(_rootPath, directory);
        Directory.CreateDirectory(fullDirectory);

        var filePath = Path.Combine(fullDirectory, fileName);
        await File.WriteAllTextAsync(filePath, content, Encoding.UTF8, cancellationToken);

        return filePath;
    }

    public async Task<string> SaveAsync(string directory, string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        var fullDirectory = Path.Combine(_rootPath, directory);
        Directory.CreateDirectory(fullDirectory);

        var filePath = Path.Combine(fullDirectory, fileName);
        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        await content.CopyToAsync(fileStream, cancellationToken);

        return filePath;
    }

    public async Task<string> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"File not found: {path}");

        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    public Task<Stream> ReadStreamAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"File not found: {path}");

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(File.Exists(path));
    }

    public Task EnsureDirectoryAsync(string directory, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_rootPath, directory);
        Directory.CreateDirectory(fullPath);
        return Task.CompletedTask;
    }

    public string GetFullPath(string relativePath)
    {
        return Path.Combine(_rootPath, relativePath);
    }
}
