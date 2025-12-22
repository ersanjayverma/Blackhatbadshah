using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using frontend;
using Blazored.LocalStorage;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");

// 1. Consolidated OIDC Configuration
builder.Services.AddOidcAuthentication(options =>
{
    // Hardcoded overrides
    options.ProviderOptions.Authority = "https://auth.blackhatbadshah.com/realms/blackhatbadshah";
    options.ProviderOptions.ClientId = "blackhatbadshah-spa";
    options.ProviderOptions.ResponseType = "code";
    options.ProviderOptions.DefaultScopes.Add("offline_access"); // Request refresh tokens
    options.ProviderOptions.DefaultScopes.Clear();
    options.ProviderOptions.DefaultScopes.Add("openid");
    options.ProviderOptions.DefaultScopes.Add("profile");
    options.ProviderOptions.DefaultScopes.Add("email");
});


builder.Services.AddBlazoredLocalStorage();


builder.Services.AddScoped(sp => new HttpClient 
{ 
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) 
});

await builder.Build().RunAsync(); // Use RunAsync() for WASM
