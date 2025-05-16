using System;
using System.Collections.Generic;
using System.Linq;
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
    public class MessageApiTests : IClassFixture<CustomWebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;
        private readonly CustomWebApplicationFactory<Program> _factory;
        private string _authToken = string.Empty;
        private string _userId = string.Empty;
        private string _vaultId = string.Empty;

        public MessageApiTests(CustomWebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
            
            // Initialize a user and vault for testing
            InitializeAsync().GetAwaiter().GetResult();
        }

        [Fact]
        public async Task AddMessage_ToVault_ShouldCreateMessage()
        {
            // Arrange
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
            
            var newMessage = new
            {
                Title = "Test Message " + DateTime.Now.Ticks,
                Content = "This is a test message created via integration test",
                UnlockTime = DateTime.UtcNow.AddDays(-1).ToString("o") // Unlocked immediately
            };

            // Act
            var response = await _client.PostAsJsonAsync($"/api/messages/vault/{_vaultId}", newMessage);
            
            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadFromJsonAsync<MessageResponse>();
            Assert.NotNull(content);
            Assert.NotNull(content.Id);
            Assert.Equal(newMessage.Title, content.Title);
            Assert.Equal(newMessage.Content, content.Content);
            Assert.Equal(_vaultId, content.VaultId);
        }

        [Fact]
        public async Task GetMessages_ForVault_ShouldReturnAllMessages()
        {
            // Arrange
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
            
            // Create a message first
            var newMessage = new
            {
                Title = "Test Message for Listing " + DateTime.Now.Ticks,
                Content = "This message should be included in the list",
                UnlockTime = DateTime.UtcNow.AddDays(-1).ToString("o") // Unlocked immediately
            };
            
            await _client.PostAsJsonAsync($"/api/messages/vault/{_vaultId}", newMessage);
            
            // Act
            var response = await _client.GetAsync($"/api/messages/vault/{_vaultId}");
            
            // Assert
            response.EnsureSuccessStatusCode();
            var messages = await response.Content.ReadFromJsonAsync<List<MessageResponse>>();
            
            Assert.NotNull(messages);
            Assert.NotEmpty(messages);
            Assert.Contains(messages, m => m.Title == newMessage.Title);
        }

        [Fact]
        public async Task GetMessageDetails_WithValidId_ShouldReturnMessage()
        {
            // Arrange
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
            
            // Create a message first
            var newMessage = new
            {
                Title = "Test Message for Details " + DateTime.Now.Ticks,
                Content = "This message is for testing details retrieval",
                UnlockTime = DateTime.UtcNow.AddDays(-1).ToString("o") // Unlocked immediately
            };
            
            var createResponse = await _client.PostAsJsonAsync($"/api/messages/vault/{_vaultId}", newMessage);
            var createdMessage = await createResponse.Content.ReadFromJsonAsync<MessageResponse>();
            
            // Act
            var response = await _client.GetAsync($"/api/messages/{createdMessage.Id}");
            
            // Assert
            response.EnsureSuccessStatusCode();
            var message = await response.Content.ReadFromJsonAsync<MessageResponse>();
            
            Assert.NotNull(message);
            Assert.Equal(createdMessage.Id, message.Id);
            Assert.Equal(newMessage.Title, message.Title);
            Assert.Equal(newMessage.Content, message.Content);
            Assert.Equal(_vaultId, message.VaultId);
        }

        [Fact]
        public async Task UpdateMessage_WithValidData_ShouldUpdateMessage()
        {
            // Arrange
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
            
            // Create a message first
            var newMessage = new
            {
                Title = "Test Message for Update " + DateTime.Now.Ticks,
                Content = "This message will be updated",
                UnlockTime = DateTime.UtcNow.AddDays(-1).ToString("o") // Unlocked immediately
            };
            
            var createResponse = await _client.PostAsJsonAsync($"/api/messages/vault/{_vaultId}", newMessage);
            var createdMessage = await createResponse.Content.ReadFromJsonAsync<MessageResponse>();
            
            // Update data
            var updateData = new
            {
                Title = "Updated Message " + DateTime.Now.Ticks,
                Content = "This message has been updated"
            };
            
            // Act
            var response = await _client.PutAsJsonAsync($"/api/messages/{createdMessage.Id}", updateData);
            
            // Assert
            response.EnsureSuccessStatusCode();
            
            // Verify the update by getting the message details
            var getResponse = await _client.GetAsync($"/api/messages/{createdMessage.Id}");
            var updatedMessage = await getResponse.Content.ReadFromJsonAsync<MessageResponse>();
            
            Assert.NotNull(updatedMessage);
            Assert.Equal(createdMessage.Id, updatedMessage.Id);
            Assert.Equal(updateData.Title, updatedMessage.Title);
            Assert.Equal(updateData.Content, updatedMessage.Content);
        }

        [Fact]
        public async Task DeleteMessage_OwnedByUser_ShouldRemoveMessage()
        {
            // Arrange
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
            
            // Create a message first
            var newMessage = new
            {
                Title = "Test Message for Deletion " + DateTime.Now.Ticks,
                Content = "This message will be deleted",
                UnlockTime = DateTime.UtcNow.AddDays(-1).ToString("o") // Unlocked immediately
            };
            
            var createResponse = await _client.PostAsJsonAsync($"/api/messages/vault/{_vaultId}", newMessage);
            var createdMessage = await createResponse.Content.ReadFromJsonAsync<MessageResponse>();
            
            // Act
            var response = await _client.DeleteAsync($"/api/messages/{createdMessage.Id}");
            
            // Assert
            response.EnsureSuccessStatusCode();
            
            // Verify by trying to get the message - should return Not Found
            var getResponse = await _client.GetAsync($"/api/messages/{createdMessage.Id}");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }

        [Fact]
        public async Task CreateTimeLocked_Message_ShouldBeRetrievableButLocked()
        {
            // Arrange
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
            
            // Create a time-locked message set to unlock in the future
            var newMessage = new
            {
                Title = "Future Time-Locked Message " + DateTime.Now.Ticks,
                Content = "This content should be locked until the future date",
                UnlockTime = DateTime.UtcNow.AddDays(7).ToString("o") // Set to 7 days in the future
            };
            
            // Act
            var createResponse = await _client.PostAsJsonAsync($"/api/messages/vault/{_vaultId}", newMessage);
            var createdMessage = await createResponse.Content.ReadFromJsonAsync<MessageResponse>();
            
            // Get the message details
            var getResponse = await _client.GetAsync($"/api/messages/{createdMessage.Id}");
            var message = await getResponse.Content.ReadFromJsonAsync<MessageResponse>();
            
            // Assert
            Assert.NotNull(message);
            Assert.Equal(createdMessage.Id, message.Id);
            Assert.Equal(newMessage.Title, message.Title);
            
            // The message should be locked, so the content may be empty or encrypted
            // Depending on how the API is implemented, this check might vary
            Assert.True(message.IsLocked || string.IsNullOrEmpty(message.Content));
        }

        [Fact]
        public async Task GetUnlockedMessages_ShouldOnlyReturnUnlockedMessages()
        {
            // Arrange
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
            
            // Create one unlocked message
            var unlockedMessage = new
            {
                Title = "Unlocked Message " + DateTime.Now.Ticks,
                Content = "This is an immediately available message",
                UnlockTime = DateTime.UtcNow.AddDays(-1).ToString("o") // Unlocked immediately
            };
            
            // Create one locked message
            var lockedMessage = new
            {
                Title = "Locked Message " + DateTime.Now.Ticks,
                Content = "This message is time-locked",
                UnlockTime = DateTime.UtcNow.AddDays(7).ToString("o") // Locked for 7 days
            };
            
            // Add both messages to the vault
            await _client.PostAsJsonAsync($"/api/messages/vault/{_vaultId}", unlockedMessage);
            await _client.PostAsJsonAsync($"/api/messages/vault/{_vaultId}", lockedMessage);
            
            // Act
            var response = await _client.GetAsync("/api/messages/unlocked");
            
            // Assert
            response.EnsureSuccessStatusCode();
            var messages = await response.Content.ReadFromJsonAsync<List<MessageResponse>>();
            
            Assert.NotNull(messages);
            
            // We should find the unlocked message
            var foundUnlocked = messages.Any(m => m.Title == unlockedMessage.Title);
            Assert.True(foundUnlocked, "The unlocked message should be returned");
            
            // We should NOT find the locked message
            var foundLocked = messages.Any(m => m.Title == lockedMessage.Title);
            Assert.False(foundLocked, "The locked message should not be returned");
        }
        
        [Fact]
        public async Task AccessMessage_InUnsharedVault_ShouldReturnForbidden()
        {
            // Arrange - Create a new user and their vault
            var otherUserEmail = $"other-user-{Guid.NewGuid()}@example.com";
            var otherUserPassword = "OtherUserP@ss123!";
            
            // Register another user and authenticate as them
            var otherUserAuth = await RegisterUserAsync(otherUserEmail, otherUserPassword);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherUserAuth.Token);
            
            // Create a vault for this other user
            var createVaultResponse = await _client.PostAsJsonAsync("/api/vaults", new
            {
                Name = "Other User's Private Vault",
                Description = "This vault is not shared with the test user"
            });
            
            createVaultResponse.EnsureSuccessStatusCode();
            var otherUserVault = await createVaultResponse.Content.ReadFromJsonAsync<VaultResponse>();
            Assert.NotNull(otherUserVault);
            
            // Create a message in this vault
            var createMessageResponse = await _client.PostAsJsonAsync($"/api/messages/vault/{otherUserVault.Id}", new
            {
                Title = "Private Message",
                Content = "This message should not be accessible to other users",
                UnlockTime = DateTime.UtcNow.ToString("o") // Use ISO 8601 format
            });
            
            createMessageResponse.EnsureSuccessStatusCode();
            var message = await createMessageResponse.Content.ReadFromJsonAsync<MessageResponse>();
            Assert.NotNull(message);
            
            // Now switch back to our original test user
            await AuthenticateAsync();
            
            // Act - Try to access the message as the original user
            var response = await _client.GetAsync($"/api/messages/{message.Id}");
            
            // Assert
            // The API might return NotFound for security reasons instead of exposing existence
            // Accept either Forbidden (403) or NotFound (404) as valid responses for security
            Assert.True(
                response.StatusCode == HttpStatusCode.Forbidden || 
                response.StatusCode == HttpStatusCode.NotFound,
                $"Expected Forbidden or NotFound, but got {response.StatusCode}"
            );
        }

        #region Helper Methods

        private async Task InitializeAsync()
        {
            // Register a test user
            var authResponse = await RegisterUserAsync("messagetest@example.com", "MessageTest123!");
            _authToken = authResponse.Token;
            _userId = authResponse.User.Id;
            
            // Create a test vault
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
            
            var newVault = new
            {
                Name = "Test Message Vault " + DateTime.Now.Ticks,
                Description = "Vault for testing messages"
            };
            
            var vaultResponse = await _client.PostAsJsonAsync("/api/vaults", newVault);
            var vault = await vaultResponse.Content.ReadFromJsonAsync<VaultResponse>();
            _vaultId = vault.Id;
        }

        private async Task<AuthResponse> RegisterUserAsync(string email, string password)
        {
            var registerRequest = new
            {
                Email = email,
                Password = password
            };

            var response = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<AuthResponse>();
            }
            else
            {
                // If registration fails (e.g., user already exists), try login
                var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", registerRequest);
                return await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
            }
        }

        private async Task AuthenticateAsync()
        {
            // Implement the logic to authenticate the original test user
            // This might involve re-logging in or refreshing the token
            // For simplicity, we'll just re-register the user
            await InitializeAsync();
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
            public string OwnerId { get; set; }
        }

        private class MessageResponse
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string Content { get; set; }
            public string VaultId { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? UnlockDateTime { get; set; }
            public bool IsLocked { get; set; }
            public bool IsRead { get; set; }
        }

        #endregion
    }
} 