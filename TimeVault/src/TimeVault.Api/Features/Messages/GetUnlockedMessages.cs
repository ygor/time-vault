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
    public static class GetUnlockedMessages
    {
        public class Query : IRequest<List<MessageDto>>
        {
            public Guid UserId { get; set; }
        }

        public class Validator : AbstractValidator<Query>
        {
            public Validator()
            {
                RuleFor(x => x.UserId).NotEmpty().WithMessage("User ID is required");
            }
        }

        public class Handler : IRequestHandler<Query, List<MessageDto>>
        {
            private readonly IMessageService _messageService;
            private readonly IMapper _mapper;

            public Handler(IMessageService messageService, IMapper mapper)
            {
                _messageService = messageService;
                _mapper = mapper;
            }

            public async Task<List<MessageDto>> Handle(Query request, CancellationToken cancellationToken)
            {
                var messages = await _messageService.GetUnlockedMessagesAsync(request.UserId);
                return _mapper.Map<List<MessageDto>>(messages);
            }
        }
    }
} 