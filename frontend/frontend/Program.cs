using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Blazored.LocalStorage;
using frontend;
using frontend.Services;
using frontend.Services.Interfaces;
using frontend.ViewModels;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");

// -------------------- OIDC --------------------
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
    options.ProviderOptions.DefaultScopes.Add("offline_access");
});

// -------------------- SERVICES --------------------
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<ApiLoaderService>();
builder.Services.AddScoped<HubConnectionService>();
builder.Services.AddScoped<ToastService>();

// -------------------- AUTH HANDLER (FIX) --------------------
builder.Services.AddScoped<CustomAuthorizationMessageHandler>();

// -------------------- API CLIENT --------------------
builder.Services.AddHttpClient("BlackHatBadshahApi", client =>
{
    client.BaseAddress = new Uri("https://api.blackhatbadshah.com/");
})
.AddHttpMessageHandler<CustomAuthorizationMessageHandler>();

// -------------------- PUBLIC API CLIENT (NO AUTH) --------------------
builder.Services.AddHttpClient("BlackHatBadshahApiPublic", client =>
{
    client.BaseAddress = new Uri("https://api.blackhatbadshah.com/");
});

// -------------------- AI CLIENT --------------------
builder.Services.AddHttpClient("BlackHatBadshahAi", client =>
{
    client.BaseAddress = new Uri("https://ai.blackhatbadshah.com/");
})
.AddHttpMessageHandler<CustomAuthorizationMessageHandler>();

// -------------------- APP SERVICES --------------------
builder.Services.AddScoped<LogService>(sp =>
    new LogService(sp.GetRequiredService<IHttpClientFactory>()
        .CreateClient("BlackHatBadshahApi")));
builder.Services.AddScoped<ILogService>(sp => sp.GetRequiredService<LogService>());

builder.Services.AddScoped<ReportService>(sp =>
    new ReportService(sp.GetRequiredService<IHttpClientFactory>()
        .CreateClient("BlackHatBadshahApi")));
builder.Services.AddScoped<IReportService>(sp => sp.GetRequiredService<ReportService>());

builder.Services.AddScoped<SubscriptionService>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return new SubscriptionService(
        factory.CreateClient("BlackHatBadshahApi"),
        factory.CreateClient("BlackHatBadshahApiPublic"));
});
builder.Services.AddScoped<ISubscriptionService>(sp => sp.GetRequiredService<SubscriptionService>());

builder.Services.AddScoped<DashboardService>(sp =>
    new DashboardService(sp.GetRequiredService<IHttpClientFactory>()
        .CreateClient("BlackHatBadshahApi")));
builder.Services.AddScoped<IDashboardService>(sp => sp.GetRequiredService<DashboardService>());

builder.Services.AddScoped<LiveLogService>(sp =>
    new LiveLogService(sp.GetRequiredService<IHttpClientFactory>()
        .CreateClient("BlackHatBadshahApi")));
builder.Services.AddScoped<ILiveLogService>(sp => sp.GetRequiredService<LiveLogService>());

builder.Services.AddScoped<WorkerService>(sp =>
    new WorkerService(sp.GetRequiredService<IHttpClientFactory>()
        .CreateClient("BlackHatBadshahApi")));
builder.Services.AddScoped<IWorkerService>(sp => sp.GetRequiredService<WorkerService>());

builder.Services.AddScoped<WorkerAgentService>(sp =>
    new WorkerAgentService(sp.GetRequiredService<IHttpClientFactory>()
        .CreateClient("BlackHatBadshahApi")));

builder.Services.AddScoped<LinuxSystemService>(sp =>
    new LinuxSystemService(sp.GetRequiredService<IHttpClientFactory>()
        .CreateClient("BlackHatBadshahApi")));

// -------------------- NEW FEATURE SERVICES --------------------
builder.Services.AddScoped<TagService>(sp =>
    new TagService(sp.GetRequiredService<IHttpClientFactory>()
        .CreateClient("BlackHatBadshahApi")));

builder.Services.AddScoped<ShareService>(sp =>
    new ShareService(sp.GetRequiredService<IHttpClientFactory>()
        .CreateClient("BlackHatBadshahApi")));

builder.Services.AddScoped<ActivityService>(sp =>
    new ActivityService(sp.GetRequiredService<IHttpClientFactory>()
        .CreateClient("BlackHatBadshahApi")));

builder.Services.AddScoped<BookmarkService>(sp =>
    new BookmarkService(sp.GetRequiredService<IHttpClientFactory>()
        .CreateClient("BlackHatBadshahApi")));

builder.Services.AddScoped<UserSettingsService>(sp =>
    new UserSettingsService(sp.GetRequiredService<IHttpClientFactory>()
        .CreateClient("BlackHatBadshahApi")));

// -------------------- VIEWMODELS --------------------
builder.Services.AddScoped<DashboardViewModel>();
builder.Services.AddScoped<LogsViewModel>();

await builder.Build().RunAsync();
