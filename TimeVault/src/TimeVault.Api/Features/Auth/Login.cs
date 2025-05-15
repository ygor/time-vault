using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using TimeVault.Core.Services.Interfaces;

namespace TimeVault.Api.Features.Auth
{
    public static class Login
    {
        public class Command : IRequest<AuthResult>
        {
            public string EmailOrUsername { get; set; }
            public string Password { get; set; }
        }

        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.EmailOrUsername).NotEmpty();
                RuleFor(x => x.Password).NotEmpty();
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
                var result = await _authService.LoginAsync(request.EmailOrUsername, request.Password);

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