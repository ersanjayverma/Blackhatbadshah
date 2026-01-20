using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
using backend.Hubs;
using backend.Data;
using Azure.Storage.Blobs;
using backend.Services;
using backend.Handlers;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
// -------------------- Services --------------------
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins("https://blackhatbadshah.com")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // needed if SignalR/auth cookies
    });
});
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://auth.blackhatbadshah.com/realms/blackhatbadshah";
        options.Audience = "blackhatbadshah-api";
        options.RequireHttpsMetadata = true;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true
        };

        // ✅ Enable SignalR authentication from query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                // If the request is for SignalR hub and token is in query string
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var cs = cfg["AzureBlob:ConnectionString"];
    return new BlobServiceClient(cs);
});
builder.Services.AddSingleton<ITextractService, TextractService>();
builder.Services.AddScoped<IHubNotificationService, HubNotificationService>();
builder.Services.AddSingleton<ILogAnalysisQueue, LogAnalysisQueue>();
builder.Services.AddSingleton<ITokenValidationService, TokenValidationService>();
builder.Services.AddHostedService<LogAnalysisBackgroundWorker>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddTransient<ForwardAuthHeaderHandler>();
builder.Services.AddHttpClient<ILogAnalyzer, LogAnalyzer>((sp, client) =>
{
    client.BaseAddress = new Uri("https://ai.blackhatbadshah.com");
    client.Timeout = TimeSpan.FromMinutes(5);
})
.AddHttpMessageHandler<ForwardAuthHeaderHandler>();
builder.Services.AddAuthorization();
// OpenAPI (doc only)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// -------------------- OpenAPI + Scalar --------------------
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // /openapi/v1.json

   app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Blackhatbadshah API")
            .WithTheme(ScalarTheme.BluePlanet)               // ✅ string, not enum
            .EnablePersistentAuthentication(); // ✅ new API
    });
}
app.MapHub<DataHub>("/hubs/data");
app.Run();
