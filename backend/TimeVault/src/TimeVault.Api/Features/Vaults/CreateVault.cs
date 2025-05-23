using AutoMapper;
using FluentValidation;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using TimeVault.Core.Services.Interfaces;

namespace TimeVault.Api.Features.Vaults
{
    public static class CreateVault
    {
        public class Command : IRequest<VaultDto>
        {
            public Guid UserId { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
        }

        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.UserId).NotEmpty().WithMessage("User ID is required");
                RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required");
                RuleFor(x => x.Description).MaximumLength(500).WithMessage("Description cannot exceed 500 characters");
            }
        }

        public class Handler : IRequestHandler<Command, VaultDto>
        {
            private readonly IVaultService _vaultService;
            private readonly IMapper _mapper;

            public Handler(IVaultService vaultService, IMapper mapper)
            {
                _vaultService = vaultService;
                _mapper = mapper;
            }

            public async Task<VaultDto> Handle(Command request, CancellationToken cancellationToken)
            {
                var vault = await _vaultService.CreateVaultAsync(
                    request.UserId,
                    request.Name,
                    request.Description
                );
                
                var vaultDto = _mapper.Map<VaultDto>(vault);
                vaultDto.IsOwner = true;
                vaultDto.CanEdit = true;
                
                return vaultDto;
            }
        }
    }
} 