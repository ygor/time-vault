using FluentValidation;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using TimeVault.Core.Services.Interfaces;

namespace TimeVault.Api.Features.Vaults
{
    public static class ShareVault
    {
        public class Command : IRequest<bool>
        {
            public Guid VaultId { get; set; }
            public Guid OwnerUserId { get; set; }
            public Guid TargetUserId { get; set; }
            public bool CanEdit { get; set; }
        }

        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.VaultId).NotEmpty().WithMessage("Vault ID is required");
                RuleFor(x => x.OwnerUserId).NotEmpty().WithMessage("Owner User ID is required");
                RuleFor(x => x.TargetUserId).NotEmpty().WithMessage("Target User ID is required");
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
                // Verify that the requesting user is the owner
                var vault = await _vaultService.GetVaultByIdAsync(request.VaultId, request.OwnerUserId);
                
                if (vault == null || vault.OwnerId != request.OwnerUserId)
                    return false;
                
                // Cannot share with yourself
                if (request.OwnerUserId == request.TargetUserId)
                    return false;
                    
                // Share the vault
                return await _vaultService.ShareVaultAsync(
                    request.VaultId,
                    request.OwnerUserId,
                    request.TargetUserId,
                    request.CanEdit
                );
            }
        }
    }
} 