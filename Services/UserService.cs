using System.Security.Cryptography;
using System.Text;
using LinkRadar.Api.Context;
using LinkRadar.Api.Dtos.User;
using LinkRadar.Api.Entities;
using LinkRadar.Api.Interface;
using LinkRadar.Api.ResultPattern;
using Microsoft.EntityFrameworkCore;

namespace LinkRadar.Api.Services;

public class UserService(
    LinkRadarDbContext context,
    ICurrentUser currentUser,
    ILogger<UserService> logger)
{
    #region CreateUserAsync

    public async Task<OperationResult<string>> CreateUserAsync(CreateUserDto createUserDto,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Start Create User With This Name = {Name}", createUserDto.Name);

        try
        {
            var apiKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var hash = HashApiKey(apiKey);
            var newUser = new User()
            {
                ApiKey = hash,
                Name = createUserDto.Name,
                IsActive = UserStatus.Active
            };
            await context.Users.AddAsync(newUser, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            // خود کلید و hash آن هرگز لاگ نمی‌شوند
            logger.LogInformation("Complete Create User With This UserId = {UserId}, Name = {Name}",
                newUser.Id, newUser.Name);

            return OperationResult<string>.SuccessResult(apiKey);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Get Exception When Create User With This Name = {Name}, With This Message {Message}",
                createUserDto.Name, e.Message);
            throw;
        }
    }

    #endregion

    #region ValidateApiKeyAsync

    public async Task<OperationResult<int>> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken)
    {
        var hash = HashApiKey(apiKey);

        var userId = await context.Users
            .Where(s => s.ApiKey == hash && s.IsActive == UserStatus.Active && !s.IsDeleted)
            .Select(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (userId == 0)
        {
            logger.LogWarning("Request With Invalid Or Inactive ApiKey Rejected");

            return OperationResult<int>.FailureResult(
                Error.Unauthorized(ErrorCodes.User.InvalidApiKey, "ApiKey is invalid or inactive"));
        }

        logger.LogDebug("ApiKey Validated For This UserId = {UserId}", userId);

        return OperationResult<int>.SuccessResult(userId);
    }

    #endregion


    public static string HashApiKey(string apiKey)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));

    #region UpdateUserAsync

    public async Task<OperationResult> UpdateUserAsync(UpdateUserDto updateUserDto,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;

        logger.LogInformation("Start Update User For This UserId = {UserId}, NewName = {Name}",
            userId, updateUserDto.Name);

        try
        {
            var changedRow =
                await context.Users.Where(s => s.Id == userId)
                    .ExecuteUpdateAsync(s => s.SetProperty(f => f.Name, updateUserDto.Name), cancellationToken);

            if (changedRow == 0)
            {
                logger.LogWarning("Can Not Find User For Update With This UserId = {UserId}", userId);

                return OperationResult.FailureResult(Error.NotFound(ErrorCodes.User.NotFound, "User not found"));
            }

            logger.LogInformation("Complete Update User For This UserId = {UserId}", userId);

            return OperationResult.SuccessResult();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Get Exception When Update User For This UserId = {UserId}, With This Message {Message}",
                userId, e.Message);
            throw;
        }
    }

    #endregion
}