using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TimeVault.Core.Services.Interfaces;

namespace TimeVault.Api.Features.Vaults
{
    public static class GetAllVaults
    {
        public class Query : IRequest<IActionResult>
        {
            public Guid UserId { get; set; }
            public bool SharedOnly { get; set; } = false;
        }

        public class Validator : AbstractValidator<Query>
        {
            public Validator()
            {
                RuleFor(x => x.UserId).NotEmpty().WithMessage("User ID is required");
            }
        }

        public class Handler : IRequestHandler<Query, IActionResult>
        {
            private readonly IVaultService _vaultService;
            private readonly IMapper _mapper;

            public Handler(IVaultService vaultService, IMapper mapper)
            {
                _vaultService = vaultService;
                _mapper = mapper;
            }

            public async Task<IActionResult> Handle(Query request, CancellationToken cancellationToken)
            {
                IEnumerable<Domain.Entities.Vault> vaults;

                if (request.SharedOnly)
                {
                    // Return only shared vaults
                    vaults = await _vaultService.GetSharedVaultsAsync(request.UserId);
                }
                else
                {
                    // Get user owned vaults
                    var userVaults = await _vaultService.GetUserVaultsAsync(request.UserId);
                    
                    // Get vaults shared with the user
                    var sharedVaults = await _vaultService.GetSharedVaultsAsync(request.UserId);
                    
                    // Combine both sets of vaults
                    vaults = userVaults.Concat(sharedVaults);
                }

                // Map the vaults to DTOs
                return new OkObjectResult(_mapper.Map<List<VaultDto>>(vaults));
            }
        }
    }
} 