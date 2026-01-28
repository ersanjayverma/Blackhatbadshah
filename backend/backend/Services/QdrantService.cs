using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using backend.Configuration;
using Microsoft.Extensions.Options;

namespace backend.Services;

public class QdrantService : IQdrantService
{
    private readonly HttpClient _httpClient;
    private readonly QdrantConfiguration _config;
    private readonly ILogger<QdrantService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _collectionInitialized;

    public QdrantService(
        HttpClient httpClient,
        IOptions<QdrantConfiguration> config,
        ILogger<QdrantService> logger)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // Set up HTTP Basic Auth (as per: curl -u qdrantuser:$QDRANT_PASS)
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_config.Username}:{_config.Password}"));
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        if (_collectionInitialized)
            return;

        try
        {
            // Check if collection exists
            var checkResponse = await _httpClient.GetAsync(
                $"{_config.Url}/collections/{_config.CollectionName}",
                cancellationToken);

            if (checkResponse.IsSuccessStatusCode)
            {
                _collectionInitialized = true;
                _logger.LogInformation("Qdrant collection '{Collection}' already exists", _config.CollectionName);
                return;
            }

            // Create collection with vector configuration
            var createRequest = new
            {
                vectors = new
                {
                    size = _config.VectorSize,
                    distance = "Cosine"
                }
            };

            var createResponse = await _httpClient.PutAsJsonAsync(
                $"{_config.Url}/collections/{_config.CollectionName}",
                createRequest,
                _jsonOptions,
                cancellationToken);

            if (createResponse.IsSuccessStatusCode)
            {
                _logger.LogInformation("Created Qdrant collection '{Collection}'", _config.CollectionName);

                // Create payload indexes for filtering
                await CreatePayloadIndexAsync("user_id", "keyword", cancellationToken);
                await CreatePayloadIndexAsync("system_id", "keyword", cancellationToken);
                await CreatePayloadIndexAsync("report_id", "keyword", cancellationToken);

                _collectionInitialized = true;
            }
            else
            {
                var error = await createResponse.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to create Qdrant collection: {Error}", error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring Qdrant collection exists");
        }
    }

    private async Task CreatePayloadIndexAsync(string fieldName, string fieldType, CancellationToken cancellationToken)
    {
        try
        {
            var indexRequest = new
            {
                field_name = fieldName,
                field_schema = fieldType
            };

            await _httpClient.PutAsJsonAsync(
                $"{_config.Url}/collections/{_config.CollectionName}/index",
                indexRequest,
                _jsonOptions,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create index for field {Field}", fieldName);
        }
    }

    public async Task<bool> StoreAnalysisAsync(
        Guid reportId,
        string userId,
        string systemId,
        string analysisText,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureCollectionExistsAsync(cancellationToken);

            // Generate embedding vector from analysis text
            var vector = await GenerateEmbeddingAsync(analysisText, cancellationToken);
            if (vector == null || vector.Length == 0)
            {
                _logger.LogWarning("Failed to generate embedding for report {ReportId}", reportId);
                return false;
            }

            // Prepare payload with user and system isolation
            var payload = new Dictionary<string, object>
            {
                ["report_id"] = reportId.ToString(),
                ["user_id"] = userId,
                ["system_id"] = systemId,
                ["summary"] = analysisText.Length > 1000 ? analysisText[..1000] : analysisText,
                ["created_at_utc"] = DateTime.UtcNow.ToString("O")
            };

            // Merge additional metadata
            if (metadata != null)
            {
                foreach (var kvp in metadata)
                {
                    payload[kvp.Key] = kvp.Value;
                }
            }

            // Upsert point to Qdrant
            var upsertRequest = new
            {
                points = new[]
                {
                    new
                    {
                        id = reportId.ToString(),
                        vector = vector,
                        payload = payload
                    }
                }
            };

            var response = await _httpClient.PutAsJsonAsync(
                $"{_config.Url}/collections/{_config.CollectionName}/points?wait=true",
                upsertRequest,
                _jsonOptions,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Stored analysis vector for ReportId {ReportId}, UserId {UserId}, SystemId {SystemId}",
                    reportId, userId, systemId);
                return true;
            }

            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to store analysis in Qdrant: {Error}", error);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing analysis in Qdrant for ReportId {ReportId}", reportId);
            return false;
        }
    }

    public async Task<List<SimilarAnalysisResult>> SearchSimilarAsync(
        string userId,
        string queryText,
        string? systemId = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<SimilarAnalysisResult>();

        try
        {
            await EnsureCollectionExistsAsync(cancellationToken);

            var vector = await GenerateEmbeddingAsync(queryText, cancellationToken);
            if (vector == null || vector.Length == 0)
            {
                _logger.LogWarning("Failed to generate embedding for search query");
                return results;
            }

            // Build filter for user isolation (and optionally system)
            var mustFilters = new List<object>
            {
                new { key = "user_id", match = new { value = userId } }
            };

            if (!string.IsNullOrEmpty(systemId))
            {
                mustFilters.Add(new { key = "system_id", match = new { value = systemId } });
            }

            var searchRequest = new
            {
                vector = vector,
                filter = new { must = mustFilters },
                limit = limit ?? _config.SearchLimit,
                score_threshold = _config.ScoreThreshold,
                with_payload = true
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_config.Url}/collections/{_config.CollectionName}/points/search",
                searchRequest,
                _jsonOptions,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Qdrant search failed: {Error}", error);
                return results;
            }

            var searchResponse = await response.Content.ReadFromJsonAsync<QdrantSearchResponse>(
                _jsonOptions, cancellationToken);

            if (searchResponse?.Result == null)
                return results;

            foreach (var hit in searchResponse.Result)
            {
                var result = new SimilarAnalysisResult
                {
                    Score = hit.Score,
                    Metadata = new Dictionary<string, object>()
                };

                if (hit.Payload != null)
                {
                    if (hit.Payload.TryGetValue("report_id", out var reportIdObj))
                        result.ReportId = Guid.TryParse(reportIdObj?.ToString(), out var rid) ? rid : Guid.Empty;

                    if (hit.Payload.TryGetValue("user_id", out var userIdObj))
                        result.UserId = userIdObj?.ToString() ?? string.Empty;

                    if (hit.Payload.TryGetValue("system_id", out var sysIdObj))
                        result.SystemId = sysIdObj?.ToString() ?? string.Empty;

                    if (hit.Payload.TryGetValue("summary", out var summaryObj))
                        result.Summary = summaryObj?.ToString() ?? string.Empty;

                    if (hit.Payload.TryGetValue("file_name", out var fileNameObj))
                        result.FileName = fileNameObj?.ToString();

                    if (hit.Payload.TryGetValue("model", out var modelObj))
                        result.Model = modelObj?.ToString();

                    if (hit.Payload.TryGetValue("created_at_utc", out var createdObj) &&
                        DateTime.TryParse(createdObj?.ToString(), out var created))
                        result.CreatedAtUtc = created;

                    result.Metadata = hit.Payload;
                }

                results.Add(result);
            }

            _logger.LogInformation(
                "Found {Count} similar analyses for UserId {UserId}, SystemId {SystemId}",
                results.Count, userId, systemId ?? "all");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching similar analyses");
        }

        return results;
    }

    public async Task<List<SimilarAnalysisResult>> GetHistoricalContextAsync(
        string userId,
        string systemId,
        string logContent,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        // Use the first portion of log content for searching similar historical logs
        var searchText = logContent.Length > 2000 ? logContent[..2000] : logContent;
        return await SearchSimilarAsync(userId, searchText, systemId, limit, cancellationToken);
    }

    public async Task<bool> DeleteAnalysisAsync(Guid reportId, CancellationToken cancellationToken = default)
    {
        try
        {
            var deleteRequest = new
            {
                points = new[] { reportId.ToString() }
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_config.Url}/collections/{_config.CollectionName}/points/delete?wait=true",
                deleteRequest,
                _jsonOptions,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Deleted analysis vector for ReportId {ReportId}", reportId);
                return true;
            }

            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Failed to delete analysis from Qdrant: {Error}", error);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting analysis from Qdrant for ReportId {ReportId}", reportId);
            return false;
        }
    }

    public async Task<bool> DeleteUserDataAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var deleteRequest = new
            {
                filter = new
                {
                    must = new[]
                    {
                        new { key = "user_id", match = new { value = userId } }
                    }
                }
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_config.Url}/collections/{_config.CollectionName}/points/delete?wait=true",
                deleteRequest,
                _jsonOptions,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Deleted all analysis vectors for UserId {UserId}", userId);
                return true;
            }

            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Failed to delete user data from Qdrant: {Error}", error);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user data from Qdrant for UserId {UserId}", userId);
            return false;
        }
    }

    /// <summary>
    /// Generates embedding vector from text using the AI service.
    /// </summary>
    private async Task<float[]?> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            // Call the AI service to generate embeddings
            // This assumes your ai.blackhatbadshah.com has an /embed endpoint
            var embeddingRequest = new
            {
                text = text.Length > 8000 ? text[..8000] : text // Limit text length
            };

            var response = await _httpClient.PostAsJsonAsync(
                "https://ai.blackhatbadshah.com/embed",
                embeddingRequest,
                _jsonOptions,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Fallback: Generate a simple hash-based pseudo-embedding
                // This is NOT ideal for semantic search but allows the system to function
                _logger.LogWarning("AI embedding service unavailable, using fallback embedding");
                return GenerateFallbackEmbedding(text);
            }

            var embeddingResponse = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(
                _jsonOptions, cancellationToken);

            return embeddingResponse?.Embedding;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate embedding, using fallback");
            return GenerateFallbackEmbedding(text);
        }
    }

    /// <summary>
    /// Fallback embedding generation using deterministic hashing.
    /// Not as good as AI embeddings but allows basic functionality.
    /// </summary>
    private float[] GenerateFallbackEmbedding(string text)
    {
        var embedding = new float[_config.VectorSize];
        var words = text.ToLowerInvariant()
            .Split(new[] { ' ', '\n', '\r', '\t', '.', ',', ':', ';', '!', '?' },
                   StringSplitOptions.RemoveEmptyEntries);

        // Simple bag-of-words style embedding with hash distribution
        foreach (var word in words)
        {
            var hash = word.GetHashCode();
            var index = Math.Abs(hash) % _config.VectorSize;
            embedding[index] += 1.0f;
        }

        // Normalize the vector
        var magnitude = (float)Math.Sqrt(embedding.Sum(x => x * x));
        if (magnitude > 0)
        {
            for (int i = 0; i < embedding.Length; i++)
            {
                embedding[i] /= magnitude;
            }
        }

        return embedding;
    }
}

// Response DTOs for Qdrant API
internal class QdrantSearchResponse
{
    public List<QdrantSearchHit>? Result { get; set; }
}

internal class QdrantSearchHit
{
    public string? Id { get; set; }
    public float Score { get; set; }
    public Dictionary<string, object>? Payload { get; set; }
}

internal class EmbeddingResponse
{
    public float[]? Embedding { get; set; }
}
