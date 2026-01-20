using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using shared.Dto;

namespace backend.Services;

public sealed class KeycloakAdminService : IKeycloakAdminService
{
    private readonly HttpClient _http;
    private readonly ILogger<KeycloakAdminService> _logger;

    private readonly string _realm;
    private readonly string _clientId;
    private readonly string _clientSecret;

    private static string? _accessToken;
    private static DateTime _expiresAtUtc;
    private static readonly SemaphoreSlim _tokenLock = new(1, 1);

    public KeycloakAdminService(
        HttpClient http,
        IConfiguration config,
        ILogger<KeycloakAdminService> logger)
    {
        _http = http;
        _logger = logger;

        _realm = config["Keycloak:Realm"]
                 ?? throw new InvalidOperationException("Keycloak:Realm missing");

        _clientId = config["Keycloak:AdminClientId"]
                    ?? throw new InvalidOperationException("Keycloak:AdminClientId missing");

        _clientSecret = config["Keycloak:AdminClientSecret"]
                        ?? throw new InvalidOperationException("Keycloak:AdminClientSecret missing");
    }

    // ---------------- TOKEN ----------------

    private async Task<string> GetAccessTokenAsync()
    {
        if (_accessToken != null && DateTime.UtcNow < _expiresAtUtc)
            return _accessToken;

        await _tokenLock.WaitAsync();
        try
        {
            if (_accessToken != null && DateTime.UtcNow < _expiresAtUtc)
                return _accessToken;

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"realms/{_realm}/protocol/openid-connect/token");

            request.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("grant_type", "client_credentials"),
                new KeyValuePair<string,string>("client_id", _clientId),
                new KeyValuePair<string,string>("client_secret", _clientSecret)
            });

            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Keycloak token request failed: {Status} - {Body}",
                    response.StatusCode,
                    errorBody);
                throw new HttpRequestException(
                    $"Keycloak token request failed: {response.StatusCode}");
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            _accessToken = doc.RootElement.GetProperty("access_token").GetString();
            var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();

            _expiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn - 60);

            return _accessToken!;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task<HttpRequestMessage> CreateAdminRequestAsync(
        HttpMethod method,
        string relativeUrl)
    {
        var token = await GetAccessTokenAsync();

        var req = new HttpRequestMessage(method, relativeUrl);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return req;
    }

    // ---------------- PLAN READ ----------------

    public async Task<string> GetUserPlanAsync(string userId)
    {
        try
        {
            using var req = await CreateAdminRequestAsync(
                HttpMethod.Get,
                $"admin/realms/{_realm}/users/{userId}");

            using var res = await _http.SendAsync(req);
            res.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());

            if (doc.RootElement.TryGetProperty("attributes", out var attrs) &&
                attrs.TryGetProperty("plan", out var planArr) &&
                planArr.ValueKind == JsonValueKind.Array &&
                planArr.GetArrayLength() > 0)
            {
                var plan = planArr[0].GetString();
                if (!string.IsNullOrWhiteSpace(plan))
                    return plan;
            }

            return PlanConstants.Plans.Free;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to read Keycloak plan for user {UserId}",
                userId);

            return PlanConstants.Plans.Free;
        }
    }

    // ---------------- PLAN UPDATE ----------------

    public async Task UpdateUserSubscriptionAttributeAsync(
        string userId,
        string planName)
    {
        using var getReq = await CreateAdminRequestAsync(
            HttpMethod.Get,
            $"admin/realms/{_realm}/users/{userId}");

        using var getRes = await _http.SendAsync(getReq);
        getRes.EnsureSuccessStatusCode();

        using var userDoc =
            JsonDocument.Parse(await getRes.Content.ReadAsStringAsync());

        var attributes =
            userDoc.RootElement.TryGetProperty("attributes", out var attrs)
                ? JsonSerializer.Deserialize<Dictionary<string, List<string>>>(
                      attrs.GetRawText()) ?? new()
                : new();

        attributes["plan"] = new List<string> { planName };

        var payload = JsonSerializer.Serialize(new { attributes });

        using var putReq = await CreateAdminRequestAsync(
            HttpMethod.Put,
            $"admin/realms/{_realm}/users/{userId}");

        putReq.Content = new StringContent(
            payload,
            Encoding.UTF8,
            "application/json");

        using var putRes = await _http.SendAsync(putReq);
        putRes.EnsureSuccessStatusCode();
    }

    // ---------------- USER INFO ----------------

    public async Task<KeycloakUserInfo?> GetUserInfoAsync(string userId)
    {
        try
        {
            using var req = await CreateAdminRequestAsync(
                HttpMethod.Get,
                $"admin/realms/{_realm}/users/{userId}");

            using var res = await _http.SendAsync(req);
            if (!res.IsSuccessStatusCode)
                return null;

            using var doc =
                JsonDocument.Parse(await res.Content.ReadAsStringAsync());

            var root = doc.RootElement;

            return new KeycloakUserInfo
            {
                Email = root.GetProperty("email").GetString() ?? string.Empty,
                FirstName = root.TryGetProperty("firstName", out var fn)
                    ? fn.GetString()
                    : null,
                LastName = root.TryGetProperty("lastName", out var ln)
                    ? ln.GetString()
                    : null,
                Phone =
                    root.TryGetProperty("attributes", out var attrs) &&
                    attrs.TryGetProperty("phone", out var phoneArr) &&
                    phoneArr.ValueKind == JsonValueKind.Array &&
                    phoneArr.GetArrayLength() > 0
                        ? phoneArr[0].GetString()
                        : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to get Keycloak user info for {UserId}",
                userId);

            return null;
        }
    }

    public async Task<string?> GetUserEmailAsync(string userId)
    {
        var info = await GetUserInfoAsync(userId);
        return info?.Email;
    }
}
