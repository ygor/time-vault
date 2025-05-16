using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimeVault.Domain.Entities;
using TimeVault.Infrastructure.Data;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http;
using System.Net.Http.Json;
using TimeVault.Api;

namespace TimeVault.Api.IntegrationTests;

public class DatabaseColumnCaseTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public DatabaseColumnCaseTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CanQueryUserByEmail_CaseInsensitive()
    {
        // Arrange
        string email = "test-case-sensitivity@example.com";
        var user = new User 
        { 
            Id = Guid.NewGuid(),
            Email = email,
            FirstName = "Test",
            LastName = "User",
            PasswordHash = "hash",
            IsAdmin = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();

            // Act & Assert - Query with exact case
            var foundUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
            Assert.NotNull(foundUser);
            Assert.Equal(user.Id, foundUser.Id);

            // Query with different case
            var foundUserDifferentCase = await dbContext.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToUpper().ToLower());
            Assert.NotNull(foundUserDifferentCase);
            Assert.Equal(user.Id, foundUserDifferentCase.Id);

            // Query by ID
            var foundById = await dbContext.Users.FindAsync(user.Id);
            Assert.NotNull(foundById);
            Assert.Equal(email, foundById.Email);
        }
    }
    
    [Fact]
    public async Task CanCreateAndRetrieveUserWithAllProperties()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "all-properties@example.com",
            FirstName = "All",
            LastName = "Properties",
            PasswordHash = "secure-hash",
            IsAdmin = true,
            CreatedAt = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2023, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            LastLogin = new DateTime(2023, 1, 3, 0, 0, 0, DateTimeKind.Utc)
        };

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();

            // Act
            var retrievedUser = await dbContext.Users.FindAsync(user.Id);

            // Assert
            Assert.NotNull(retrievedUser);
            Assert.Equal(user.Email, retrievedUser.Email);
            Assert.Equal(user.FirstName, retrievedUser.FirstName);
            Assert.Equal(user.LastName, retrievedUser.LastName);
            Assert.Equal(user.PasswordHash, retrievedUser.PasswordHash);
            Assert.Equal(user.IsAdmin, retrievedUser.IsAdmin);
            Assert.Equal(user.CreatedAt, retrievedUser.CreatedAt);
            Assert.Equal(user.UpdatedAt, retrievedUser.UpdatedAt);
            Assert.Equal(user.LastLogin, retrievedUser.LastLogin);
        }
    }

    [Fact]
    public async Task CanCreateAndQueryVault_WithCaseInsensitiveColumns()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "vault-owner@example.com",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var vault = new Vault
        {
            Id = Guid.NewGuid(),
            Name = "Test Vault",
            Description = "A vault for testing case sensitivity",
            OwnerId = user.Id,
            PublicKey = "test-public-key",
            EncryptedPrivateKey = "test-encrypted-private-key",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Users.Add(user);
            dbContext.Vaults.Add(vault);
            await dbContext.SaveChangesAsync();

            // Act & Assert

            // Test querying by ID
            var foundVault = await dbContext.Vaults.FindAsync(vault.Id);
            Assert.NotNull(foundVault);
            Assert.Equal(vault.Name, foundVault.Name);

            // Test querying by owner ID
            var vaultsByOwner = await dbContext.Vaults
                .Where(v => v.OwnerId == user.Id)
                .ToListAsync();
            Assert.Single(vaultsByOwner);
            Assert.Equal(vault.Id, vaultsByOwner.First().Id);

            // Test join query with User
            var vaultWithOwner = await dbContext.Vaults
                .Include(v => v.Owner)
                .FirstOrDefaultAsync(v => v.Id == vault.Id);
            Assert.NotNull(vaultWithOwner);
            Assert.NotNull(vaultWithOwner.Owner);
            Assert.Equal(user.Id, vaultWithOwner.Owner.Id);
            Assert.Equal(user.Email, vaultWithOwner.Owner.Email);

            // Test with predicates that would be affected by column case
            var foundByNameAndOwner = await dbContext.Vaults
                .Where(v => v.Name == vault.Name && v.OwnerId == user.Id)
                .FirstOrDefaultAsync();
            Assert.NotNull(foundByNameAndOwner);
            
            // Verify all properties are correctly mapped
            Assert.Equal(vault.Id, foundByNameAndOwner.Id);
            Assert.Equal(vault.Name, foundByNameAndOwner.Name);
            Assert.Equal(vault.Description, foundByNameAndOwner.Description);
            Assert.Equal(vault.OwnerId, foundByNameAndOwner.OwnerId);
            Assert.Equal(vault.PublicKey, foundByNameAndOwner.PublicKey);
            Assert.Equal(vault.EncryptedPrivateKey, foundByNameAndOwner.EncryptedPrivateKey);
            Assert.Equal(vault.CreatedAt, foundByNameAndOwner.CreatedAt);
            Assert.Equal(vault.UpdatedAt, foundByNameAndOwner.UpdatedAt);
        }
    }

    [Fact]
    public async Task CanCreateAndQueryMessageAndVaultShare_WithCaseInsensitiveColumns()
    {
        // Arrange
        var owner = new User
        {
            Id = Guid.NewGuid(),
            Email = "vault-share-owner@example.com",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var sharedUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "vault-share-user@example.com",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var vault = new Vault
        {
            Id = Guid.NewGuid(),
            Name = "Shared Vault",
            Description = "A shared vault for testing",
            OwnerId = owner.Id,
            PublicKey = "test-public-key",
            EncryptedPrivateKey = "test-encrypted-private-key",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var vaultShare = new VaultShare
        {
            Id = Guid.NewGuid(),
            VaultId = vault.Id,
            UserId = sharedUser.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CanEdit = true
        };

        var message = new Message
        {
            Id = Guid.NewGuid(),
            Title = "Test Message",
            Content = "Test message for case sensitivity",
            VaultId = vault.Id,
            SenderId = owner.Id,
            IsEncrypted = false,
            IsTlockEncrypted = false,
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Users.AddRange(owner, sharedUser);
            dbContext.Vaults.Add(vault);
            dbContext.VaultShares.Add(vaultShare);
            dbContext.Messages.Add(message);
            await dbContext.SaveChangesAsync();

            // Act & Assert - Test VaultShare queries
            var foundShare = await dbContext.VaultShares
                .Include(vs => vs.User)
                .Include(vs => vs.Vault)
                .FirstOrDefaultAsync(vs => vs.VaultId == vault.Id && vs.UserId == sharedUser.Id);
            
            Assert.NotNull(foundShare);
            Assert.Equal(vaultShare.Id, foundShare.Id);
            Assert.NotNull(foundShare.User);
            Assert.Equal(sharedUser.Email, foundShare.User.Email);
            Assert.NotNull(foundShare.Vault);
            Assert.Equal(vault.Name, foundShare.Vault.Name);

            // Test Message queries
            var foundMessage = await dbContext.Messages
                .Include(m => m.Sender)
                .Include(m => m.Vault)
                .FirstOrDefaultAsync(m => m.VaultId == vault.Id);
            
            Assert.NotNull(foundMessage);
            Assert.Equal(message.Id, foundMessage.Id);
            Assert.Equal(message.Content, foundMessage.Content);
            Assert.NotNull(foundMessage.Sender);
            Assert.Equal(owner.Email, foundMessage.Sender.Email);
            Assert.NotNull(foundMessage.Vault);
            Assert.Equal(vault.Name, foundMessage.Vault.Name);

            // Test complex query joining multiple tables
            var messagesBySharedVaults = await dbContext.Messages
                .Include(m => m.Vault)
                .Where(m => m.VaultId == vault.Id &&
                            dbContext.VaultShares.Any(vs => 
                                vs.VaultId == m.VaultId && 
                                vs.UserId == sharedUser.Id))
                .ToListAsync();
            
            Assert.Single(messagesBySharedVaults);
            Assert.Equal(message.Id, messagesBySharedVaults.First().Id);
        }
    }
} 