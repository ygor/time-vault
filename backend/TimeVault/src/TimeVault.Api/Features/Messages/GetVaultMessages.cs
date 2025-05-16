using AutoMapper;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TimeVault.Core.Services.Interfaces;

namespace TimeVault.Api.Features.Messages
{
    public static class GetVaultMessages
    {
        public class Query : IRequest<List<MessageDto>>
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

        public class Handler : IRequestHandler<Query, List<MessageDto>>
        {
            private readonly IMessageService _messageService;
            private readonly IVaultService _vaultService;
            private readonly IMapper _mapper;

            public Handler(IMessageService messageService, IVaultService vaultService, IMapper mapper)
            {
                _messageService = messageService;
                _vaultService = vaultService;
                _mapper = mapper;
            }

            public async Task<List<MessageDto>> Handle(Query request, CancellationToken cancellationToken)
            {
                // Check if user has access to the vault
                var hasAccess = await _vaultService.HasVaultAccessAsync(request.VaultId, request.UserId);
                
                if (!hasAccess)
                    return new List<MessageDto>();
                
                var messages = await _messageService.GetVaultMessagesAsync(request.VaultId, request.UserId);
                return _mapper.Map<List<MessageDto>>(messages);
            }
        }
    }
} 