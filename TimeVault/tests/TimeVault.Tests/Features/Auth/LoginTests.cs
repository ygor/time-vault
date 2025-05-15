using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TimeVault.Api.Features.Auth;
using TimeVault.Api.Infrastructure.Mapping;
using TimeVault.Core.Services.Interfaces;
using TimeVault.Domain.Entities;
using Xunit;

namespace TimeVault.Tests.Features.Auth
{
    public class LoginTests
    {
        private readonly Mock<IAuthService> _mockAuthService;
        private readonly IMapper _mapper;
        private readonly IRequestHandler<Login.Command, AuthResult> _handler;

        public LoginTests()
        {
            // Set up AutoMapper with real mapping profile
            var mapperConfig = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile(new MappingProfile());
            });
            _mapper = mapperConfig.CreateMapper();

            // Set up mock auth service
            _mockAuthService = new Mock<IAuthService>();

            // Create handler with mocked dependencies
            _handler = new Login.Handler(_mockAuthService.Object, _mapper);
        }

        [Fact]
        public async Task Handle_ShouldReturnSuccessResult_WhenCredentialsAreValid()
        {
            // Arrange
            var command = new Login.Command
            {
                EmailOrUsername = "test@example.com",
                Password = "Password123!"
            };

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                Email = command.EmailOrUsername,
                CreatedAt = DateTime.UtcNow
            };

            _mockAuthService.Setup(x => x.LoginAsync(
                    command.EmailOrUsername,
                    command.Password))
                .ReturnsAsync((true, "token123", user, string.Empty));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            result.Token.Should().Be("token123");
            result.User.Should().NotBeNull();
            result.User.Email.Should().Be(command.EmailOrUsername);
        }

        [Fact]
        public async Task Handle_ShouldReturnFailureResult_WhenCredentialsAreInvalid()
        {
            // Arrange
            var command = new Login.Command
            {
                EmailOrUsername = "test@example.com",
                Password = "WrongPassword"
            };

            // Suppress nullability warnings for Moq setup methods
#pragma warning disable CS8620 // Argument cannot be used for parameter due to differences in nullability of reference types.
            _mockAuthService.Setup(x => x.LoginAsync(
                    command.EmailOrUsername,
                    command.Password))
                .ReturnsAsync((false, string.Empty, (User?)null, "Invalid password"));
#pragma warning restore CS8620

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Success.Should().BeFalse();
            result.Error.Should().Be("Invalid password");
        }
    }
} 