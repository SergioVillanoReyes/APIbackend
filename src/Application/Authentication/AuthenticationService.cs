using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Domain;

namespace Application.Authentication;

public sealed class AuthenticationService(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IAccessTokenGenerator tokenGenerator)
{
    private static readonly Regex UsernamePattern = new(
        "^[A-Za-z0-9._-]{3,32}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<UserResponse> RegisterAsync(
        RegisterUserRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRegistration(request);

        var normalizedUsername = Normalize(request.Username);
        var normalizedEmail = Normalize(request.Email);

        if (await users.UsernameExistsAsync(normalizedUsername, cancellationToken))
        {
            throw new UserAlreadyExistsException("username");
        }

        if (await users.EmailExistsAsync(normalizedEmail, cancellationToken))
        {
            throw new UserAlreadyExistsException("email");
        }

        var user = new User(
            request.Username,
            normalizedUsername,
            request.Email,
            normalizedEmail,
            passwordHasher.Hash(request.Password),
            DateTime.UtcNow);

        await users.AddAsync(user, cancellationToken);

        return new UserResponse(user.Id, user.Username, user.Email);
    }

    public async Task<LoginResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.UsernameOrEmail))
        {
            errors[nameof(request.UsernameOrEmail)] = ["Username or email is required."];
        }
        else if (request.UsernameOrEmail.Length > 254)
        {
            errors[nameof(request.UsernameOrEmail)] = ["Username or email cannot exceed 254 characters."];
        }

        if (string.IsNullOrEmpty(request.Password))
        {
            errors[nameof(request.Password)] = ["Password is required."];
        }
        else if (request.Password.Length > 128)
        {
            errors[nameof(request.Password)] = ["Password cannot exceed 128 characters."];
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }

        var user = await users.FindByLoginAsync(
            Normalize(request.UsernameOrEmail),
            cancellationToken);

        if (user is null)
        {
            passwordHasher.Hash(request.Password);
            return null;
        }

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return null;
        }

        var token = tokenGenerator.Generate(user);
        return new LoginResponse(token.Value, "Bearer", token.ExpiresAtUtc);
    }

    private static void ValidateRegistration(RegisterUserRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Username) || !UsernamePattern.IsMatch(request.Username))
        {
            errors[nameof(request.Username)] =
                ["Username must be 3-32 characters and contain only letters, numbers, '.', '_' or '-'."] ;
        }

        if (string.IsNullOrWhiteSpace(request.Email)
            || request.Email.Length > 254
            || !new EmailAddressAttribute().IsValid(request.Email)
            || !IsSimpleEmailAddress(request.Email))
        {
            errors[nameof(request.Email)] = ["A valid email address is required (maximum 254 characters)."];
        }

        if (string.IsNullOrEmpty(request.Password) || request.Password.Length is < 12 or > 128)
        {
            errors[nameof(request.Password)] = ["Password must be between 12 and 128 characters."];
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }
    }

    private static bool IsSimpleEmailAddress(string email)
    {
        try
        {
            return string.Equals(new MailAddress(email).Address, email, StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
