using AutoMapper;
using FluentValidation;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using TimeVault.Core.Services.Interfaces;

namespace TimeVault.Api.Features.Vaults
{
    public static class GetVault
    {
        public class Query : IRequest<VaultDto?>
        {
            public Guid VaultId { get; set; }
            public Guid UserId { get; set; }
        }

        public class Validator : AbstractValidator<Query>
        {
            public Validator()
            {
                RuleFor(x => x.VaultId).NotEmpty().WithMessage("Vault ID is required");
                RuleFor(x => x.UserId).NotEmpty().WithMessage("User ID is required");
            }
        }

        public class Handler : IRequestHandler<Query, VaultDto?>
        {
            private readonly IVaultService _vaultService;
            private readonly IMapper _mapper;

            public Handler(IVaultService vaultService, IMapper mapper)
            {
                _vaultService = vaultService;
                _mapper = mapper;
            }

            public async Task<VaultDto?> Handle(Query request, CancellationToken cancellationToken)
            {
                var vault = await _vaultService.GetVaultByIdAsync(request.VaultId, request.UserId);
                
                if (vault == null)
                    return null;
                
                var vaultDto = _mapper.Map<VaultDto>(vault);
                vaultDto.IsOwner = vault.OwnerId == request.UserId;
                
                // Check if the user can edit this vault
                vaultDto.CanEdit = vaultDto.IsOwner || 
                    await _vaultService.CanEditVaultAsync(request.VaultId, request.UserId);
                
                return vaultDto;
            }
        }
    }
} 