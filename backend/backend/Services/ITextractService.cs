namespace backend.Services;
public interface ITextractService
{
     Task<string> ExtractTextAsync(IFormFile file);
     Task<string> ExtractTextFromStreamAsync(Stream stream, string fileName);
}
