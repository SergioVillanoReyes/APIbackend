using Application.Authentication;
using Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class UserRepository(AuthDbContext dbContext) : IUserRepository
{
    public Task<bool> UsernameExistsAsync(
        string normalizedUsername,
        CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(
            user => user.NormalizedUsername == normalizedUsername,
            cancellationToken);

    public Task<bool> EmailExistsAsync(
        string normalizedEmail,
        CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(
            user => user.NormalizedEmail == normalizedEmail,
            cancellationToken);

    public Task<User?> FindByLoginAsync(
        string normalizedLogin,
        CancellationToken cancellationToken) =>
        dbContext.Users.SingleOrDefaultAsync(
            user => user.NormalizedUsername == normalizedLogin
                || user.NormalizedEmail == normalizedLogin,
            cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        dbContext.Users.Add(user);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            var field = GetDuplicateField(exception);
            if (field is null)
            {
                throw;
            }

            throw new UserAlreadyExistsException(field);
        }
    }

    private static string? GetDuplicateField(DbUpdateException exception)
    {
        var sqlException = exception.InnerException as SqlException;
        if (sqlException?.Number is 2601 or 2627)
        {
            if (sqlException.Message.Contains("IX_Users_NormalizedUsername", StringComparison.Ordinal))
            {
                return "username";
            }

            if (sqlException.Message.Contains("IX_Users_NormalizedEmail", StringComparison.Ordinal))
            {
                return "email";
            }
        }

        return null;
    }
}
