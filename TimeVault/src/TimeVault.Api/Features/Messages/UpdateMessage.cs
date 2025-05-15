using FluentValidation;
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

        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.MessageId).NotEmpty().WithMessage("Message ID is required");
                RuleFor(x => x.UserId).NotEmpty().WithMessage("User ID is required");
                RuleFor(x => x.Title).NotEmpty().MaximumLength(100).WithMessage("Title is required and must be less than 100 characters");
                RuleFor(x => x.Content).NotEmpty().WithMessage("Content is required");
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
                
                // Update the message
                return await _messageService.UpdateMessageAsync(
                    request.MessageId,
                    request.UserId,
                    request.Title,
                    request.Content,
                    request.UnlockDateTime
                );
            }
        }
    }
} 