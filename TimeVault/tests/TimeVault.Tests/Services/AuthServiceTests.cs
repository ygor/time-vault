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
using Microsoft.Extensions.DependencyInjection;

namespace TimeVault.Tests.Services
{
    public class AuthServiceTests
    {
        private readonly DbContextOptions<ApplicationDbContext> _contextOptions;
        private readonly IConfiguration _configuration;

        public AuthServiceTests()
        {
            // Use SQLite in-memory database for testing
            _contextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("AuthServiceTestDb")
                .Options;

            // Create test configuration
            var configValues = new Dictionary<string, string>
            {
                {"Jwt:Key", "your-256-bit-secret-key-used-to-sign-and-verify-jwt-token-super-secure"},
                {"Jwt:Issuer", "TimeVault"},
                {"Jwt:Audience", "TimeVaultUsers"}
            };

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();

            // Create database and apply migrations
            using var context = new ApplicationDbContext(_contextOptions);
            context.Database.EnsureCreated();
        }

        [Fact]
        public async Task RegisterAsync_ShouldCreateNewUser_WithAllRequiredFields()
        {
            // Arrange
            using var context = new ApplicationDbContext(_contextOptions);
            var authService = new AuthService(context, _configuration);
            
            var testEmail = "test@example.com";
            var testPassword = "StrongPassword!123";

            // Act
            var result = await authService.RegisterAsync(testEmail, testPassword);

            // Assert
            Assert.True(result.Success);
            Assert.NotEmpty(result.Token);
            Assert.NotNull(result.User);
            
            // Check that the user is in the database
            var userInDb = await context.Users.FirstOrDefaultAsync(u => u.Email == testEmail);
            Assert.NotNull(userInDb);
            
            // Ensure all required fields are populated
            Assert.NotEqual(Guid.Empty, userInDb.Id);
            Assert.Equal(testEmail, userInDb.Email);
            Assert.NotEmpty(userInDb.PasswordHash);
            Assert.Equal(string.Empty, userInDb.FirstName); // Default value
            Assert.Equal(string.Empty, userInDb.LastName); // Default value
            Assert.False(userInDb.IsAdmin);
            Assert.NotEqual(default, userInDb.CreatedAt);
            Assert.NotEqual(default, userInDb.UpdatedAt);
            Assert.NotNull(userInDb.LastLogin);
        }

        [Fact]
        public async Task RegisterAsync_ShouldReturnError_WhenEmailAlreadyExists()
        {
            // Arrange
            using var context = new ApplicationDbContext(_contextOptions);
            var authService = new AuthService(context, _configuration);
            
            var testEmail = "duplicate@example.com";
            var testPassword = "StrongPassword!123";
            
            // First registration should succeed
            await authService.RegisterAsync(testEmail, testPassword);

            // Act
            var result = await authService.RegisterAsync(testEmail, testPassword);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Email already registered", result.Error);
        }

        [Fact]
        public async Task LoginAsync_ShouldReturnUser_WhenCredentialsAreValid()
        {
            // Arrange
            using var context = new ApplicationDbContext(_contextOptions);
            var authService = new AuthService(context, _configuration);
            
            var testEmail = "login@example.com";
            var testPassword = "StrongPassword!123";
            
            // Register user first
            await authService.RegisterAsync(testEmail, testPassword);

            // Act
            var result = await authService.LoginAsync(testEmail, testPassword);

            // Assert
            Assert.True(result.Success);
            Assert.NotEmpty(result.Token);
            Assert.NotNull(result.User);
            Assert.Equal(testEmail, result.User.Email);
        }

        [Fact]
        public async Task LoginAsync_ShouldReturnError_WhenCredentialsAreInvalid()
        {
            // Arrange
            using var context = new ApplicationDbContext(_contextOptions);
            var authService = new AuthService(context, _configuration);
            
            var testEmail = "invalid@example.com";
            var testPassword = "StrongPassword!123";
            
            // Register user first
            await authService.RegisterAsync(testEmail, testPassword);

            // Act
            var result = await authService.LoginAsync(testEmail, "WrongPassword!123");

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Invalid password", result.Error);
        }

        [Fact]
        public async Task CreateAdminUserIfNotExists_ShouldCreateAdminUser_WhenEmailDoesNotExist()
        {
            // Arrange
            using var context = new ApplicationDbContext(_contextOptions);
            var authService = new AuthService(context, _configuration);
            
            var testEmail = "admin@example.com";
            var testPassword = "AdminPassword!123";

            // Act
            var result = await authService.CreateAdminUserIfNotExists(testEmail, testPassword);

            // Assert
            Assert.True(result);
            
            // Check that the admin user is in the database
            var adminInDb = await context.Users.FirstOrDefaultAsync(u => u.Email == testEmail);
            Assert.NotNull(adminInDb);
            Assert.Equal("Admin", adminInDb.FirstName);
            Assert.Equal("User", adminInDb.LastName);
            Assert.True(adminInDb.IsAdmin);
        }
    }
} 