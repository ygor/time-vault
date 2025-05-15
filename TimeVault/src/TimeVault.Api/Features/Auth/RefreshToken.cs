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
            public string Token { get; set; } = string.Empty;
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
                var (success, token, error) = await _authService.RefreshTokenAsync(request.Token);

                return new RefreshTokenResult
                {
                    Success = success,
                    Token = token,
                    Error = error
                };
            }
        }

        public class RefreshTokenResult
        {
            public bool Success { get; set; }
            public string Token { get; set; } = string.Empty;
            public string Error { get; set; } = string.Empty;
        }
    }
} 