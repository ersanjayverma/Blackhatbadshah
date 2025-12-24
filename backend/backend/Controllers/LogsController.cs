using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using backend.Data.Entities;
using backend.Data;
namespace backend.Controllers
{
    [ApiController]
    [Route("api/logs")]
    [Authorize]
    public class LogsController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly BlobServiceClient _blobService;
        private readonly AppDbContext _db;

        public LogsController(
            IConfiguration config,
            BlobServiceClient blobService,
            AppDbContext db)
        {
            _config = config;
            _blobService = blobService;
            _db = db;
        }

        // -----------------------------------------
        // POST api/logs/upload
        // -----------------------------------------
        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("File is required");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? User.FindFirstValue("sub");

            if (userId == null)
                return Unauthorized("User id missing");

            var containerName = _config["AzureBlob:Container"]
                ?? throw new InvalidOperationException("AzureBlob:Container not configured");

            var logId = Guid.NewGuid();
            var blobName = $"{userId}/{logId}.log";

            var container = _blobService.GetBlobContainerClient(containerName);
            await container.CreateIfNotExistsAsync();

            var blob = container.GetBlobClient(blobName);

            await using (var stream = file.OpenReadStream())
            {
                await blob.UploadAsync(stream, overwrite: true);
            }

            var entity = new Log    
            {
                Id = logId,
                UserId = userId,
                FileName = file.FileName,
                ContentType = file.ContentType,
                SizeBytes = file.Length,
                StoragePath = blobName,
                CreatedAt = DateTime.UtcNow
            };

            _db.Logs.Add(entity);
            await _db.SaveChangesAsync();

            return Ok(new { logId });
        }

        // -----------------------------------------
        // GET api/logs/{id}
        // (secure fetch, owner only)
        // -----------------------------------------
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? User.FindFirstValue("sub");

            var log = await _db.Logs.FindAsync(id);
            if (log == null)
                return NotFound();

            if (log.UserId != userId)
                return Forbid();

            return Ok(new
            {
                log.Id,
                log.FileName,
                log.SizeBytes,
                log.CreatedAt
            });
        }

        // -----------------------------------------
        // GET api/logs/{id}/content
        // (used later by analyzer tool)
        // -----------------------------------------
        [HttpGet("{id:guid}/content")]
        public async Task<IActionResult> GetContent(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? User.FindFirstValue("sub");

            var log = await _db.Logs.FindAsync(id);
            if (log == null)
                return NotFound();

            if (log.UserId != userId)
                return Forbid();

            var containerName = _config["AzureBlob:Container"]!;
            var container = _blobService.GetBlobContainerClient(containerName);
            var blob = container.GetBlobClient(log.StoragePath);

            if (!await blob.ExistsAsync())
                return NotFound("Blob missing");

            var download = await blob.DownloadStreamingAsync();
            return File(download.Value.Content, log.ContentType ?? "text/plain");
        }
    }
}
