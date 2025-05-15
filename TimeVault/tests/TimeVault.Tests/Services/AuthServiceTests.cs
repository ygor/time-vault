using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using FluentAssertions;
using TimeVault.Domain.Entities;
using TimeVault.Infrastructure.Data;
using TimeVault.Infrastructure.Services;
using Xunit;

namespace TimeVault.Tests.Services
{
    public class AuthServiceTests
    {
        private readonly IConfiguration _configuration;

        public AuthServiceTests()
        {
            // Set up minimal configuration for JWT
            var inMemorySettings = new Dictionary<string, string?> {
                {"Jwt:Key", "this-is-a-very-long-secret-key-for-testing-purposes-only"},
                {"Jwt:Issuer", "time-vault-test"},
                {"Jwt:Audience", "time-vault-users-test"}
            };

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();
        }

        private ApplicationDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"AuthServiceTests_{Guid.NewGuid()}")
                .EnableSensitiveDataLogging() // Enable sensitive data logging
                .Options;

            var context = new ApplicationDbContext(options);
            return context;
        }

        [Fact]
        public async Task RegisterAsync_ShouldCreateUser_WithHashedPassword()
        {
            // Arrange
            using var context = CreateDbContext();
            var service = new AuthService(context, _configuration);
            var email = "test@example.com";
            var password = "Password123!";

            // Act
            var result = await service.RegisterAsync(email, password);

            // Assert
            result.Success.Should().BeTrue();
            result.User.Should().NotBeNull();
            result.User.Email.Should().Be(email);
            result.User.PasswordHash.Should().NotBeNullOrEmpty();
            result.User.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));

            var savedUser = await context.Users.FindAsync(result.User.Id);
            savedUser.Should().NotBeNull();
            savedUser!.Email.Should().Be(email);
            savedUser.PasswordHash.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task LoginAsync_ShouldReturnSuccessAndToken_WhenCredentialsAreCorrect()
        {
            // Arrange
            using var context = CreateDbContext();
            var service = new AuthService(context, _configuration);
            var email = "test@example.com";
            var password = "Password123!";

            // Register a user first
            await service.RegisterAsync(email, password);

            // Act - login with email
            var result = await service.LoginAsync(email, password);

            // Assert
            result.Success.Should().BeTrue();
            result.Error.Should().BeEmpty();
            result.Token.Should().NotBeNullOrEmpty();
            result.User.Should().NotBeNull();
            result.User.Email.Should().Be(email);
        }

        [Fact]
        public async Task LoginAsync_ShouldReturnFailure_WhenEmailIsIncorrect()
        {
            // Arrange
            using var context = CreateDbContext();
            var service = new AuthService(context, _configuration);
            var email = "test@example.com";
            var password = "Password123!";

            // Register a user first
            await service.RegisterAsync(email, password);

            // Act
            var result = await service.LoginAsync("wrong@example.com", password);

            // Assert
            result.Success.Should().BeFalse();
            result.Error.Should().NotBeNullOrEmpty();
            result.Error.Should().Be("User not found");
        }

        [Fact]
        public async Task LoginAsync_ShouldReturnFailure_WhenPasswordIsIncorrect()
        {
            // Arrange
            using var context = CreateDbContext();
            var service = new AuthService(context, _configuration);
            var email = "test@example.com";
            var password = "Password123!";

            // Register a user first
            await service.RegisterAsync(email, password);

            // Act
            var result = await service.LoginAsync(email, "wrongpassword");

            // Assert
            result.Success.Should().BeFalse();
            result.Error.Should().NotBeNullOrEmpty();
            result.Error.Should().Be("Invalid password");
        }

        [Fact]
        public async Task ChangePasswordAsync_ShouldReturnTrue_WhenCurrentPasswordIsCorrect()
        {
            // Arrange
            using var context = CreateDbContext();
            var service = new AuthService(context, _configuration);
            var email = "test@example.com";
            var currentPassword = "Password123!";
            var newPassword = "NewPassword123!";

            // Register a user first
            var registerResult = await service.RegisterAsync(email, currentPassword);
            var userId = registerResult.User.Id;

            // Act
            var result = await service.ChangePasswordAsync(userId, currentPassword, newPassword);

            // Assert
            result.Should().BeTrue();

            // Verify we can login with the new password
            var loginResult = await service.LoginAsync(email, newPassword);
            loginResult.Success.Should().BeTrue();
        }
    }
} 