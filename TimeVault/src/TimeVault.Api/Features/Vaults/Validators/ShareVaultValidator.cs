using FluentValidation;

namespace TimeVault.Api.Features.Vaults.Validators
{
    public class ShareVaultValidator : AbstractValidator<ShareVault.Command>
    {
        public ShareVaultValidator()
        {
            RuleFor(x => x.VaultId).NotEmpty().WithMessage("Vault ID is required");
            RuleFor(x => x.OwnerUserId).NotEmpty().WithMessage("Owner User ID is required");
            RuleFor(x => x.TargetUserId).NotEmpty().WithMessage("Target User ID is required");
        }
    }
} 