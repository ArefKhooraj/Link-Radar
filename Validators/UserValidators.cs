using FluentValidation;
using LinkRadar.Api.Dtos.User;
using LinkRadar.Api.ResultPattern;

namespace LinkRadar.Api.Validators;

public class CreateUserValidator : AbstractValidator<CreateUserDto>
{
    public CreateUserValidator()
    {
        RuleFor(s => s.Name)
            .NotEmpty().WithErrorCode(ErrorCodes.User.NameRequired)
            .MaximumLength(250).WithErrorCode(ErrorCodes.User.NameTooLong);
    }
}

public class UpdateUserValidator : AbstractValidator<UpdateUserDto>
{
    public UpdateUserValidator()
    {
        RuleFor(s => s.Name)
            .NotEmpty().WithErrorCode(ErrorCodes.User.NameRequired)
            .MaximumLength(250).WithErrorCode(ErrorCodes.User.NameTooLong);
    }
}
