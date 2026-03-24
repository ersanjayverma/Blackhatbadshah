using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace backend.Controllers;

/// <summary>
/// Base API controller with common functionality shared across all controllers
/// </summary>
[ApiController]
public abstract class BaseApiController : ControllerBase
{
    /// <summary>
    /// Gets the current user's ID from the JWT token claims.
    /// Checks both NameIdentifier and "sub" claims for compatibility with different auth providers.
    /// </summary>
    /// <returns>The user ID or null if not authenticated</returns>
    protected string? GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        User.FindFirstValue("sub");

    /// <summary>
    /// Gets the current user's ID and returns Unauthorized if not found.
    /// </summary>
    /// <param name="userId">The user ID if found</param>
    /// <returns>True if user ID was found, false otherwise</returns>
    protected bool TryGetUserId(out string userId)
    {
        userId = GetUserId() ?? string.Empty;
        return !string.IsNullOrEmpty(userId);
    }

    /// <summary>
    /// Gets the bearer token from the Authorization header.
    /// </summary>
    /// <returns>The bearer token or null if not present</returns>
    protected string? GetBearerToken()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        return authHeader.StartsWith("Bearer ") ? authHeader[7..] : null;
    }

    /// <summary>
    /// Creates a standard unauthorized response with error message.
    /// </summary>
    protected IActionResult UnauthorizedWithError(string message = "Unable to determine user identity")
        => Unauthorized(new { error = message });

    /// <summary>
    /// Creates a standard not found response with error message.
    /// </summary>
    protected IActionResult NotFoundWithError(string message)
        => NotFound(new { error = message });

    /// <summary>
    /// Creates a standard bad request response with error message.
    /// </summary>
    protected IActionResult BadRequestWithError(string message)
        => BadRequest(new { error = message });

    /// <summary>
    /// Creates a standard conflict response with error message.
    /// </summary>
    protected IActionResult ConflictWithError(string message)
        => Conflict(new { error = message });

    /// <summary>
    /// Creates a payment required (402) response for plan limits.
    /// </summary>
    protected IActionResult PaymentRequired(string message)
        => StatusCode(402, new { error = message, upgradeRequired = true });
}
