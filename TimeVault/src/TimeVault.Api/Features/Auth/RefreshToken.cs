using FluentValidation;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using TimeVault.Core.Services.Interfaces;

namespace TimeVault.Api.Features.Auth
{
    public static class RefreshToken
    {
        public class Command : IRequest<RefreshTokenResult>
        {
            public string Token { get; set; }
        }

        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.Token).NotEmpty();
            }
        }

        public class Handler : IRequestHandler<Command, RefreshTokenResult>
        {
            private readonly IAuthService _authService;

            public Handler(IAuthService authService)
            {
                _authService = authService;
            }

            public async Task<RefreshTokenResult> Handle(Command request, CancellationToken cancellationToken)
            {
                var result = await _authService.RefreshTokenAsync(request.Token);

                return new RefreshTokenResult
                {
                    Success = result.Success,
                    Token = result.Token,
                    Error = result.Error
                };
            }
        }

        public class RefreshTokenResult
        {
            public bool Success { get; set; }
            public string Token { get; set; }
            public string Error { get; set; }
        }
    }
} 