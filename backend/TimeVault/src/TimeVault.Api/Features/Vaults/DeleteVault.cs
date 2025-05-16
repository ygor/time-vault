using FluentValidation;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using TimeVault.Core.Services.Interfaces;

namespace TimeVault.Api.Features.Vaults
{
    public static class DeleteVault
    {
        public class Command : IRequest<bool>
        {
            public Guid VaultId { get; set; }
            public Guid UserId { get; set; }
        }

        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.VaultId).NotEmpty().WithMessage("Vault ID is required");
                RuleFor(x => x.UserId).NotEmpty().WithMessage("User ID is required");
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
                // Only vault owners can delete vaults
                var vault = await _vaultService.GetVaultByIdAsync(request.VaultId, request.UserId);
                
                if (vault == null || vault.OwnerId != request.UserId)
                    return false;
                
                // Delete the vault
                return await _vaultService.DeleteVaultAsync(request.VaultId, request.UserId);
            }
        }
    }
} 