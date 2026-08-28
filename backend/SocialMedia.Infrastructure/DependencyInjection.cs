using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SocialMedia.Application.Interfaces;
using SocialMedia.Application.Settings;
using SocialMedia.Domain.Interfaces;
using SocialMedia.Infrastructure.Auth;
using SocialMedia.Infrastructure.Meta;
using SocialMedia.Infrastructure.Persistence;
using SocialMedia.Infrastructure.Repositories;
using System.Text;

namespace SocialMedia.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<MetaSettings>(configuration.GetSection(MetaSettings.SectionName));

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(ResolveConnectionString(configuration));
        });

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAccessTokenRepository, AccessTokenRepository>();
        services.AddScoped<IAppConnectionConfigRepository, AppConnectionConfigRepository>();
        services.AddScoped<IIntegrationAppConfigRepository, IntegrationAppConfigRepository>();
        services.AddScoped<IDeveloperAppConfigRepository, DeveloperAppConfigRepository>();
        services.AddScoped<IProcessDataStoreFactory, ProcessDataStoreFactory>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IInboxRealtimeNotifier, Application.Realtime.NullInboxRealtimeNotifier>();

        services.AddHttpClient<MetaGraphClient>();
        services.AddScoped<IMetaOAuthExchange, MetaOAuthExchangeService>();
        services.AddScoped<IFacebookService, FacebookService>();
        services.AddScoped<IInstagramService, InstagramService>();
        services.AddScoped<IWhatsAppService, WhatsAppService>();

        var jwt = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException("JwtSettings section is missing.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey))
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/inbox"))
                            context.Token = accessToken;
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();
        return services;
    }

    private static string ResolveConnectionString(IConfiguration configuration)
    {
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(databaseUrl))
            return BuildNpgsqlFromUrl(databaseUrl);

        var fromConfig = configuration.GetConnectionString("Default")
            ?? configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(fromConfig))
            return fromConfig;

        var host = Environment.GetEnvironmentVariable("PGHOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("PGPORT") ?? "5432";
        var user = Environment.GetEnvironmentVariable("PGUSER") ?? "postgres";
        var password = Environment.GetEnvironmentVariable("PGPASSWORD") ?? "";
        var database = Environment.GetEnvironmentVariable("PGDATABASE") ?? "postgres";
        var sslMode = Environment.GetEnvironmentVariable("PGSSLMODE") ?? "Prefer";
        return
            $"Host={host};Port={port};Username={user};Password={password};Database={database};SSL Mode={sslMode};Trust Server Certificate=true";
    }

    private static string BuildNpgsqlFromUrl(string databaseUrl)
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var database = uri.AbsolutePath.Trim('/');
        var sslMode = Environment.GetEnvironmentVariable("PGSSLMODE") ?? "Prefer";

        return
            $"Host={uri.Host};Port={(uri.Port > 0 ? uri.Port : 5432)};Username={user};Password={password};Database={database};SSL Mode={sslMode};Trust Server Certificate=true";
    }
}
