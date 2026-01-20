namespace backend.Services;

using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Textract;
using Amazon.Textract.Model;
using Microsoft.AspNetCore.Http;

public sealed class TextractService : ITextractService
{
    private readonly IAmazonS3 _s3;
    private readonly AmazonTextractClient _textract;
    private readonly string _bucket;

    public TextractService(IConfiguration config)
    {
        var region = RegionEndpoint.GetBySystemName(
            config["AWS:Region"] ?? throw new InvalidOperationException("AWS:Region missing")
        );

        _bucket = config["AWS:TextractBucket"]
            ?? throw new InvalidOperationException("AWS:TextractBucket missing");

        _s3 = new AmazonS3Client(region);
        _textract = new AmazonTextractClient(region);
    }

    public async Task<string> ExtractTextAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is empty");

        await using var stream = file.OpenReadStream();
        return await ExtractTextFromStreamAsync(stream, file.FileName);
    }

    public async Task<string> ExtractTextFromStreamAsync(Stream stream, string fileName)
    {
        if (stream == null || stream.Length == 0)
            throw new ArgumentException("Stream is empty");

        var s3Key = $"textract-temp/{Guid.NewGuid()}{Path.GetExtension(fileName)}";

        try
        {
            // 1. Upload file to S3 (temporary)
            await _s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucket,
                Key = s3Key,
                InputStream = stream
            });

            // 2. Call Textract
            var response = await _textract.DetectDocumentTextAsync(
                new DetectDocumentTextRequest
                {
                    Document = new Document
                    {
                        S3Object = new Amazon.Textract.Model.S3Object
                        {
                            Bucket = _bucket,
                            Name = s3Key
                        }
                    }
                });

            // 3. Extract LINE blocks
            var lines = response.Blocks
                .Where(b => b.BlockType == BlockType.LINE)
                .Select(b => b.Text)
                .Where(t => !string.IsNullOrWhiteSpace(t));

            return string.Join(Environment.NewLine, lines);
        }
        finally
        {
            // 4. Always cleanup temp object
            await _s3.DeleteObjectAsync(_bucket, s3Key);
        }
    }
}
