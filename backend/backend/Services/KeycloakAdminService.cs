using System.Net.Http.Headers;
using System.Text.Json;
using shared.Dto;

namespace backend.Services;

public class KeycloakAdminService : IKeycloakAdminService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<KeycloakAdminService> _logger;
    private readonly string _realm;
    private readonly string _adminUrl;
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public KeycloakAdminService(HttpClient httpClient, IConfiguration config, ILogger<KeycloakAdminService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
        _realm = _config["Keycloak:Realm"] ?? "blackhatbadshah";
        _adminUrl = _config["Keycloak:AdminUrl"] ?? "https://auth.blackhatbadshah.com";
    }

    private async Task EnsureAuthenticatedAsync()
    {
        if (_accessToken != null && DateTime.UtcNow < _tokenExpiry)
            return;

        var tokenEndpoint = $"{_adminUrl}/realms/master/protocol/openid-connect/token";
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", _config["Keycloak:AdminClientId"]!),
            new KeyValuePair<string, string>("client_secret", _config["Keycloak:AdminClientSecret"]!)
        });

        var response = await _httpClient.PostAsync(tokenEndpoint, content);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        _accessToken = result.GetProperty("access_token").GetString();
        var expiresIn = result.GetProperty("expires_in").GetInt32();
        _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60);

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    public async Task UpdateUserSubscriptionAttributeAsync(string userId, string planName)
    {
        try
        {
            await EnsureAuthenticatedAsync();

            var userUrl = $"{_adminUrl}/admin/realms/{_realm}/users/{userId}";

            var getResponse = await _httpClient.GetAsync(userUrl);
            getResponse.EnsureSuccessStatusCode();
            var user = await getResponse.Content.ReadFromJsonAsync<JsonElement>();

            var attributes = user.TryGetProperty("attributes", out var existingAttrs)
                ? JsonSerializer.Deserialize<Dictionary<string, List<string>>>(existingAttrs.GetRawText()) ?? new()
                : new Dictionary<string, List<string>>();

            attributes["plan"] = new List<string> { planName };

            var updatePayload = new { attributes };
            var updateResponse = await _httpClient.PutAsJsonAsync(userUrl, updatePayload);
            updateResponse.EnsureSuccessStatusCode();

            _logger.LogInformation("Updated Keycloak user {UserId} subscription_plan to {PlanName}", userId, planName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Keycloak user {UserId} subscription attribute", userId);
            throw;
        }
    }

    public async Task<string> GetUserPlanAsync(string userId)
    {
        try
        {
            await EnsureAuthenticatedAsync();

            var userUrl = $"{_adminUrl}/admin/realms/{_realm}/users/{userId}";
            var response = await _httpClient.GetAsync(userUrl);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get user {UserId} from Keycloak, defaulting to Free plan", userId);
                return PlanConstants.Plans.Free;
            }

            var user = await response.Content.ReadFromJsonAsync<JsonElement>();

            if (user.TryGetProperty("attributes", out var attrs) &&
                attrs.TryGetProperty("plan", out var planAttr) &&
                planAttr.GetArrayLength() > 0)
            {
                var plan = planAttr[0].GetString();
                if (!string.IsNullOrEmpty(plan))
                    return plan;
            }

            return PlanConstants.Plans.Free;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get plan for user {UserId}, defaulting to Free", userId);
            return PlanConstants.Plans.Free;
        }
    }

    public async Task<string?> GetUserEmailAsync(string userId)
    {
        var userInfo = await GetUserInfoAsync(userId);
        return userInfo?.Email;
    }

    public async Task<KeycloakUserInfo?> GetUserInfoAsync(string userId)
    {
        try
        {
            await EnsureAuthenticatedAsync();

            var userUrl = $"{_adminUrl}/admin/realms/{_realm}/users/{userId}";
            var response = await _httpClient.GetAsync(userUrl);

            if (!response.IsSuccessStatusCode)
                return null;

            var user = await response.Content.ReadFromJsonAsync<JsonElement>();

            return new KeycloakUserInfo
            {
                Email = user.GetProperty("email").GetString() ?? string.Empty,
                FirstName = user.TryGetProperty("firstName", out var fn) ? fn.GetString() : null,
                LastName = user.TryGetProperty("lastName", out var ln) ? ln.GetString() : null,
                Phone = user.TryGetProperty("attributes", out var attrs) &&
                        attrs.TryGetProperty("phone", out var phone) &&
                        phone.GetArrayLength() > 0
                            ? phone[0].GetString()
                            : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Keycloak user info for {UserId}", userId);
            return null;
        }
    }
}
