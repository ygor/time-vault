using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using TimeVault.Domain.Entities;
using Xunit;

namespace TimeVault.Api.IntegrationTests
{
    public class ErrorHandlingTests : IClassFixture<CustomWebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;
        private readonly CustomWebApplicationFactory<Program> _factory;
        private string _authToken = string.Empty;

        public ErrorHandlingTests(CustomWebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            
            // Authenticate before each test
            AuthenticateAsync().GetAwaiter().GetResult();
        }

        [Fact]
        public async Task InvalidRegistration_WithExistingEmail_ShouldReturnError()
        {
            // Arrange
            var existingEmail = "exists-test@example.com";
            var password = "Password123!";
            
            // Create a user first
            await RegisterUserAsync(existingEmail, password);
            
            // Attempt to register the same email again
            var registerRequest = new
            {
                Email = existingEmail,
                Password = password
            };
            
            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
            
            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            Assert.NotNull(content);
            Assert.False(content.Success);
            Assert.Contains("already registered", content.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task InvalidLogin_WithNonexistentUser_ShouldReturnError()
        {
            // Arrange
            var loginRequest = new
            {
                Email = "nonexistent@example.com",
                Password = "Password123!"
            };
            
            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
            
            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            Assert.NotNull(content);
            Assert.False(content.Success);
            Assert.Contains("user not found", content.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task InvalidLogin_WithIncorrectPassword_ShouldReturnError()
        {
            // Arrange
            var email = "valid-user@example.com";
            var password = "CorrectPassword123!";
            
            // Create a user first
            await RegisterUserAsync(email, password);
            
            var loginRequest = new
            {
                Email = email,
                Password = "WrongPassword123!"
            };
            
            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
            
            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            Assert.NotNull(content);
            Assert.False(content.Success);
            Assert.Contains("invalid password", content.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AccessProtectedRoute_WithoutToken_ShouldReturnUnauthorized()
        {
            // Arrange - No auth token set
            
            // Act
            var response = await _client.GetAsync("/api/vaults");
            
            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task AccessProtectedRoute_WithInvalidToken_ShouldReturnUnauthorized()
        {
            // Arrange
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid_token_string");
            
            // Act
            var response = await _client.GetAsync("/api/vaults");
            
            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task CreateVault_WithInvalidData_ShouldReturnValidationError()
        {
            // Arrange
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
            
            // Empty name (which should be required)
            var invalidVault = new
            {
                Name = "",
                Description = "This vault has an invalid empty name"
            };
            
            // Act
            var response = await _client.PostAsJsonAsync("/api/vaults", invalidVault);
            
            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<ValidationErrorResponse>();
            Assert.NotNull(content);
            Assert.False(content.Success);
            Assert.NotEmpty(content.Errors);
        }

        [Fact]
        public async Task GetVault_WithNonexistentId_ShouldReturnNotFound()
        {
            // Arrange
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
            
            // Generate a random GUID that doesn't exist
            var nonExistentId = Guid.NewGuid().ToString();
            
            // Act
            var response = await _client.GetAsync($"/api/vaults/{nonExistentId}");
            
            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task CreateMessage_WithInvalidUnlockTime_ShouldReturnValidationError()
        {
            // Arrange
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
            
            // Create a vault first
            var newVault = new
            {
                Name = "Test Vault for Invalid Message " + DateTime.Now.Ticks,
                Description = "This vault will be used to test validation errors"
            };
            
            var vaultResponse = await _client.PostAsJsonAsync("/api/vaults", newVault);
            var vault = await vaultResponse.Content.ReadFromJsonAsync<VaultResponse>();
            
            // Create an invalid message with malformed unlock time
            var invalidMessage = new
            {
                Title = "Invalid Message",
                Content = "This message has an invalid unlock time",
                UnlockTime = "not-a-date"
            };
            
            // Act
            var response = await _client.PostAsJsonAsync($"/api/messages/vault/{vault.Id}", invalidMessage);
            
            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task ShareVault_WithNonexistentEmail_ShouldReturnError()
        {
            // Arrange
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
            
            // Create a vault first
            var newVault = new
            {
                Name = "Test Vault for Invalid Share " + DateTime.Now.Ticks,
                Description = "This vault will be used to test sharing errors"
            };
            
            var vaultResponse = await _client.PostAsJsonAsync("/api/vaults", newVault);
            var vault = await vaultResponse.Content.ReadFromJsonAsync<VaultResponse>();
            
            // Share with non-existent email
            var shareRequest = new
            {
                UserEmail = "nonexistent-user@example.com",
                CanEdit = false
            };
            
            // Act
            var response = await _client.PostAsJsonAsync($"/api/vaults/{vault.Id}/share", shareRequest);
            
            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            Assert.NotNull(content);
            Assert.False(content.Success);
        }

        [Fact]
        public async Task RevokeShare_FromUnsharedVault_ShouldReturnError()
        {
            // Arrange
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
            
            // Create a vault first
            var newVault = new
            {
                Name = "Test Vault for Invalid Revoke " + DateTime.Now.Ticks,
                Description = "This vault will be used to test revoking errors"
            };
            
            var vaultResponse = await _client.PostAsJsonAsync("/api/vaults", newVault);
            var vault = await vaultResponse.Content.ReadFromJsonAsync<VaultResponse>();
            
            // Attempt to revoke a non-existent share
            var nonExistentUserId = Guid.NewGuid().ToString();
            
            // Act
            var response = await _client.DeleteAsync($"/api/vaults/{vault.Id}/share/{nonExistentUserId}");
            
            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task UpdateMessage_WithOverlongContent_ShouldReturnValidationError()
        {
            // Arrange
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
            
            // Create a vault first
            var newVault = new
            {
                Name = "Test Vault for Validation " + DateTime.Now.Ticks,
                Description = "This vault will be used to test validation limits"
            };
            
            var vaultResponse = await _client.PostAsJsonAsync("/api/vaults", newVault);
            var vault = await vaultResponse.Content.ReadFromJsonAsync<VaultResponse>();
            
            // Create a valid message first
            var newMessage = new
            {
                Title = "Test Message",
                Content = "Initial content",
                UnlockTime = DateTime.UtcNow.AddDays(-1).ToString("o")
            };
            
            var messageResponse = await _client.PostAsJsonAsync($"/api/messages/vault/{vault.Id}", newMessage);
            var message = await messageResponse.Content.ReadFromJsonAsync<MessageResponse>();
            
            // Create an update with extremely long content
            var extremelyLongContent = new string('X', 1000000); // 1 million characters
            var updateData = new
            {
                Title = "Updated Message",
                Content = extremelyLongContent
            };
            
            // Act
            var response = await _client.PutAsJsonAsync($"/api/messages/{message.Id}", updateData);
            
            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        #region Helper Methods

        private async Task AuthenticateAsync()
        {
            // Register a test user
            var authResponse = await RegisterUserAsync("errortest@example.com", "ErrorTest123!");
            _authToken = authResponse.Token;
        }

        private async Task<AuthResponse> RegisterUserAsync(string email, string password)
        {
            var registerRequest = new
            {
                Email = email,
                Password = password
            };

            // Use StringContent with proper JSON serialization
            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(registerRequest),
                Encoding.UTF8,
                "application/json");

            var response = await _client.PostAsync("/api/auth/register", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                return System.Text.Json.JsonSerializer.Deserialize<AuthResponse>(
                    responseString, 
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            else
            {
                // If registration fails (e.g., user already exists), try login
                var loginContent = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(registerRequest),
                    Encoding.UTF8,
                    "application/json");
                    
                var loginResponse = await _client.PostAsync("/api/auth/login", loginContent);
                
                if (loginResponse.IsSuccessStatusCode)
                {
                    var responseString = await loginResponse.Content.ReadAsStringAsync();
                    return System.Text.Json.JsonSerializer.Deserialize<AuthResponse>(
                        responseString, 
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                
                // If both registration and login fail, return a minimal response to continue the test
                return new AuthResponse { Success = false, Token = "invalid-token" };
            }
        }

        #endregion

        #region Response Models

        private class AuthResponse
        {
            public bool Success { get; set; }
            public string Token { get; set; }
            public UserData User { get; set; }
        }

        private class UserData
        {
            public string Id { get; set; }
            public string Email { get; set; }
        }

        private class VaultResponse
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
        }

        private class MessageResponse
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string Content { get; set; }
        }

        private class ErrorResponse
        {
            public bool Success { get; set; }
            public string Error { get; set; }
        }

        private class ValidationErrorResponse
        {
            public bool Success { get; set; }
            public List<string> Errors { get; set; }
        }

        #endregion
    }
} 