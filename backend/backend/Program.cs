using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
using backend.Hubs;
using backend.Data;
using backend.Services;
using backend.Handlers;
using backend.Configuration;
using backend.Middleware;
using backend.Application.Common.Interfaces;
using backend.Application.Logs.Commands;
using backend.Application.Logs.Queries;
using backend.Application.Logs.Handlers;
using backend.Application.Subscriptions.Queries;
using backend.Application.Subscriptions.Handlers;
using backend.Infrastructure.Persistence;
using backend.Infrastructure.Persistence.Repositories;
using backend.Infrastructure.Storage;

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
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 32))
    )
    .EnableSensitiveDataLogging(false)
    .EnableDetailedErrors(false)
);

// Clean Architecture - Infrastructure Layer
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ILogRepository, LogRepository>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddScoped<IUsageTrackingRepository, UsageTrackingRepository>();
builder.Services.AddSingleton<IFileStorageService, LocalFileStorageService>();

// Clean Architecture - Application Layer Handlers
builder.Services.AddScoped<ICommandHandler<UploadLogCommand, UploadLogResult>, UploadLogCommandHandler>();
builder.Services.AddScoped<ICommandHandler<DeleteLogCommand, DeleteLogResult>, DeleteLogCommandHandler>();
builder.Services.AddScoped<IQueryHandler<ListLogsQuery, List<shared.Dto.LogDto>>, ListLogsQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetLogQuery, GetLogResult>, GetLogQueryHandler>();
builder.Services.AddScoped<IQueryHandler<GetCurrentMonthUsageQuery, CurrentMonthUsageResult>, GetCurrentMonthUsageQueryHandler>();

// Plan & Model Configuration
builder.Services.Configure<PlansConfiguration>(builder.Configuration.GetSection("Plans"));
builder.Services.Configure<ModelsConfig>(builder.Configuration.GetSection("Models"));

// SignalR Query Token config (default false for security - prefer Authorization header)
var allowSignalRQueryToken = builder.Configuration.GetValue<bool>("SignalR:AllowQueryToken", false);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.Authority = "https://auth.blackhatbadshah.com/realms/blackhatbadshah";

    options.RequireHttpsMetadata = true;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidAudiences = new[]
            {
                "blackhatbadshah-api",
                "account"
            },
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        NameClaimType = "sub"
    };

    // SignalR authentication - prefer Authorization header, fallback to query only if explicitly enabled
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var path = context.HttpContext.Request.Path;

            // Only for SignalR hub requests
            if (path.StartsWithSegments("/hubs"))
            {
                // First check Authorization header (preferred)
                var authHeader = context.Request.Headers["Authorization"].ToString();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    context.Token = authHeader.Substring(7);
                }
                // Fallback to query string only if explicitly allowed by config
                else if (allowSignalRQueryToken)
                {
                    var accessToken = context.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        context.Token = accessToken;
                    }
                }
            }

            return Task.CompletedTask;
        }
    };
})
// Add WorkerKey authentication scheme for worker API key auth
.AddScheme<WorkerKeyAuthenticationOptions, WorkerKeyAuthenticationHandler>(
    WorkerKeyAuthenticationOptions.SchemeName,
    options => { });


builder.Services.AddSingleton<ITextractService, TextractService>();
builder.Services.AddSingleton<IHubNotificationService, HubNotificationService>();
builder.Services.AddSingleton<ILogAnalysisQueue, LogAnalysisQueue>();
builder.Services.AddSingleton<ITokenValidationService, TokenValidationService>();
builder.Services.AddHostedService<LogAnalysisBackgroundWorker>();

// Live Log Services
builder.Services.AddSingleton<ILiveLogBuffer, LiveLogBuffer>();
builder.Services.AddSingleton<ILiveLogAnalysisQueue, LiveLogAnalysisQueue>();
builder.Services.AddSingleton<IWorkerRegistry, WorkerRegistry>();
builder.Services.AddHostedService<LiveLogAnalysisBackgroundWorker>();
builder.Services.AddHostedService<WorkerCleanupBackgroundService>();
builder.Services.AddHttpContextAccessor();

// Subscription & Payment Services
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IPlanEnforcementService, PlanEnforcementService>();
builder.Services.AddHttpClient<IRazorpayService, RazorpayService>();
builder.Services.AddHttpClient<IKeycloakAdminService, KeycloakAdminService>(c =>
{
    c.BaseAddress = new Uri(
        builder.Configuration["Keycloak:AdminUrl"]?.TrimEnd('/') + "/"
        ?? "https://auth.blackhatbadshah.com/");
    c.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
    c.DefaultRequestHeaders.UserAgent.ParseAdd("Backend/1.0");
    c.DefaultRequestVersion = System.Net.HttpVersion.Version20;
    c.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    SslOptions = new System.Net.Security.SslClientAuthenticationOptions
    {
        RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true
    },
    EnableMultipleHttp2Connections = true
});

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

// Initialize database with seed data
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var plansConfig = scope.ServiceProvider.GetRequiredService<IOptions<PlansConfiguration>>().Value;
    await DbInitializer.InitializeAsync(dbContext, plansConfig);
}
app.UseRouting();

// Global exception handling - must be early in pipeline
app.UseGlobalExceptionHandler();

app.UseCors("AllowFrontend");
app.Use(async (context, next) =>
{
    if (context.Request.Method == HttpMethods.Options)
    {
        context.Response.StatusCode = StatusCodes.Status204NoContent;
        return;
    }

    await next();
});
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers().RequireCors("AllowFrontend");;

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
app.MapHub<DataHub>("/hubs/data").RequireCors("AllowFrontend");
app.MapHub<LiveLogHub>("/hubs/livelog").RequireCors("AllowFrontend").AllowAnonymous();
app.Run();
