namespace backend.Services;

public interface IKeycloakAdminService
{
    Task UpdateUserSubscriptionAttributeAsync(string userId, string planName);
    Task<string> GetUserPlanAsync(string userId);
    Task<string?> GetUserEmailAsync(string userId);
    Task<KeycloakUserInfo?> GetUserInfoAsync(string userId);
}

public class KeycloakUserInfo
{
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
}
