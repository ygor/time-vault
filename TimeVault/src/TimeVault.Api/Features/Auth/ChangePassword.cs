using FluentValidation;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using TimeVault.Core.Services.Interfaces;

namespace TimeVault.Api.Features.Auth
{
    public static class ChangePassword
    {
        public class Command : IRequest<ChangePasswordResult>
        {
            public Guid UserId { get; set; }
            public string CurrentPassword { get; set; }
            public string NewPassword { get; set; }
        }

        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.UserId).NotEmpty();
                RuleFor(x => x.CurrentPassword).NotEmpty();
                RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(6).MaximumLength(100);
            }
        }

        public class Handler : IRequestHandler<Command, ChangePasswordResult>
        {
            private readonly IAuthService _authService;

            public Handler(IAuthService authService)
            {
                _authService = authService;
            }

            public async Task<ChangePasswordResult> Handle(Command request, CancellationToken cancellationToken)
            {
                var result = await _authService.ChangePasswordAsync(request.UserId, request.CurrentPassword, request.NewPassword);

                return new ChangePasswordResult
                {
                    Success = result
                };
            }
        }

        public class ChangePasswordResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public string Error { get; set; } = string.Empty;
        }
    }
} 