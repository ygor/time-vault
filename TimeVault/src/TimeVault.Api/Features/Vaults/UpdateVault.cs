using FluentValidation;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using TimeVault.Core.Services.Interfaces;

namespace TimeVault.Api.Features.Vaults
{
    public static class UpdateVault
    {
        public class Command : IRequest<bool>
        {
            public Guid VaultId { get; set; }
            public Guid UserId { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
        }

        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.VaultId).NotEmpty().WithMessage("Vault ID is required");
                RuleFor(x => x.UserId).NotEmpty().WithMessage("User ID is required");
                RuleFor(x => x.Name).NotEmpty().MaximumLength(100).WithMessage("Name is required and must be less than 100 characters");
                RuleFor(x => x.Description).MaximumLength(500).WithMessage("Description must be less than 500 characters");
            }
        }

        public class Handler : IRequestHandler<Command, bool>
        {
            private readonly IVaultService _vaultService;

            public Handler(IVaultService vaultService)
            {
                _vaultService = vaultService;
            }

            public async Task<bool> Handle(Command request, CancellationToken cancellationToken)
            {
                // Check if user can edit this vault
                if (!await _vaultService.CanEditVaultAsync(request.VaultId, request.UserId))
                    return false;
                
                // Update the vault
                var updated = await _vaultService.UpdateVaultAsync(
                    request.VaultId,
                    request.UserId,
                    request.Name,
                    request.Description
                );
                
                return updated;
            }
        }
    }
} 