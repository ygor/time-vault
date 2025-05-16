using FluentValidation;

namespace TimeVault.Api.Features.Auth.Validators
{
    public class ChangePasswordValidator : AbstractValidator<ChangePassword.Command>
    {
        public ChangePasswordValidator()
        {
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.CurrentPassword).NotEmpty();
            RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(6).MaximumLength(100);
        }
    }
} 