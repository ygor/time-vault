using FluentValidation;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using TimeVault.Core.Services.Interfaces;

namespace TimeVault.Api.Features.Messages
{
    public static class MarkMessageAsRead
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

            public Handler(IMessageService messageService)
            {
                _messageService = messageService;
            }

            public async Task<bool> Handle(Command request, CancellationToken cancellationToken)
            {
                return await _messageService.MarkMessageAsReadAsync(request.MessageId, request.UserId);
            }
        }
    }
} 