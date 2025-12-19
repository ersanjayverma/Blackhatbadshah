using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using frontend;
var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");

// OIDC (Keycloak)
builder.Services.AddOidcAuthentication(options =>
{
    options.ProviderOptions.Authority =
        "https://auth.blackhatbadshah.com/realms/blackhatbadshah";

    options.ProviderOptions.ClientId = "blackhatbadshah-spa";
    options.ProviderOptions.ResponseType = "code";
    options.ProviderOptions.DefaultScopes.Clear();
    options.ProviderOptions.DefaultScopes.Add("openid");
    options.ProviderOptions.DefaultScopes.Add("profile");
    options.ProviderOptions.DefaultScopes.Add("email");
});
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped(_ =>
    new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
    });
    
await builder.Build().RunAsync();
