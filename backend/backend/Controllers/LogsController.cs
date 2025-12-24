using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using backend.Data.Entities;
using backend.Data;
using shared.Dto;
using backend.Services;
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
        private readonly ILogAnalyzer _analyzer;

        public LogsController(
            IConfiguration config,
            BlobServiceClient blobService,
            AppDbContext db,ILogAnalyzer analyzer)
        {
            _config = config;
            _blobService = blobService;
            _db = db;
            _analyzer=analyzer;
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

            return Ok(new LogDto
                {
                    Id = log.Id,
                    FileName = log.FileName,
                    SizeBytes = log.SizeBytes,
                    CreatedAt = log.CreatedAt
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
        [HttpGet]
        public async Task<IActionResult> List()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? User.FindFirstValue("sub");

            var logs = await _db.Logs
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new LogDto
                {
                    Id = x.Id,
                    FileName = x.FileName,
                    SizeBytes = x.SizeBytes,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();

            return Ok(logs);
        }
        // -----------------------------------------
        // DELETE api/logs/{id}
        // (owner only, deletes blob + db record)
        // -----------------------------------------
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? User.FindFirstValue("sub");

            if (userId == null)
                return Unauthorized();

            var log = await _db.Logs.FindAsync(id);
            if (log == null)
                return NotFound();

            if (log.UserId != userId)
                return Forbid();

            var containerName = _config["AzureBlob:Container"]
                ?? throw new InvalidOperationException("AzureBlob:Container not configured");

            var container = _blobService.GetBlobContainerClient(containerName);
            var blob = container.GetBlobClient(log.StoragePath);

            // Delete blob if it exists (idempotent)
            try
            {
                await blob.DeleteIfExistsAsync();
            }
            catch (Exception)
            {
                // Optional: log this, but do NOT block DB cleanup
                // _logger.LogWarning(ex, "Failed to delete blob {Path}", log.StoragePath);
            }

            _db.Logs.Remove(log);
            await _db.SaveChangesAsync();

            return NoContent();
        }
        // -----------------------------------------
// POST api/logs/Analyse/{id}/content
// Analyze log content (owner only)
// -----------------------------------------
[HttpGet("Analyze/{id:guid}")]
public async Task<IActionResult> AnalyseContent( Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? User.FindFirstValue("sub");

            if (userId == null)
                return Unauthorized();

            // 1. Fetch log metadata
            var log = await _db.Logs.FindAsync(id);
            if (log == null)
                return NotFound("Log not found");

            if (log.UserId != userId)
                return Forbid();

            // 2. Fetch blob
            var containerName = _config["AzureBlob:Container"];
            if (string.IsNullOrWhiteSpace(containerName))
                throw new InvalidOperationException("AzureBlob:Container not configured");

            var container = _blobService.GetBlobContainerClient(containerName);
            var blob = container.GetBlobClient(log.StoragePath);

            if (!await blob.ExistsAsync())
                return NotFound("Log content missing in storage");

            // 3. Stream blob content (safe)
            string content;
            await using (var stream = await blob.OpenReadAsync())
            using (var reader = new StreamReader(stream))
            {
                content = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(content))
                return BadRequest("Log content is empty");

            // 4. Analyze (deterministic analyzer)
            var analysis = await _analyzer.AnalyzeAsync(log.Id, content);

            return Ok(analysis);
        }

    }
}
