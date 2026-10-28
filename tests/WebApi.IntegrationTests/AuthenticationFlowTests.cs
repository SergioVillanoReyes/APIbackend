using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Microsoft.IdentityModel.Tokens;

namespace WebApi.IntegrationTests;

public sealed class AuthenticationFlowTests : IAsyncLifetime
{
    private const string SigningKey = "integration-test-only-key-with-at-least-32-bytes";
    private const string Issuer = "APIbackend.IntegrationTests";
    private const string Audience = "APIbackend.IntegrationTests.Client";

    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword($"Aa1!{Guid.NewGuid():N}")
        .Build();

    [Fact]
    public async Task RegistrationLoginAndProtectedIdentity_WorkAgainstSqlServer()
    {
        await using var factory = new AuthenticationApiFactory(_sqlServer.GetConnectionString());
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        using var client = factory.CreateClient();
        const string password = "correct-horse-battery";
        var registration = new
        {
            username = $"user_{Guid.NewGuid():N}"[..13],
            email = $"{Guid.NewGuid():N}@example.com",
            password
        };

        var invalidRegistration = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { username = "x", email = "not-an-email", password = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidRegistration.StatusCode);

        var registrationResponse = await client.PostAsJsonAsync("/api/auth/register", registration);
        Assert.Equal(HttpStatusCode.Created, registrationResponse.StatusCode);
        var user = await registrationResponse.Content.ReadFromJsonAsync<RegisteredUser>();
        Assert.NotNull(user);
        Assert.DoesNotContain("passwordHash", await registrationResponse.Content.ReadAsStringAsync());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var storedUser = await dbContext.Users.SingleAsync(item => item.Id == user.Id);
            Assert.NotEqual(password, storedUser.PasswordHash);
            Assert.StartsWith("pbkdf2-sha256$v1$600000$", storedUser.PasswordHash);
        }

        var duplicateResponse = await client.PostAsJsonAsync("/api/auth/register", registration);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        var caseInsensitiveDuplicate = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                username = registration.username.ToUpperInvariant(),
                email = $"{Guid.NewGuid():N}@example.com",
                password
            });
        Assert.Equal(HttpStatusCode.Conflict, caseInsensitiveDuplicate.StatusCode);

        var invalidLogin = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { usernameOrEmail = registration.username, password = "incorrect-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, invalidLogin.StatusCode);
        var missingUserLogin = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { usernameOrEmail = "missing_user", password = "incorrect-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, missingUserLogin.StatusCode);
        var invalidLoginProblem = await invalidLogin.Content.ReadFromJsonAsync<ProblemDetails>();
        var missingUserProblem = await missingUserLogin.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(invalidLoginProblem);
        Assert.NotNull(missingUserProblem);
        Assert.Equal(invalidLoginProblem.Title, missingUserProblem.Title);
        Assert.Equal(invalidLoginProblem.Detail, missingUserProblem.Detail);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { usernameOrEmail = registration.email, password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResult>();
        Assert.NotNull(login);
        Assert.Equal("Bearer", login.TokenType);
        Assert.True(login.ExpiresAtUtc > DateTime.UtcNow);

        var missingToken = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, missingToken.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var currentUserResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, currentUserResponse.StatusCode);
        var currentUser = await currentUserResponse.Content.ReadFromJsonAsync<CurrentUser>();
        Assert.NotNull(currentUser);
        Assert.Equal(user.Id, currentUser.Id);
        Assert.Equal(user.Username, currentUser.Username);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.token.value");
        var invalidToken = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, invalidToken.StatusCode);

        var invalidTokens = new[]
        {
            CreateToken(SigningKey, Issuer, Audience, DateTime.UtcNow.AddMinutes(-1)),
            CreateToken("different-test-key-with-at-least-32-bytes", Issuer, Audience, DateTime.UtcNow.AddMinutes(5)),
            CreateToken(SigningKey, "wrong-issuer", Audience, DateTime.UtcNow.AddMinutes(5)),
            CreateToken(SigningKey, Issuer, "wrong-audience", DateTime.UtcNow.AddMinutes(5))
        };
        foreach (var token in invalidTokens)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var rejectedToken = await client.GetAsync("/api/auth/me");
            Assert.Equal(HttpStatusCode.Unauthorized, rejectedToken.StatusCode);
        }

        var concurrentUsername = $"race_{Guid.NewGuid():N}"[..13];
        var concurrentRegistrations = await Task.WhenAll(
            client.PostAsJsonAsync(
                "/api/auth/register",
                new
                {
                    username = concurrentUsername,
                    email = $"{Guid.NewGuid():N}@example.com",
                    password
                }),
            client.PostAsJsonAsync(
                "/api/auth/register",
                new
                {
                    username = concurrentUsername,
                    email = $"{Guid.NewGuid():N}@example.com",
                    password
                }));
        Assert.Equal(
            new[] { HttpStatusCode.Created, HttpStatusCode.Conflict },
            concurrentRegistrations.Select(response => response.StatusCode).OrderBy(status => status).ToArray());
    }

    public Task InitializeAsync() => _sqlServer.StartAsync();

    public Task DisposeAsync() => _sqlServer.DisposeAsync().AsTask();

    private sealed class AuthenticationApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var sqlConnection = new SqlConnectionStringBuilder(connectionString)
            {
                InitialCatalog = $"AuthTests_{Guid.NewGuid():N}"
            };

            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = sqlConnection.ConnectionString,
                    ["Jwt:Issuer"] = Issuer,
                    ["Jwt:Audience"] = Audience,
                    ["Jwt:SigningKey"] = SigningKey,
                    ["Jwt:ExpiresMinutes"] = "15"
                }));
        }
    }

    private static string CreateToken(
        string signingKey,
        string issuer,
        string audience,
        DateTime expiresAtUtc)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            [new Claim("sub", Guid.NewGuid().ToString()), new Claim("preferred_username", "test_user")],
            DateTime.UtcNow.AddMinutes(-5),
            expiresAtUtc,
            credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record RegisteredUser(Guid Id, string Username, string Email);

    private sealed record LoginResult(string AccessToken, string TokenType, DateTime ExpiresAtUtc);

    private sealed record CurrentUser(Guid Id, string Username);
}
