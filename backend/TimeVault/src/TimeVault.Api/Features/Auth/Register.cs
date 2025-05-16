using FluentValidation;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using TimeVault.Core.Services.Interfaces;

namespace TimeVault.Api.Features.Auth
{
    public static class Register
    {
        public class Command : IRequest<AuthResult>
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.Email)
                    .NotEmpty().WithMessage("Email is required")
                    .EmailAddress().WithMessage("Invalid email format");
                
                RuleFor(x => x.Password)
                    .NotEmpty().WithMessage("Password is required")
                    .MinimumLength(8).WithMessage("Password must be at least 8 characters");
            }
        }

        public class Handler : IRequestHandler<Command, AuthResult>
        {
            private readonly IAuthService _authService;
            private readonly IMapper _mapper;

            public Handler(IAuthService authService, IMapper mapper)
            {
                _authService = authService;
                _mapper = mapper;
            }

            public async Task<AuthResult> Handle(Command request, CancellationToken cancellationToken)
            {
                var (success, token, user, error) = await _authService.RegisterAsync(
                    request.Email, 
                    request.Password);

                var result = new AuthResult
                {
                    Success = success,
                    Token = token,
                    User = user != null ? _mapper.Map<UserDto>(user) : null,
                    Error = error
                };

                return result;
            }
        }
    }
} 