using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

public class CustomAuthorizationMessageHandler : AuthorizationMessageHandler
{
    public CustomAuthorizationMessageHandler(IAccessTokenProvider provider, 
        NavigationManager navigation)
        : base(provider, navigation)
    {
        ConfigureHandler(
            authorizedUrls: new[] { "https://api.blackhatbadshah.com" ,"https://ai.blackhatbadshah.com"}, // The API Base URL
            scopes: new[] { "openid", "profile", "email", "offline_access" } // Must match your requested scopes
        );
    }
}