using FluentValidation;
using LinkRadar.Api.Dtos.User;
using LinkRadar.Api.ResultPattern;
using LinkRadar.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LinkRadar.Api.EndpointHandlers;

public class UserEndpointHandler
{
    #region CreateUser

    public static async Task<Results<Ok<CreateUserResponseDto>, BadRequest<ProblemDetails>, ValidationProblem>>
        CreateUser(
            CreateUserDto createUserDto, ILogger<UserEndpointHandler> logger, UserService userService,
            IValidator<CreateUserDto> validator, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(createUserDto, cancellationToken);

        if (!validation.IsValid)
        {
            logger.LogWarning("Invalid Create User Request With This Name = {Name}, Errors = {Errors}",
                createUserDto.Name, validation.ToString());

            return TypedResults.ValidationProblem(validation.ToDictionary());
        }

        var result = await userService.CreateUserAsync(createUserDto, cancellationToken);

        if (!result.IsSuccess)
            return TypedResults.BadRequest(ToProblem(result));

        // کلید فقط همین یک بار برمی‌گردد و بعداً قابل بازیابی نیست
        return TypedResults.Ok(new CreateUserResponseDto(result.Result!));
    }

    #endregion

    #region GetUser

    public static async Task<Results<Ok<UserDto>, NotFound<ProblemDetails>>> GetUser(
        UserService userService, CancellationToken cancellationToken)
    {
        var result = await userService.GetUserAsync(cancellationToken);

        if (!result.IsSuccess)
            return TypedResults.NotFound(ToProblem(result));

        return TypedResults.Ok(result.Result!);
    }

    #endregion

    #region UpdateUser

    public static async Task<Results<Ok, NotFound<ProblemDetails>, ValidationProblem>> UpdateUser(
        UpdateUserDto updateUserDto, ILogger<UserEndpointHandler> logger, UserService userService,
        IValidator<UpdateUserDto> validator, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(updateUserDto, cancellationToken);

        if (!validation.IsValid)
        {
            logger.LogWarning("Invalid Update User Request With This Name = {Name}, Errors = {Errors}",
                updateUserDto.Name, validation.ToString());

            return TypedResults.ValidationProblem(validation.ToDictionary());
        }

        var result = await userService.UpdateUserAsync(updateUserDto, cancellationToken);

        if (!result.IsSuccess)
            return TypedResults.NotFound(ToProblem(result));

        return TypedResults.Ok();
    }

    #endregion

    #region DeleteUser

    public static async Task<Results<NoContent, NotFound<ProblemDetails>>> DeleteUser(
        UserService userService, CancellationToken cancellationToken)
    {
        var result = await userService.DeleteUserAsync(cancellationToken);

        if (!result.IsSuccess)
            return TypedResults.NotFound(ToProblem(result));

        return TypedResults.NoContent();
    }

    #endregion

    private static ProblemDetails ToProblem(OperationResult result)
    {
        var error = result.Errors[0];

        return new ProblemDetails
        {
            Title = error.Code,
            Detail = error.Message
        };
    }
}
