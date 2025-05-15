using FluentValidation;

namespace TimeVault.Api.Features.Auth.Validators
{
    public class RefreshTokenValidator : AbstractValidator<RefreshToken.Command>
    {
        public RefreshTokenValidator()
        {
            RuleFor(x => x.Token).NotEmpty();
        }
    }
} 