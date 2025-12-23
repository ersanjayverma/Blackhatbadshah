using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using backend.Hubs;
using backend.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
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
    });


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
