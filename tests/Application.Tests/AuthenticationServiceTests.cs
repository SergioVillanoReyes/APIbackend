using Application.Authentication;
using Domain;

namespace Application.Tests;

public sealed class AuthenticationServiceTests
{
    [Fact]
    public async Task RegisterAsync_StoresOnlyThePasswordHashAndReturnsPublicData()
    {
        var repository = new FakeUserRepository();
        var service = CreateService(repository);

        var result = await service.RegisterAsync(
            new RegisterUserRequest("sergio_1", "sergio@example.com", "correct-horse-battery"));

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("sergio_1", result.Username);
        Assert.Equal("sergio@example.com", result.Email);
        Assert.DoesNotContain("correct-horse-battery", repository.User!.PasswordHash);
        Assert.Equal("test-only-hash", repository.User.PasswordHash);
    }

    [Theory]
    [InlineData("sergio_1", "other@example.com", "username")]
    [InlineData("other_1", "sergio@example.com", "email")]
    public async Task RegisterAsync_RejectsExistingUsernameOrEmail(
        string username,
        string email,
        string expectedField)
    {
        var repository = new FakeUserRepository();
        repository.Seed(new User(
            "sergio_1", "SERGIO_1", "sergio@example.com", "SERGIO@EXAMPLE.COM",
            "test-only-hash", DateTime.UtcNow));
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<UserAlreadyExistsException>(() =>
            service.RegisterAsync(new RegisterUserRequest(username, email, "correct-horse-battery")));

        Assert.Equal(expectedField, exception.Field);
    }

    [Fact]
    public async Task RegisterAsync_RejectsInvalidInputBeforePersisting()
    {
        var repository = new FakeUserRepository();
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.RegisterAsync(new RegisterUserRequest("x", "invalid-email", "short")));

        Assert.Contains(nameof(RegisterUserRequest.Username), exception.Errors.Keys);
        Assert.Contains(nameof(RegisterUserRequest.Email), exception.Errors.Keys);
        Assert.Contains(nameof(RegisterUserRequest.Password), exception.Errors.Keys);
        Assert.Null(repository.User);
    }

    [Fact]
    public async Task LoginAsync_ReturnsTokenOnlyForValidCredentials()
    {
        var repository = new FakeUserRepository();
        repository.Seed(new User(
            "sergio_1", "SERGIO_1", "sergio@example.com", "SERGIO@EXAMPLE.COM",
            "test-only-hash", DateTime.UtcNow));
        var service = CreateService(repository);

        var result = await service.LoginAsync(new LoginRequest("sergio@example.com", "correct-horse-battery"));
        var invalidPassword = await service.LoginAsync(new LoginRequest("sergio_1", "incorrect-password"));
        var missingUser = await service.LoginAsync(new LoginRequest("missing_1", "incorrect-password"));

        Assert.NotNull(result);
        Assert.Equal("test-token", result.AccessToken);
        Assert.Equal("Bearer", result.TokenType);
        Assert.Null(invalidPassword);
        Assert.Null(missingUser);
    }

    [Fact]
    public async Task LoginAsync_RejectsOversizedCredentials()
    {
        var service = CreateService(new FakeUserRepository());

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.LoginAsync(new LoginRequest(new string('u', 255), new string('p', 129))));

        Assert.Contains(nameof(LoginRequest.UsernameOrEmail), exception.Errors.Keys);
        Assert.Contains(nameof(LoginRequest.Password), exception.Errors.Keys);
    }

    private static AuthenticationService CreateService(FakeUserRepository repository) =>
        new(repository, new FakePasswordHasher(), new FakeTokenGenerator());

    private sealed class FakeUserRepository : IUserRepository
    {
        public User? User { get; private set; }

        public void Seed(User user) => User = user;

        public Task<bool> UsernameExistsAsync(string normalizedUsername, CancellationToken cancellationToken) =>
            Task.FromResult(User?.NormalizedUsername == normalizedUsername);

        public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(User?.NormalizedEmail == normalizedEmail);

        public Task<User?> FindByLoginAsync(string normalizedLogin, CancellationToken cancellationToken) =>
            Task.FromResult(
                User is not null
                && (User.NormalizedUsername == normalizedLogin || User.NormalizedEmail == normalizedLogin)
                    ? User
                    : null);

        public Task AddAsync(User user, CancellationToken cancellationToken)
        {
            User = user;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "test-only-hash";

        public bool Verify(string password, string passwordHash) =>
            password == "correct-horse-battery" && passwordHash == Hash(password);
    }

    private sealed class FakeTokenGenerator : IAccessTokenGenerator
    {
        public AccessToken Generate(User user) => new("test-token", DateTime.UtcNow.AddMinutes(15));
    }
}
