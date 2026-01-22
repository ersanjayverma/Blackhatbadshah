using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace backend.Services;

/// <summary>
/// Service to validate JWT tokens and extract user claims
/// </summary>
public interface ITokenValidationService
{
    /// <summary>
    /// Extracts the user ID (sub claim) from a JWT token
    /// </summary>
    string? ExtractUserId(string accessToken);
}

public class TokenValidationService : ITokenValidationService
{
    private readonly ILogger<TokenValidationService> _logger;

    public TokenValidationService(ILogger<TokenValidationService> logger)
    {
        _logger = logger;
    }

    public string? ExtractUserId(string accessToken)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();

            // Remove "Bearer " prefix if present
            var token = accessToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? accessToken["Bearer ".Length..].Trim()
                : accessToken.Trim();

            // Check if token can be read
            if (!handler.CanReadToken(token))
            {
                _logger.LogWarning("Invalid JWT token format");
                return null;
            }

            // Read token without validation (we already validated it at the API gateway)
            var jwtToken = handler.ReadJwtToken(token);

            // Extract user ID from 'sub' claim
            var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value
                      ?? jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("No user ID found in token claims");
                return null;
            }

            return userId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting user ID from token");
            return null;
        }
    }
}
