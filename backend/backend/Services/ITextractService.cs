namespace backend.Services;
public interface ITextractService
{
     Task<string> ExtractTextAsync(IFormFile file);
}
