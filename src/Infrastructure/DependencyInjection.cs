using Application.Authentication;
using Infrastructure.Authentication;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<DatabaseConnectionOptions>()
            .Configure(options =>
                options.ConnectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty)
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "ConnectionStrings:DefaultConnection is required.")
            .ValidateOnStart();

        services.AddDbContext<AuthDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
            }

            options.UseSqlServer(connectionString);
        });
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IAccessTokenGenerator, JwtAccessTokenGenerator>();
        return services;
    }
}
