using AutoMapper;
using FluentValidation;
using MediatR;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TimeVault.Core.Services.Interfaces;

namespace TimeVault.Api.Features.Auth
{
    public static class Login
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
                    .NotEmpty().WithMessage("Password is required");
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
                var (success, token, user, error) = await _authService.LoginAsync(request.Email, request.Password);

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