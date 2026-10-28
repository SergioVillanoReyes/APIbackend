using System.Security.Claims;
using Application.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Authentication");
        group.MapPost("/register", RegisterAsync).WithName("RegisterUser");
        group.MapPost("/login", LoginAsync).WithName("Login");
        group.MapGet("/me", GetCurrentUser).RequireAuthorization().WithName("GetCurrentUser");
        return group;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterUserRequest request,
        AuthenticationService authentication,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await authentication.RegisterAsync(request, cancellationToken);
            return Results.Json(user, statusCode: StatusCodes.Status201Created);
        }
        catch (RequestValidationException exception)
        {
            return Results.ValidationProblem(exception.Errors);
        }
        catch (UserAlreadyExistsException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: $"The {exception.Field} is already registered.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = $"{exception.Field}_already_exists"
                });
        }
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        AuthenticationService authentication,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await authentication.LoginAsync(request, cancellationToken);
            return response is null
                ? Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Unauthorized",
                    detail: "Invalid username/email or password.")
                : Results.Ok(response);
        }
        catch (RequestValidationException exception)
        {
            return Results.ValidationProblem(exception.Errors);
        }
    }

    private static IResult GetCurrentUser(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue("sub");
        var username = principal.FindFirstValue("preferred_username");

        if (!Guid.TryParse(id, out var userId) || string.IsNullOrWhiteSpace(username))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new CurrentUserResponse(userId, username));
    }

    private sealed record CurrentUserResponse(Guid Id, string Username);
}
