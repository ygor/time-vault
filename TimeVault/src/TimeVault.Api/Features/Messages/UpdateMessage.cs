using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using TimeVault.Core.Services.Interfaces;

namespace TimeVault.Api.Features.Messages
{
    public static class UpdateMessage
    {
        public class Command : IRequest<bool>
        {
            public Guid MessageId { get; set; }
            public Guid UserId { get; set; }
            public string Title { get; set; }
            public string Content { get; set; }
            public DateTime? UnlockDateTime { get; set; }
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
                // First check if user can edit this vault's messages
                var message = await _messageService.GetMessageByIdAsync(request.MessageId, request.UserId);
                if (message == null)
                    return false;

                // Check if user has edit permissions on the vault containing this message
                bool canEdit = await _vaultService.CanEditVaultAsync(message.VaultId, request.UserId);
                if (!canEdit)
                    return false;

                // Update the message
                bool updated = await _messageService.UpdateMessageAsync(
                    request.MessageId,
                    request.UserId,
                    request.Title,
                    request.Content,
                    request.UnlockDateTime
                );

                return updated;
            }
        }
    }
} 