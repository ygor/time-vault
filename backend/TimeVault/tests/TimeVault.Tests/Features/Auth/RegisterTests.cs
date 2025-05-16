using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TimeVault.Api.Features.Auth;
using TimeVault.Api.Features.Auth.Mapping;
using TimeVault.Core.Services.Interfaces;
using TimeVault.Domain.Entities;
using Xunit;

namespace TimeVault.Tests.Features.Auth
{
    public class RegisterTests
    {
        private readonly Mock<IAuthService> _mockAuthService;
        private readonly IMapper _mapper;
        private readonly IRequestHandler<Register.Command, AuthResult> _handler;

        public RegisterTests()
        {
            // Set up AutoMapper with feature-specific mapping profile
            var mapperConfig = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile(new AuthMappingProfile());
            });
            _mapper = mapperConfig.CreateMapper();

            // Set up mock auth service
            _mockAuthService = new Mock<IAuthService>();

            // Create handler with mocked dependencies
            _handler = new Register.Handler(_mockAuthService.Object, _mapper);
        }

        [Fact]
        public async Task Handle_ShouldReturnSuccessResult_WhenRegistrationSucceeds()
        {
            // Arrange
            var command = new Register.Command
            {
                Email = "test@example.com",
                Password = "Password123!"
            };

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = command.Email,
                CreatedAt = DateTime.UtcNow
            };

            _mockAuthService.Setup(x => x.RegisterAsync(
                    command.Email,
                    command.Password))
                .ReturnsAsync((true, "token123", user, string.Empty));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            result.Token.Should().Be("token123");
            result.User.Should().NotBeNull();
            result.User.Email.Should().Be(command.Email);
        }

        [Fact]
        public async Task Handle_ShouldReturnFailureResult_WhenRegistrationFails()
        {
            // Arrange
            var command = new Register.Command
            {
                Email = "existing@example.com",
                Password = "Password123!"
            };

            // Suppress nullability warnings for Moq setup methods
#pragma warning disable CS8620 // Argument cannot be used for parameter due to differences in nullability of reference types.
            _mockAuthService.Setup(x => x.RegisterAsync(
                    command.Email,
                    command.Password))
                .ReturnsAsync((false, string.Empty, (User?)null, "Email already registered"));
#pragma warning restore CS8620

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Success.Should().BeFalse();
            result.Error.Should().Be("Email already registered");
        }
    }
} 