using FluentValidation;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using TimeVault.Core.Services.Interfaces;

namespace TimeVault.Api.Features.Messages
{
    public static class CreateMessage
    {
        public class Command : IRequest<MessageDto?>
        {
            public Guid VaultId { get; set; }
            public Guid UserId { get; set; }
            public string Title { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
            public DateTime? UnlockDateTime { get; set; }
        }

        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.VaultId).NotEmpty().WithMessage("Vault ID is required");
                RuleFor(x => x.UserId).NotEmpty().WithMessage("User ID is required");
                RuleFor(x => x.Title).NotEmpty().WithMessage("Title is required");
                RuleFor(x => x.Content).NotEmpty().WithMessage("Content is required");
            }
        }

        public class Handler : IRequestHandler<Command, MessageDto?>
        {
            private readonly IMessageService _messageService;
            private readonly IMapper _mapper;

            public Handler(IMessageService messageService, IMapper mapper)
            {
                _messageService = messageService;
                _mapper = mapper;
            }

            public async Task<MessageDto?> Handle(Command request, CancellationToken cancellationToken)
            {
                var message = await _messageService.CreateMessageAsync(
                    request.VaultId,
                    request.UserId,
                    request.Title,
                    request.Content,
                    request.UnlockDateTime);

                if (message == null)
                    return null;
                    
                return _mapper.Map<MessageDto>(message);
            }
        }
    }
} 