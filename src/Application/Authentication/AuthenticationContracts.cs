using Domain;

namespace Application.Authentication;

public interface IUserRepository
{
    Task<bool> UsernameExistsAsync(string normalizedUsername, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<User?> FindByLoginAsync(string normalizedLogin, CancellationToken cancellationToken);

    Task AddAsync(User user, CancellationToken cancellationToken);
}

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}

public interface IAccessTokenGenerator
{
    AccessToken Generate(User user);
}

public sealed record AccessToken(string Value, DateTime ExpiresAtUtc);

public sealed record RegisterUserRequest(string Username, string Email, string Password);

public sealed record LoginRequest(string UsernameOrEmail, string Password);

public sealed record UserResponse(Guid Id, string Username, string Email);

public sealed record LoginResponse(string AccessToken, string TokenType, DateTime ExpiresAtUtc);

public sealed class UserAlreadyExistsException(string field)
    : Exception($"The {field} is already registered.")
{
    public string Field { get; } = field;
}

public sealed class RequestValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
