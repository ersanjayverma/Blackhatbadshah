namespace backend.Services;

/// <summary>
/// Stores the current user's access token in async context for background workers
/// </summary>
public class TokenContextService
{
    private static readonly AsyncLocal<string?> _currentToken = new();

    public static string? CurrentToken
    {
        get => _currentToken.Value;
        set => _currentToken.Value = value;
    }
}
