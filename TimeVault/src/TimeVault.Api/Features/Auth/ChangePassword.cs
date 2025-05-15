using System;
using FluentValidation;
using MediatR;
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
            public string CurrentPassword { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
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
                var success = await _authService.ChangePasswordAsync(
                    request.UserId, 
                    request.CurrentPassword, 
                    request.NewPassword);

                if (success)
                {
                    return new ChangePasswordResult
                    {
                        Success = true,
                        Message = "Password changed successfully"
                    };
                }

                return new ChangePasswordResult
                {
                    Success = false,
                    Error = "Failed to change password. Current password may be incorrect."
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