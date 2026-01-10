using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using frontend;
using Microsoft.Extensions.Http;
using Blazored.LocalStorage;
using frontend.Services;
using System.Text.Json;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");

// 1. Consolidated OIDC Configuration
builder.Services.AddOidcAuthentication(options =>
{
    // Hardcoded overrides
options.ProviderOptions.Authority = "https://auth.blackhatbadshah.com/realms/blackhatbadshah";
options.ProviderOptions.ClientId = "blackhatbadshah-spa";
options.ProviderOptions.ResponseType = "code";
options.ProviderOptions.DefaultScopes.Clear();
options.ProviderOptions.DefaultScopes.Add("offline_access"); // Request refresh tokens
options.ProviderOptions.DefaultScopes.Add("openid");
options.ProviderOptions.DefaultScopes.Add("profile");
options.ProviderOptions.DefaultScopes.Add("email");
});
builder.Services.AddScoped<ApiLoaderService>();
builder.Services.AddScoped<HubConnectionService>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddTransient<ApiLoaderHandler>();
builder.Services.AddScoped<CustomAuthorizationMessageHandler>();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddHttpClient("BlackHatBadshahApi", client =>
    client.BaseAddress = new Uri("https://api.blackhatbadshah.com"))
    .AddHttpMessageHandler<ApiLoaderHandler>()
    .AddHttpMessageHandler<CustomAuthorizationMessageHandler>();

// Register services that depend on the API HttpClient
builder.Services.AddScoped<LogService>(sp => new LogService(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("BlackHatBadshahApi")));
builder.Services.AddScoped<ReportService>(sp => new ReportService(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("BlackHatBadshahApi")));
builder.Services.AddScoped<SubscriptionService>(sp => new SubscriptionService(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("BlackHatBadshahApi")));
builder.Services.AddHttpClient("BlackHatBadshahAi", client => 
    client.BaseAddress = new Uri("https://ai.blackhatbadshah.com"))
    .AddHttpMessageHandler<CustomAuthorizationMessageHandler>();
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>()
    .CreateClient("BlackHatBadshahApi"));
builder.Services.AddHttpClient("BlackHatBadshahAi-free", client => 
    client.BaseAddress = new Uri("https://ai.blackhatbadshah.com"));
await builder.Build().RunAsync(); // Use RunAsync() for WASM
