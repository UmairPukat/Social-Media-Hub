using Microsoft.OpenApi.Models;
using SocialMedia.Api.Configuration;
using SocialMedia.Application;
using SocialMedia.Infrastructure;
using SocialMedia.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Map flat Railway env vars (instagramAppId, JwtSecretKey, …) into nested settings.
builder.Configuration.AddRailwayFlatEnv();

// Clean Architecture registration: Application (services) + Infrastructure (EF, Meta, JWT)
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Social Media Hub API",
        Version = "v1",
        Description = "Clean Architecture API for Facebook, Instagram, WhatsApp and more."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var fromConfig = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
        var fromEnv = builder.Configuration["corsOrigins"]?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? Array.Empty<string>();

        var origins = fromConfig
            .Concat(fromEnv)
            .Select(o => o.TrimEnd('/'))
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .DefaultIfEmpty("http://localhost:4200")
            .ToArray();

        policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Create DB + seed demo admin / invite token.
// SQL Server may not be ready yet if services start in parallel (e.g. on Railway), so
// retry with a delay a few times before giving up. If seeding still fails after all
// retries, log the error and let the application start anyway rather than crashing -
// requests that depend on seeded data will surface their own errors, and the app can
// be restarted or the seed re-attempted later.
if (Environment.GetEnvironmentVariable("SEED_ON_STARTUP")?.ToLower() == "true")
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            await DbSeeder.SeedWithRetryAsync(
                db,
                maxAttempts: 8,
                delayBetweenAttempts: TimeSpan.FromSeconds(3),
                logger: logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database seeding failed after all retry attempts. Continuing startup without seeded data.");
        }
    }
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

