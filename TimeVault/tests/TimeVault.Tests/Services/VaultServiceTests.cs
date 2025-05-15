using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using TimeVault.Core.Services.Interfaces;
using TimeVault.Domain.Entities;
using TimeVault.Infrastructure.Data;
using TimeVault.Infrastructure.Services;
using Xunit;

namespace TimeVault.Tests.Services
{
    public class VaultServiceTests
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly VaultService _vaultService;
        private readonly Mock<IKeyVaultService> _mockKeyVaultService;
        private readonly Guid _testUserId;
        private readonly Guid _otherUserId;

        public VaultServiceTests()
        {
            // Set up in-memory database for testing
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .EnableSensitiveDataLogging()
                .Options;

            _dbContext = new ApplicationDbContext(options);
            _mockKeyVaultService = new Mock<IKeyVaultService>();
            
            // Setup key pair generation for the vault service
            _mockKeyVaultService
                .Setup(k => k.GenerateVaultKeyPairAsync())
                .ReturnsAsync(("testPublicKey", "testPrivateKey"));
                
            _mockKeyVaultService
                .Setup(k => k.EncryptVaultPrivateKeyAsync(It.IsAny<string>(), It.IsAny<Guid>()))
                .ReturnsAsync("encryptedPrivateKey");
                
            _mockKeyVaultService
                .Setup(k => k.DecryptVaultPrivateKeyAsync(It.IsAny<string>(), It.IsAny<Guid>()))
                .ReturnsAsync("decryptedPrivateKey");
                
            _vaultService = new VaultService(_dbContext, _mockKeyVaultService.Object);

            // Create test users
            _testUserId = Guid.NewGuid();
            _otherUserId = Guid.NewGuid();

            _dbContext.Users.Add(new User
            {
                Id = _testUserId,
                Username = "testuser",
                Email = "test@example.com",
                PasswordHash = "hashedpassword",
                CreatedAt = DateTime.UtcNow
            });

            _dbContext.Users.Add(new User
            {
                Id = _otherUserId,
                Username = "otheruser",
                Email = "other@example.com",
                PasswordHash = "hashedpassword",
                CreatedAt = DateTime.UtcNow
            });

            _dbContext.SaveChanges();
        }

        [Fact]
        public async Task CreateVaultAsync_ShouldCreateNewVault()
        {
            // Arrange
            var name = "Test Vault";
            var description = "Test Description";

            // Act
            var vault = await _vaultService.CreateVaultAsync(_testUserId, name, description);

            // Assert
            vault.Should().NotBeNull();
            vault.Name.Should().Be(name);
            vault.Description.Should().Be(description);
            vault.OwnerId.Should().Be(_testUserId);

            // Verify vault is in the database
            var vaultInDb = await _dbContext.Vaults.FindAsync(vault.Id);
            vaultInDb.Should().NotBeNull();
            vaultInDb!.Name.Should().Be(name);
        }

        [Fact]
        public async Task GetVaultByIdAsync_ShouldReturnVault_WhenUserIsOwner()
        {
            // Arrange
            var vaultId = Guid.NewGuid();
            _dbContext.Vaults.Add(new Vault
            {
                Id = vaultId,
                Name = "Owner Vault",
                Description = "Owner Description",
                OwnerId = _testUserId,
                CreatedAt = DateTime.UtcNow,
                PublicKey = "testPublicKey",
                EncryptedPrivateKey = "encryptedPrivateKey"
            });
            await _dbContext.SaveChangesAsync();

            // Act
            var vault = await _vaultService.GetVaultByIdAsync(vaultId, _testUserId);

            // Assert
            vault.Should().NotBeNull();
            vault!.Name.Should().Be("Owner Vault");
            vault.OwnerId.Should().Be(_testUserId);
        }

        [Fact]
        public async Task GetVaultByIdAsync_ShouldReturnVault_WhenVaultIsSharedWithUser()
        {
            // Arrange
            var vaultId = Guid.NewGuid();
            _dbContext.Vaults.Add(new Vault
            {
                Id = vaultId,
                Name = "Shared Vault",
                Description = "Shared Description",
                OwnerId = _otherUserId,
                CreatedAt = DateTime.UtcNow,
                PublicKey = "testPublicKey",
                EncryptedPrivateKey = "encryptedPrivateKey"
            });

            _dbContext.VaultShares.Add(new VaultShare
            {
                Id = Guid.NewGuid(),
                VaultId = vaultId,
                UserId = _testUserId,
                SharedAt = DateTime.UtcNow,
                CanEdit = false
            });

            await _dbContext.SaveChangesAsync();

            // Act
            var vault = await _vaultService.GetVaultByIdAsync(vaultId, _testUserId);

            // Assert
            vault.Should().NotBeNull();
            vault!.Name.Should().Be("Shared Vault");
            vault.OwnerId.Should().Be(_otherUserId);
        }

        [Fact]
        public async Task GetVaultByIdAsync_ShouldReturnNull_WhenUserHasNoAccess()
        {
            // Arrange
            var vaultId = Guid.NewGuid();
            _dbContext.Vaults.Add(new Vault
            {
                Id = vaultId,
                Name = "Private Vault",
                Description = "Private Description",
                OwnerId = _otherUserId,
                CreatedAt = DateTime.UtcNow,
                PublicKey = "testPublicKey",
                EncryptedPrivateKey = "encryptedPrivateKey"
            });
            await _dbContext.SaveChangesAsync();

            // Act
            var vault = await _vaultService.GetVaultByIdAsync(vaultId, _testUserId);

            // Assert
            vault.Should().BeNull();
        }

        [Fact]
        public async Task GetUserVaultsAsync_ShouldReturnUserOwnedVaults()
        {
            // Arrange
            _dbContext.Vaults.Add(new Vault
            {
                Id = Guid.NewGuid(),
                Name = "User Vault 1",
                Description = "Description for User Vault 1",
                OwnerId = _testUserId,
                CreatedAt = DateTime.UtcNow,
                PublicKey = "testPublicKey1",
                EncryptedPrivateKey = "encryptedPrivateKey1"
            });

            _dbContext.Vaults.Add(new Vault
            {
                Id = Guid.NewGuid(),
                Name = "User Vault 2",
                Description = "Description for User Vault 2",
                OwnerId = _testUserId,
                CreatedAt = DateTime.UtcNow,
                PublicKey = "testPublicKey2",
                EncryptedPrivateKey = "encryptedPrivateKey2"
            });

            _dbContext.Vaults.Add(new Vault
            {
                Id = Guid.NewGuid(),
                Name = "Other User Vault",
                Description = "Description for Other User Vault",
                OwnerId = _otherUserId,
                CreatedAt = DateTime.UtcNow,
                PublicKey = "testPublicKey3",
                EncryptedPrivateKey = "encryptedPrivateKey3"
            });

            await _dbContext.SaveChangesAsync();

            // Act
            var vaults = await _vaultService.GetUserVaultsAsync(_testUserId);

            // Assert
            vaults.Should().NotBeNull();
            vaults.Should().HaveCount(2);
            vaults.All(v => v.OwnerId == _testUserId).Should().BeTrue();
        }

        [Fact]
        public async Task GetSharedVaultsAsync_ShouldReturnVaultsSharedWithUser()
        {
            // Arrange
            var sharedVault1Id = Guid.NewGuid();
            var sharedVault2Id = Guid.NewGuid();

            _dbContext.Vaults.Add(new Vault
            {
                Id = sharedVault1Id,
                Name = "Shared Vault 1",
                Description = "Description for Shared Vault 1",
                OwnerId = _otherUserId,
                CreatedAt = DateTime.UtcNow,
                PublicKey = "testPublicKey1",
                EncryptedPrivateKey = "encryptedPrivateKey1"
            });

            _dbContext.Vaults.Add(new Vault
            {
                Id = sharedVault2Id,
                Name = "Shared Vault 2",
                Description = "Description for Shared Vault 2",
                OwnerId = _otherUserId,
                CreatedAt = DateTime.UtcNow,
                PublicKey = "testPublicKey2",
                EncryptedPrivateKey = "encryptedPrivateKey2"
            });

            _dbContext.VaultShares.Add(new VaultShare
            {
                Id = Guid.NewGuid(),
                VaultId = sharedVault1Id,
                UserId = _testUserId,
                SharedAt = DateTime.UtcNow,
                CanEdit = false
            });

            _dbContext.VaultShares.Add(new VaultShare
            {
                Id = Guid.NewGuid(),
                VaultId = sharedVault2Id,
                UserId = _testUserId,
                SharedAt = DateTime.UtcNow,
                CanEdit = true
            });

            await _dbContext.SaveChangesAsync();

            // Act
            var sharedVaults = await _vaultService.GetSharedVaultsAsync(_testUserId);

            // Assert
            sharedVaults.Should().NotBeNull();
            sharedVaults.Should().HaveCount(2);
            sharedVaults.All(v => v.OwnerId == _otherUserId).Should().BeTrue();
        }

        [Fact]
        public async Task UpdateVaultAsync_ShouldUpdateVault_WhenUserIsOwner()
        {
            // Arrange
            var vaultId = Guid.NewGuid();
            _dbContext.Vaults.Add(new Vault
            {
                Id = vaultId,
                Name = "Original Name",
                Description = "Original Description",
                OwnerId = _testUserId,
                CreatedAt = DateTime.UtcNow,
                PublicKey = "testPublicKey",
                EncryptedPrivateKey = "encryptedPrivateKey"
            });
            await _dbContext.SaveChangesAsync();

            var newName = "Updated Name";
            var newDescription = "Updated Description";

            // Act
            var result = await _vaultService.UpdateVaultAsync(vaultId, _testUserId, newName, newDescription);

            // Assert
            result.Should().BeTrue();

            // Verify vault was updated in database
            var updatedVault = await _dbContext.Vaults.FindAsync(vaultId);
            updatedVault.Should().NotBeNull();
            updatedVault!.Name.Should().Be(newName);
            updatedVault.Description.Should().Be(newDescription);
        }

        [Fact]
        public async Task UpdateVaultAsync_ShouldUpdateVault_WhenUserHasEditPermission()
        {
            // Arrange
            var vaultId = Guid.NewGuid();
            _dbContext.Vaults.Add(new Vault
            {
                Id = vaultId,
                Name = "Original Shared Name",
                Description = "Original Shared Description",
                OwnerId = _otherUserId,
                CreatedAt = DateTime.UtcNow,
                PublicKey = "testPublicKey",
                EncryptedPrivateKey = "encryptedPrivateKey"
            });

            _dbContext.VaultShares.Add(new VaultShare
            {
                Id = Guid.NewGuid(),
                VaultId = vaultId,
                UserId = _testUserId,
                SharedAt = DateTime.UtcNow,
                CanEdit = true
            });

            await _dbContext.SaveChangesAsync();

            var newName = "Updated Shared Name";
            var newDescription = "Updated Shared Description";

            // Act
            var result = await _vaultService.UpdateVaultAsync(vaultId, _testUserId, newName, newDescription);

            // Assert
            result.Should().BeTrue();

            // Verify vault was updated in database
            var updatedVault = await _dbContext.Vaults.FindAsync(vaultId);
            updatedVault.Should().NotBeNull();
            updatedVault!.Name.Should().Be(newName);
            updatedVault.Description.Should().Be(newDescription);
        }

        [Fact]
        public async Task UpdateVaultAsync_ShouldReturnFalse_WhenUserHasNoEditPermission()
        {
            // Arrange
            var vaultId = Guid.NewGuid();
            _dbContext.Vaults.Add(new Vault
            {
                Id = vaultId,
                Name = "No Edit Vault",
                Description = "No Edit Description",
                OwnerId = _otherUserId,
                CreatedAt = DateTime.UtcNow,
                PublicKey = "testPublicKey",
                EncryptedPrivateKey = "encryptedPrivateKey"
            });

            _dbContext.VaultShares.Add(new VaultShare
            {
                Id = Guid.NewGuid(),
                VaultId = vaultId,
                UserId = _testUserId,
                SharedAt = DateTime.UtcNow,
                CanEdit = false // Read-only permission
            });

            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _vaultService.UpdateVaultAsync(vaultId, _testUserId, "New Name", "New Description");

            // Assert
            result.Should().BeFalse();

            // Verify vault was not updated
            var vault = await _dbContext.Vaults.FindAsync(vaultId);
            vault!.Name.Should().Be("No Edit Vault");
        }

        [Fact]
        public async Task DeleteVaultAsync_ShouldDeleteVault_WhenUserIsOwner()
        {
            // Arrange
            var vaultId = Guid.NewGuid();
            _dbContext.Vaults.Add(new Vault
            {
                Id = vaultId,
                Name = "Vault to Delete",
                Description = "Description of vault to delete",
                OwnerId = _testUserId,
                CreatedAt = DateTime.UtcNow,
                PublicKey = "testPublicKey",
                EncryptedPrivateKey = "encryptedPrivateKey"
            });
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _vaultService.DeleteVaultAsync(vaultId, _testUserId);

            // Assert
            result.Should().BeTrue();

            // Verify vault was deleted from database
            var deletedVault = await _dbContext.Vaults.FindAsync(vaultId);
            deletedVault.Should().BeNull();
        }

        [Fact]
        public async Task DeleteVaultAsync_ShouldReturnFalse_WhenUserIsNotOwner()
        {
            // Arrange
            var vaultId = Guid.NewGuid();
            _dbContext.Vaults.Add(new Vault
            {
                Id = vaultId,
                Name = "Protected Vault",
                Description = "Description of protected vault",
                OwnerId = _otherUserId,
                CreatedAt = DateTime.UtcNow,
                PublicKey = "testPublicKey",
                EncryptedPrivateKey = "encryptedPrivateKey"
            });

            _dbContext.VaultShares.Add(new VaultShare
            {
                Id = Guid.NewGuid(),
                VaultId = vaultId,
                UserId = _testUserId,
                SharedAt = DateTime.UtcNow,
                CanEdit = true // Even with edit permission, only owner can delete
            });

            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _vaultService.DeleteVaultAsync(vaultId, _testUserId);

            // Assert
            result.Should().BeFalse();

            // Verify vault was not deleted
            var vault = await _dbContext.Vaults.FindAsync(vaultId);
            vault.Should().NotBeNull();
        }

        [Fact]
        public async Task ShareVaultAsync_ShouldShareVault_WhenUserIsOwner()
        {
            // Arrange
            var vaultId = Guid.NewGuid();
            _dbContext.Vaults.Add(new Vault
            {
                Id = vaultId,
                Name = "Vault to Share",
                Description = "Description for Vault to share",
                OwnerId = _testUserId,
                CreatedAt = DateTime.UtcNow,
                PublicKey = "testPublicKey",
                EncryptedPrivateKey = "encryptedPrivateKey"
            });
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _vaultService.ShareVaultAsync(vaultId, _testUserId, _otherUserId, true);

            // Assert
            result.Should().BeTrue();

            // Verify share exists in database
            var vaultShare = await _dbContext.VaultShares
                .FirstOrDefaultAsync(vs => vs.VaultId == vaultId && vs.UserId == _otherUserId);
            vaultShare.Should().NotBeNull();
            vaultShare!.CanEdit.Should().BeTrue();
        }

        [Fact]
        public async Task ShareVaultAsync_ShouldReturnFalse_WhenUserIsNotOwner()
        {
            // Arrange
            var vaultId = Guid.NewGuid();
            _dbContext.Vaults.Add(new Vault
            {
                Id = vaultId,
                Name = "Non-Owner Vault",
                Description = "Description for Non-Owner Vault",
                OwnerId = _otherUserId,
                CreatedAt = DateTime.UtcNow,
                PublicKey = "testPublicKey",
                EncryptedPrivateKey = "encryptedPrivateKey"
            });
            await _dbContext.SaveChangesAsync();

            // Act - Non-owner trying to share a vault
            var result = await _vaultService.ShareVaultAsync(vaultId, _testUserId, Guid.NewGuid(), true);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task RevokeVaultShareAsync_ShouldRevokeShare_WhenUserIsOwner()
        {
            // Arrange
            var vaultId = Guid.NewGuid();
            _dbContext.Vaults.Add(new Vault
            {
                Id = vaultId,
                Name = "Shared Vault to Revoke",
                Description = "Description for Shared Vault to revoke",
                OwnerId = _testUserId,
                CreatedAt = DateTime.UtcNow,
                PublicKey = "testPublicKey",
                EncryptedPrivateKey = "encryptedPrivateKey"
            });

            _dbContext.VaultShares.Add(new VaultShare
            {
                Id = Guid.NewGuid(),
                VaultId = vaultId,
                UserId = _otherUserId,
                SharedAt = DateTime.UtcNow,
                CanEdit = true
            });

            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _vaultService.RevokeVaultShareAsync(vaultId, _testUserId, _otherUserId);

            // Assert
            result.Should().BeTrue();

            // Verify share was removed from database
            var vaultShare = await _dbContext.VaultShares
                .FirstOrDefaultAsync(vs => vs.VaultId == vaultId && vs.UserId == _otherUserId);
            vaultShare.Should().BeNull();
        }

        [Fact]
        public async Task HasVaultAccessAsync_ShouldReturnTrue_WhenUserIsOwner()
        {
            // Arrange
            var vaultId = Guid.NewGuid();
            _dbContext.Vaults.Add(new Vault
            {
                Id = vaultId,
                Name = "Owner Access Vault",
                Description = "Description for Owner Access Vault",
                OwnerId = _testUserId,
                CreatedAt = DateTime.UtcNow,
                PublicKey = "testPublicKey",
                EncryptedPrivateKey = "encryptedPrivateKey"
            });
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _vaultService.HasVaultAccessAsync(vaultId, _testUserId);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task HasVaultAccessAsync_ShouldReturnTrue_WhenVaultIsShared()
        {
            // Arrange
            var vaultId = Guid.NewGuid();
            _dbContext.Vaults.Add(new Vault
            {
                Id = vaultId,
                Name = "Shared Access Vault",
                Description = "Description for Shared Access Vault",
                OwnerId = _otherUserId,
                CreatedAt = DateTime.UtcNow,
                PublicKey = "testPublicKey",
                EncryptedPrivateKey = "encryptedPrivateKey"
            });

            _dbContext.VaultShares.Add(new VaultShare
            {
                Id = Guid.NewGuid(),
                VaultId = vaultId,
                UserId = _testUserId,
                SharedAt = DateTime.UtcNow,
                CanEdit = false
            });

            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _vaultService.HasVaultAccessAsync(vaultId, _testUserId);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task HasVaultAccessAsync_ShouldReturnFalse_WhenUserHasNoAccess()
        {
            // Arrange
            var vaultId = Guid.NewGuid();
            _dbContext.Vaults.Add(new Vault
            {
                Id = vaultId,
                Name = "No Access Vault",
                Description = "Description for No Access Vault",
                OwnerId = _otherUserId,
                CreatedAt = DateTime.UtcNow,
                PublicKey = "testPublicKey",
                EncryptedPrivateKey = "encryptedPrivateKey"
            });
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _vaultService.HasVaultAccessAsync(vaultId, _testUserId);

            // Assert
            result.Should().BeFalse();
        }
    }
} 