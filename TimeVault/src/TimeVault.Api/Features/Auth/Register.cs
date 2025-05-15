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
            public string Email { get; set; }
            public string Password { get; set; }
        }

        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.Email).NotEmpty().EmailAddress();
                RuleFor(x => x.Password).NotEmpty().MinimumLength(6).MaximumLength(100);
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
                var result = await _authService.RegisterAsync(request.Email, request.Password);

                if (!result.Success)
                    return new AuthResult { Success = false, Error = result.Error };

                var userDto = _mapper.Map<UserDto>(result.User);

                return new AuthResult
                {
                    Success = true,
                    Token = result.Token,
                    User = userDto,
                    Expiration = DateTime.UtcNow.AddDays(7) // Match JWT expiry in service
                };
            }
        }
    }
} 