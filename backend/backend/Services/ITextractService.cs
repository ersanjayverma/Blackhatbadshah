namespace backend.Services;
public interface ITextractService
{
    Task<string> ExtractTextAsync(
        string containerName,
        string blobPath
    );
     Task<string> ExtractTextAsync(IFormFile file);
}
