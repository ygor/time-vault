using AutoMapper;
using FluentValidation;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using TimeVault.Core.Services.Interfaces;

namespace TimeVault.Api.Features.Messages
{
    public static class UnlockMessage
    {
        public class Query : IRequest<MessageDto?>
        {
            public Guid MessageId { get; set; }
            public Guid UserId { get; set; }
        }

        public class Validator : AbstractValidator<Query>
        {
            public Validator()
            {
                RuleFor(x => x.MessageId).NotEmpty().WithMessage("Message ID is required");
                RuleFor(x => x.UserId).NotEmpty().WithMessage("User ID is required");
            }
        }

        public class Handler : IRequestHandler<Query, MessageDto?>
        {
            private readonly IMessageService _messageService;
            private readonly IMapper _mapper;

            public Handler(IMessageService messageService, IMapper mapper)
            {
                _messageService = messageService;
                _mapper = mapper;
            }

            public async Task<MessageDto?> Handle(Query request, CancellationToken cancellationToken)
            {
                // Attempt to unlock the message
                var unlockedMessage = await _messageService.UnlockMessageAsync(request.MessageId, request.UserId);
                
                if (unlockedMessage == null)
                    return null;
                
                return _mapper.Map<MessageDto>(unlockedMessage);
            }
        }
    }
} 