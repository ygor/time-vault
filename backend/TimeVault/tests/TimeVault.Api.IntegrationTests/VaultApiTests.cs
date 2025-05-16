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
using System.Linq;

namespace TimeVault.Api.IntegrationTests
{
    public class VaultApiTests : IClassFixture<CustomWebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;
        private readonly CustomWebApplicationFactory<Program> _factory;
        private string _authToken = string.Empty;
        private Guid _userId;

        public VaultApiTests(CustomWebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
            
            // Authenticate before each test
            AuthenticateAsync().GetAwaiter().GetResult();
        }

        [Fact]
        public async Task CreateVault_WithValidData_ShouldReturnSuccess()
        {
            // Arrange
            var newVault = new
            {
                Name = "Test Vault " + DateTime.Now.Ticks,
                Description = "This is a test vault created via integration test"
            };

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);

            // Act
            var response = await _client.PostAsJsonAsync("/api/vaults", newVault);
            
            // Assert
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadFromJsonAsync<VaultResponse>();
            Assert.NotNull(content);
            Assert.NotNull(content.Id);
            Assert.Equal(newVault.Name, content.Name);
            Assert.Equal(newVault.Description, content.Description);
            Assert.Equal(_userId, content.OwnerId);
        }

        [Fact]
        public async Task GetVaults_ShouldReturnUserVaults()
        {
            // Arrange
            var client = _factory.CreateClient();
            var user = await RegisterTestUserAsync(client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
            
            // Create a vault first
            var vault = await CreateVaultAsync(client, "Test Vault", "Test Description");
            
            // Act
            var response = await client.GetAsync("/api/vaults");
            
            // Debug - Print the raw response content
            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Raw response content: {responseContent}");
            
            // Assert
            response.EnsureSuccessStatusCode();
            
            // The API returns an OkObjectResult wrapper around the actual list of vaults
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            var wrapper = await response.Content.ReadFromJsonAsync<OkObjectResultWrapper>(options);
            Assert.NotNull(wrapper);
            
            // Deserialize the "value" property which contains the actual list of vaults
            var valueJson = JsonSerializer.Serialize(wrapper.Value);
            var vaults = JsonSerializer.Deserialize<List<VaultDto>>(valueJson, options);
            
            Assert.NotNull(vaults);
            Assert.Contains(vaults, v => v.Id == vault.Id);
        }

        [Fact]
        public async Task GetVaultDetails_WithValidId_ShouldReturnVaultDetails()
        {
            // Arrange
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
            
            // Create a vault first
            var newVault = new
            {
                Name = "Test Vault for Details",
                Description = "This vault is for testing get details"
            };
            
            var createResponse = await _client.PostAsJsonAsync("/api/vaults", newVault);
            var createdVault = await createResponse.Content.ReadFromJsonAsync<VaultResponse>();
            
            // Act
            var response = await _client.GetAsync($"/api/vaults/{createdVault.Id}");
            
            // Assert
            response.EnsureSuccessStatusCode();
            var vault = await response.Content.ReadFromJsonAsync<VaultResponse>();
            
            Assert.NotNull(vault);
            Assert.Equal(createdVault.Id, vault.Id);
            Assert.Equal(newVault.Name, vault.Name);
            Assert.Equal(newVault.Description, vault.Description);
            Assert.True(vault.IsOwner);
        }

        [Fact]
        public async Task UpdateVault_WithValidData_ShouldUpdateVault()
        {
            // Arrange
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
            
            // Create a vault first
            var newVault = new
            {
                Name = "Test Vault for Update " + DateTime.Now.Ticks,
                Description = "This vault will be updated"
            };
            
            var createResponse = await _client.PostAsJsonAsync("/api/vaults", newVault);
            var createdVault = await createResponse.Content.ReadFromJsonAsync<VaultResponse>();
            
            // Update data
            var updateData = new
            {
                Name = "Updated Vault " + DateTime.Now.Ticks,
                Description = "This vault has been updated"
            };
            
            // Act
            var response = await _client.PutAsJsonAsync($"/api/vaults/{createdVault.Id}", updateData);
            
            // Assert
            response.EnsureSuccessStatusCode();
            
            // Verify the update by getting the vault details
            var getResponse = await _client.GetAsync($"/api/vaults/{createdVault.Id}");
            var updatedVault = await getResponse.Content.ReadFromJsonAsync<VaultResponse>();
            
            Assert.NotNull(updatedVault);
            Assert.Equal(createdVault.Id, updatedVault.Id);
            Assert.Equal(updateData.Name, updatedVault.Name);
            Assert.Equal(updateData.Description, updatedVault.Description);
        }

        [Fact]
        public async Task ShareVault_WithValidEmail_ShouldShareVault()
        {
            // Arrange
            await AuthenticateAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
            
            // Create a vault first
            var vaultName = $"Share Test Vault {DateTime.Now.Ticks}";
            var createVaultResponse = await _client.PostAsJsonAsync("/api/vaults", new
            {
                Name = vaultName,
                Description = "Test vault for sharing"
            });
            
            createVaultResponse.EnsureSuccessStatusCode();
            var vault = await createVaultResponse.Content.ReadFromJsonAsync<VaultResponse>();
            Assert.NotNull(vault);
            
            // Register another user to share with
            var targetEmail = $"share-target-{Guid.NewGuid()}@example.com";
            var targetUserResponse = await RegisterUserAsync(targetEmail, "P@ssw0rd123!");
            var targetUserId = targetUserResponse.User.Id;
            
            // Make sure we're still authenticated
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
            
            // Act - Share the vault
            var shareRequest = new
            {
                UserEmail = targetEmail,
                CanEdit = false
            };
            
            var response = await _client.PostAsJsonAsync($"/api/vaults/{vault.Id}/share", shareRequest);
            
            // Assert
            response.EnsureSuccessStatusCode();
            
            // Verify that the vault is now shared with the target user
            var vaultDetailsResponse = await _client.GetAsync($"/api/vaults/{vault.Id}");
            vaultDetailsResponse.EnsureSuccessStatusCode();
            
            var vaultDetails = await vaultDetailsResponse.Content.ReadFromJsonAsync<VaultResponse>();
            Assert.NotNull(vaultDetails);
            
            // Check that the SharedWith collection is not null and not empty
            Assert.NotNull(vaultDetails.SharedWith);
            Assert.NotEmpty(vaultDetails.SharedWith);
            
            // Check that the target user is present in the SharedWith collection
            var sharedUser = vaultDetails.SharedWith.FirstOrDefault(s => s.UserId == targetUserId);
            Assert.NotNull(sharedUser);
            Assert.Equal(targetEmail, sharedUser.Email);
            Assert.False(sharedUser.CanEdit);
        }

        [Fact]
        public async Task GetSharedVaults_ShouldReturnVaultsSharedWithUser()
        {
            // Arrange
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
            
            // Create a vault first
            var newVault = new
            {
                Name = "Test Vault for Sharing " + DateTime.Now.Ticks,
                Description = "This vault will be shared for testing"
            };
            
            var createResponse = await _client.PostAsJsonAsync("/api/vaults", newVault);
            var createdVault = await createResponse.Content.ReadFromJsonAsync<VaultResponse>();
            
            // Register a second user to share with
            var shareUserResponse = await RegisterUserAsync("shared-vaults-test@example.com", "SharedTest123!");
            var shareUserToken = shareUserResponse.Token;
            
            // Share the vault
            var shareRequest = new
            {
                UserEmail = "shared-vaults-test@example.com",
                CanEdit = false
            };
            
            await _client.PostAsJsonAsync($"/api/vaults/{createdVault.Id}/share", shareRequest);
            
            // Switch to the second user's context
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", shareUserToken);
            
            // Act
            var response = await _client.GetAsync("/api/vaults/shared");
            
            // Assert
            response.EnsureSuccessStatusCode();
            
            // Parse the response as a wrapper object that contains the array of vaults
            var responseContent = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            // Deserialize to an anonymous type with a "value" property
            var wrapper = System.Text.Json.JsonSerializer.Deserialize<OkObjectResultWrapper<List<VaultDto>>>(responseContent, options);
            
            // Extract the value property which contains the array of vaults
            var sharedVaults = wrapper.Value;
            
            Assert.NotNull(sharedVaults);
            Assert.NotEmpty(sharedVaults);
            Assert.Contains(sharedVaults, v => v.Id.Equals(createdVault.Id));
        }

        [Fact]
        public async Task DeleteVault_OwnedByUser_ShouldRemoveVault()
        {
            // Arrange
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
            
            // Create a vault first
            var newVault = new
            {
                Name = "Test Vault for Deletion " + DateTime.Now.Ticks,
                Description = "This vault will be deleted"
            };
            
            var createResponse = await _client.PostAsJsonAsync("/api/vaults", newVault);
            var createdVault = await createResponse.Content.ReadFromJsonAsync<VaultResponse>();
            
            // Act
            var response = await _client.DeleteAsync($"/api/vaults/{createdVault.Id}");
            
            // Assert
            response.EnsureSuccessStatusCode();
            
            // Verify by trying to get the vault - should return Not Found
            var getResponse = await _client.GetAsync($"/api/vaults/{createdVault.Id}");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }

        [Fact]
        public async Task AccessVault_NotOwnedOrShared_ShouldReturnForbidden()
        {
            // Arrange
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
            
            // Create a vault first
            var newVault = new
            {
                Name = "Test Vault for Authorization " + DateTime.Now.Ticks,
                Description = "This vault will be used to test authorization"
            };
            
            var createResponse = await _client.PostAsJsonAsync("/api/vaults", newVault);
            var createdVault = await createResponse.Content.ReadFromJsonAsync<VaultResponse>();
            
            // Create a second user who does not have access
            var unauthorizedUserResponse = await RegisterUserAsync("unauthorized@example.com", "Unauthorized123!");
            var unauthorizedToken = unauthorizedUserResponse.Token;
            
            // Switch to the unauthorized user's context
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", unauthorizedToken);
            
            // Act
            var response = await _client.GetAsync($"/api/vaults/{createdVault.Id}");
            
            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        #region Helper Methods

        private async Task AuthenticateAsync()
        {
            // Register a test user
            var authResponse = await RegisterUserAsync("vaulttest@example.com", "VaultTest123!");
            _authToken = authResponse.Token;
            _userId = authResponse.User.Id;
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

        private async Task<VaultResponse> CreateVaultAsync(HttpClient client, string name, string description)
        {
            var response = await client.PostAsJsonAsync("/api/vaults", new
            {
                Name = name,
                Description = description
            });
            
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<VaultResponse>();
        }

        private async Task<AuthResponse> RegisterTestUserAsync(HttpClient client)
        {
            var response = await client.PostAsJsonAsync("/api/auth/register", new
            {
                Email = "testuser@example.com",
                Password = "TestPassword123!"
            });
            
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AuthResponse>();
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
            public Guid Id { get; set; }
            public string Email { get; set; }
        }

        private class VaultResponse
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public Guid OwnerId { get; set; }
            public string OwnerEmail { get; set; }
            public bool IsOwner { get; set; }
            public bool CanEdit { get; set; }
            public List<SharedUserData> SharedWith { get; set; }
        }

        private class SharedUserData
        {
            public Guid UserId { get; set; }
            public string Email { get; set; }
            public bool CanEdit { get; set; }
            public DateTime SharedAt { get; set; }
        }

        // Add the VaultDto class to match the API response format
        private class VaultDto
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public Guid OwnerId { get; set; }
            public string OwnerEmail { get; set; }
            public bool IsOwner { get; set; }
            public bool CanEdit { get; set; }
            public List<VaultShareDto> SharedWith { get; set; }
        }

        private class VaultShareDto
        {
            public Guid UserId { get; set; }
            public string Email { get; set; }
            public bool CanEdit { get; set; }
            public DateTime SharedAt { get; set; }
        }

        // Add a wrapper class to handle OkObjectResult JSON format
        private class OkObjectResultWrapper<T>
        {
            public T Value { get; set; }
            public int StatusCode { get; set; }
        }

        // Add a class to represent the OkObjectResult wrapper
        private class OkObjectResultWrapper
        {
            public object Value { get; set; }
            public object[] Formatters { get; set; }
            public object[] ContentTypes { get; set; }
            public object DeclaredType { get; set; }
            public int StatusCode { get; set; }
        }

        #endregion
    }
} 