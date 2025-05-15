using FluentValidation;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using TimeVault.Core.Services.Interfaces;

namespace TimeVault.Api.Features.Messages
{
    public static class DeleteMessage
    {
        public class Command : IRequest<bool>
        {
            public Guid MessageId { get; set; }
            public Guid UserId { get; set; }
        }

        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.MessageId).NotEmpty().WithMessage("Message ID is required");
                RuleFor(x => x.UserId).NotEmpty().WithMessage("User ID is required");
            }
        }

        public class Handler : IRequestHandler<Command, bool>
        {
            private readonly IMessageService _messageService;
            private readonly IVaultService _vaultService;

            public Handler(IMessageService messageService, IVaultService vaultService)
            {
                _messageService = messageService;
                _vaultService = vaultService;
            }

            public async Task<bool> Handle(Command request, CancellationToken cancellationToken)
            {
                // Get the message to check if user has edit permissions
                var message = await _messageService.GetMessageByIdAsync(request.MessageId, request.UserId);
                if (message == null)
                    return false;
                
                // Check if user can edit the vault containing this message
                var canEdit = await _vaultService.CanEditVaultAsync(message.VaultId, request.UserId);
                if (!canEdit)
                    return false;
                
                // Delete the message
                return await _messageService.DeleteMessageAsync(request.MessageId, request.UserId);
            }
        }
    }
} 