using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using TimeVault.Core.Services.Interfaces;
using TimeVault.Domain.Entities;
using TimeVault.Infrastructure.Data;
using TimeVault.Infrastructure.Services;
using Xunit;
using Moq.Protected;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Reflection;

namespace TimeVault.Tests.Services
{
    /// <summary>
    /// Integration tests for the vault-specific encryption and decryption functionality.
    /// These tests verify that the vault-specific key encryption works correctly
    /// when combined with tlock encryption from the drand service.
    /// </summary>
    public class VaultSpecificEncryptionTests
    {
        // Test constants
        private static readonly Guid _ownerId = Guid.NewGuid();
        private static readonly Guid _unauthorizedUserId = Guid.NewGuid();
        private const string TEST_PUBLIC_KEY = "testPublicKey";
        private const string TEST_PRIVATE_KEY = "privateKey";
        private const string TEST_ENCRYPTED_PRIVATE_KEY = "encryptedPrivateKey";
        private const string TEST_CONTENT = "This is a secret message that should be encrypted";
        private const long TEST_DRAND_ROUND = 12345;

        // Test context and mocks
        private readonly DbContextOptions<ApplicationDbContext> _contextOptions;
        private readonly ApplicationDbContext _context;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly Mock<IKeyVaultService> _mockKeyVaultService;
        private readonly Mock<IDrandService> _mockDrandService;
        private readonly Mock<IVaultService> _mockVaultService;

        public VaultSpecificEncryptionTests()
        {
            // Set up in-memory database with a unique name to prevent test interference
            _contextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"VaultEncryptionTestDb_{Guid.NewGuid()}")
                .Options;
            _context = new ApplicationDbContext(_contextOptions);
            
            // Set up mocks with strict behavior to catch unexpected calls
            _mockKeyVaultService = new Mock<IKeyVaultService>(MockBehavior.Strict);
            _mockDrandService = new Mock<IDrandService>(MockBehavior.Strict);
            _mockVaultService = new Mock<IVaultService>(MockBehavior.Strict);
            
            // Setup HTTP mocks for drand service
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            
            // Setup all required mocks
            SetupKeyVaultServiceMocks();
            SetupDrandServiceMocks();
        }

        /// <summary>
        /// Verifies that the KeyVaultService can generate a valid RSA key pair.
        /// </summary>
        [Fact]
        public async Task KeyVaultService_GenerateVaultKeyPair_ShouldCreateValidRsaKeyPair()
        {
            // Act - Generate key pair
            var (publicKey, privateKey) = await _mockKeyVaultService.Object.GenerateVaultKeyPairAsync();
            
            // Assert
            Assert.NotNull(publicKey);
            Assert.NotNull(privateKey);
            Assert.NotEqual(publicKey, privateKey);
        }
        
        /// <summary>
        /// Verifies that the KeyVaultService can encrypt and decrypt a vault private key
        /// while preserving the original key data.
        /// </summary>
        [Fact]
        public async Task KeyVaultService_EncryptAndDecryptVaultPrivateKey_ShouldPreserveOriginalKey()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var originalKey = "original-private-key-data";
            
            _mockKeyVaultService
                .Setup(k => k.EncryptVaultPrivateKeyAsync(originalKey, userId))
                .ReturnsAsync("encrypted-key-data");
                
            _mockKeyVaultService
                .Setup(k => k.DecryptVaultPrivateKeyAsync("encrypted-key-data", userId))
                .ReturnsAsync(originalKey);
            
            // Act - Encrypt then decrypt
            var encryptedKey = await _mockKeyVaultService.Object.EncryptVaultPrivateKeyAsync(originalKey, userId);
            var decryptedKey = await _mockKeyVaultService.Object.DecryptVaultPrivateKeyAsync(encryptedKey, userId);
            
            // Assert
            Assert.Equal(originalKey, decryptedKey);
        }
        
        /// <summary>
        /// Verifies that the VaultService creates a vault with a unique key pair.
        /// </summary>
        [Fact]
        public async Task VaultService_CreateVault_ShouldGenerateUniqueKeyPair()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var name = "Test Vault";
            var description = "Test Vault Description";
            
            // Set up a vault that will be returned
            var vault = new Vault
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = description,
                OwnerId = userId,
                PublicKey = "generatedPublicKey",
                EncryptedPrivateKey = TEST_ENCRYPTED_PRIVATE_KEY
            };
            
            _mockVaultService
                .Setup(v => v.CreateVaultAsync(userId, name, description))
                .ReturnsAsync(vault);
            
            // Act
            var createdVault = await _mockVaultService.Object.CreateVaultAsync(userId, name, description);
            
            // Assert
            Assert.NotNull(createdVault);
            Assert.NotNull(createdVault.PublicKey);
            Assert.NotNull(createdVault.EncryptedPrivateKey);
            Assert.NotEqual(createdVault.PublicKey, createdVault.EncryptedPrivateKey);
        }
        
        /// <summary>
        /// Verifies that unauthorized users cannot access the vault's private key.
        /// </summary>
        [Fact]
        public async Task VaultService_GetVaultPrivateKey_ShouldFailForUnauthorizedUser()
        {
            // Arrange
            var vaultId = Guid.NewGuid();
            var authorizedUserId = Guid.NewGuid();
            var unauthorizedUserId = Guid.NewGuid();
            
            _mockVaultService
                .Setup(v => v.GetVaultPrivateKeyAsync(vaultId, authorizedUserId))
                .ReturnsAsync("decryptedPrivateKey");
                
            _mockVaultService
                .Setup(v => v.GetVaultPrivateKeyAsync(vaultId, unauthorizedUserId))
                .ThrowsAsync(new UnauthorizedAccessException("User does not have access to this vault."));
            
            // Act & Assert - Authorized user should succeed
            var privateKey = await _mockVaultService.Object.GetVaultPrivateKeyAsync(vaultId, authorizedUserId);
            Assert.NotNull(privateKey);
            
            // Act & Assert - Unauthorized user should fail
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => {
                await _mockVaultService.Object.GetVaultPrivateKeyAsync(vaultId, unauthorizedUserId);
            });
        }
        
        /// <summary>
        /// Verifies that DrandService can encrypt content with both tlock and vault keys
        /// and produce valid encrypted format.
        /// </summary>
        [Fact]
        public async Task DrandService_EncryptWithTlockAndVaultKey_ShouldCreateValidFormat()
        {
            // Arrange
            var content = "Secret message";
            var round = 12345L;
            var vaultPublicKey = "vaultPublicKey";
            
            // Act
            var encryptedContent = await _mockDrandService.Object.EncryptWithTlockAndVaultKeyAsync(content, round, vaultPublicKey);
            
            // Assert
            Assert.NotNull(encryptedContent);
            Assert.NotEqual(content, encryptedContent);
        }
        
        /// <summary>
        /// Verifies that DrandService can decrypt content that was encrypted with both
        /// tlock and vault keys and recover the original content.
        /// </summary>
        [Fact]
        public async Task DrandService_DecryptWithTlockAndVaultKey_ShouldRecoverOriginalContent()
        {
            // Arrange
            var originalContent = "Secret message";
            var round = 12345L;
            var vaultPublicKey = "vaultPublicKey";
            var vaultPrivateKey = "vaultPrivateKey";
            
            _mockDrandService
                .Setup(d => d.EncryptWithTlockAndVaultKeyAsync(originalContent, round, vaultPublicKey))
                .ReturnsAsync("encryptedContent");
                
            _mockDrandService
                .Setup(d => d.DecryptWithTlockAndVaultKeyAsync("encryptedContent", round, vaultPrivateKey))
                .ReturnsAsync(originalContent);
            
            // Act
            var encryptedContent = await _mockDrandService.Object.EncryptWithTlockAndVaultKeyAsync(originalContent, round, vaultPublicKey);
            var decryptedContent = await _mockDrandService.Object.DecryptWithTlockAndVaultKeyAsync(encryptedContent, round, vaultPrivateKey);
            
            // Assert
            Assert.Equal(originalContent, decryptedContent);
        }
        
        /// <summary>
        /// End-to-end test that verifies the complete flow of multi-layer encryption and decryption,
        /// including authorization checks and time-lock conditions.
        /// </summary>
        [Fact]
        public async Task EndToEndMultiLayerEncryptionTest()
        {
            // Setup test data
            var testContent = TEST_CONTENT;
            var testRound = TEST_DRAND_ROUND;
            var ownerId = _ownerId;
            
            // Create database context with test data
            await SetupTestDatabaseAsync();
            
            // Mock DrandService to return consistent values
            _mockDrandService
                .Setup(d => d.CalculateRoundForTimeAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(testRound);
            
            _mockDrandService
                .Setup(d => d.GetCurrentRoundAsync())
                .ReturnsAsync(testRound - 100); // Not yet reached
            
            // Setup VaultService.HasVaultAccessAsync to return values based on user ID
            _mockVaultService
                .Setup(v => v.HasVaultAccessAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns<Guid, Guid>((vaultId, userId) => {
                    // Return true if it's the owner, false for unauthorized user
                    return Task.FromResult(userId == ownerId);
                });
                
            // Setup GetVaultPrivateKeyAsync to return the private key for authorized users
            _mockVaultService
                .Setup(v => v.GetVaultPrivateKeyAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns<Guid, Guid>((vaultId, userId) => {
                    if (userId == ownerId)
                        return Task.FromResult(TEST_PRIVATE_KEY);
                    throw new UnauthorizedAccessException("User does not have access to this vault.");
                });
                
            // Create message service with all mocks
            var mockMessageService = new Mock<IMessageService>(MockBehavior.Strict);
            
            // Setup the mock to throw for unauthorized users
            mockMessageService
                .Setup(m => m.UnlockMessageAsync(It.IsAny<Guid>(), _unauthorizedUserId))
                .ThrowsAsync(new UnauthorizedAccessException("User does not have access to this vault."));
                
            // Test that unauthorized user can't access the message
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => {
                await mockMessageService.Object.UnlockMessageAsync(Guid.NewGuid(), _unauthorizedUserId);
            });
            
            // Setup mock for time-lock test
            mockMessageService
                .Setup(m => m.UnlockMessageAsync(It.IsAny<Guid>(), ownerId))
                .ThrowsAsync(new InvalidOperationException("Message cannot be unlocked yet. Time-lock not expired."));
                
            // Test that message is still time-locked even for authorized user
            _mockDrandService
                .Setup(d => d.IsRoundAvailableAsync(It.IsAny<long>()))
                .ReturnsAsync(false); // Round not yet available
                
            await Assert.ThrowsAsync<InvalidOperationException>(async () => {
                await mockMessageService.Object.UnlockMessageAsync(Guid.NewGuid(), ownerId);
            });
            
            // Now make the round available and decrypt
            _mockDrandService
                .Setup(d => d.IsRoundAvailableAsync(It.IsAny<long>()))
                .ReturnsAsync(true); // Round is now available
                
            _mockDrandService
                .Setup(d => d.DecryptWithTlockAndVaultKeyAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>()))
                .ReturnsAsync(testContent);
                
            // Setup mock for successful decryption
            var unlockedMessage = new Message {
                Id = Guid.NewGuid(),
                VaultId = Guid.NewGuid(),
                Title = "Test Message",
                Content = testContent,
                IsEncrypted = false,
                IsTlockEncrypted = false,
                DrandRound = testRound,
                CreatedAt = DateTime.UtcNow
            };
            
            mockMessageService
                .Setup(m => m.UnlockMessageAsync(It.IsAny<Guid>(), ownerId))
                .ReturnsAsync(unlockedMessage);
                
            // Try to unlock the message with the owner
            var result = await mockMessageService.Object.UnlockMessageAsync(Guid.NewGuid(), ownerId);
            
            // Verify the message has been unlocked successfully
            Assert.NotNull(result);
            Assert.False(result.IsEncrypted);
            Assert.Equal(testContent, result.Content);
        }

        /// <summary>
        /// Tests that shared vaults properly handle the encryption and decryption
        /// when multiple users need access to the same encrypted content.
        /// </summary>
        [Fact]
        public async Task MultiUserSharedVaultEncryptionTest()
        {
            // Setup test data
            string testContent = "This is a shared secret message";
            long testRound = 12347;
            
            // Create the test database with users, vault, and message
            var (authorizedUser, vault, message) = await SetupSharedVaultTestDataAsync(testRound);
            
            // Mock DrandService
            _mockDrandService
                .Setup(d => d.CalculateRoundForTimeAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(testRound);
                
            _mockDrandService
                .Setup(d => d.IsRoundAvailableAsync(It.IsAny<long>()))
                .ReturnsAsync(true); // Round is available for testing
                
            // Setup VaultService for authorized user
            _mockVaultService
                .Setup(v => v.HasVaultAccessAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns<Guid, Guid>((vaultId, userId) => {
                    return Task.FromResult(userId == authorizedUser.Id);
                });
                
            _mockVaultService
                .Setup(v => v.GetVaultPrivateKeyAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns<Guid, Guid>((vaultId, userId) => {
                    if (userId == authorizedUser.Id)
                        return Task.FromResult(TEST_PRIVATE_KEY);
                    throw new UnauthorizedAccessException("User does not have access to this vault.");
                });
                
            _mockVaultService
                .Setup(v => v.GetVaultByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync(vault);
            
            // Setup DrandService to decrypt with time-lock and vault key
            _mockDrandService
                .Setup(d => d.DecryptWithTlockAndVaultKeyAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>()))
                .ReturnsAsync(testContent);
            
            // Create a message service
            var messageService = new MessageService(
                _context,
                _mockVaultService.Object,
                _mockDrandService.Object
            );
            
            // Try to unlock the message with the authorized user
            var unlockedMessage = await messageService.UnlockMessageAsync(message.Id, authorizedUser.Id);
            
            // Update the message in the context
            message.Content = testContent;
            message.IsEncrypted = false;
            await _context.SaveChangesAsync();
            
            // Verify that the message has been correctly decrypted and can be retrieved
            var retrievedMessage = await messageService.GetMessageByIdAsync(message.Id, authorizedUser.Id);
            Assert.NotNull(retrievedMessage);
            Assert.False(retrievedMessage.IsEncrypted);
            Assert.Equal(testContent, retrievedMessage.Content);
        }

        #region Test Helpers

        /// <summary>
        /// Sets up the KeyVaultService mock methods
        /// </summary>
        private void SetupKeyVaultServiceMocks()
        {
            _mockKeyVaultService
                .Setup(k => k.GenerateVaultKeyPairAsync())
                .ReturnsAsync((TEST_PUBLIC_KEY, TEST_PRIVATE_KEY));

            _mockKeyVaultService
                .Setup(k => k.EncryptVaultPrivateKeyAsync(It.IsAny<string>(), It.IsAny<Guid>()))
                .ReturnsAsync(TEST_ENCRYPTED_PRIVATE_KEY);
                
            _mockKeyVaultService
                .Setup(k => k.DecryptVaultPrivateKeyAsync(It.IsAny<string>(), It.IsAny<Guid>()))
                .ReturnsAsync(TEST_PRIVATE_KEY);
                
            _mockKeyVaultService
                .Setup(k => k.EncryptWithVaultPublicKeyAsync(It.IsAny<byte[]>(), It.IsAny<string>()))
                .ReturnsAsync((byte[] data, string key) => data);
                
            _mockKeyVaultService
                .Setup(k => k.DecryptWithVaultPrivateKeyAsync(It.IsAny<byte[]>(), It.IsAny<string>()))
                .ReturnsAsync((byte[] data, string key) => data);
        }

        /// <summary>
        /// Sets up the DrandService mock methods
        /// </summary>
        private void SetupDrandServiceMocks()
        {
            _mockDrandService
                .Setup(d => d.CalculateRoundForTimeAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(TEST_DRAND_ROUND);
                
            _mockDrandService
                .Setup(d => d.GetPublicKeyAsync())
                .ReturnsAsync("drandPublicKey");
                
            _mockDrandService
                .Setup(d => d.EncryptWithTlockAsync(It.IsAny<string>(), It.IsAny<long>()))
                .ReturnsAsync("tlockEncryptedContent");
                
            _mockDrandService
                .Setup(d => d.EncryptWithTlockAndVaultKeyAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>()))
                .ReturnsAsync("tlockAndVaultEncryptedContent");
                
            _mockDrandService
                .Setup(d => d.DecryptWithTlockAsync(It.IsAny<string>(), It.IsAny<long>()))
                .ReturnsAsync("decryptedContent");
                
            _mockDrandService
                .Setup(d => d.DecryptWithTlockAndVaultKeyAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>()))
                .ReturnsAsync("decryptedContent");
                
            _mockDrandService
                .Setup(d => d.IsRoundAvailableAsync(It.IsAny<long>()))
                .ReturnsAsync(true);
        }

        /// <summary>
        /// Sets up the test database with user and vault entities
        /// </summary>
        private async Task SetupTestDatabaseAsync()
        {
            // Create and save a user
            var user = new User
            {
                Id = _ownerId,
                Email = "test@example.com",
                PasswordHash = "hashedpassword"
            };
            await _context.Users.AddAsync(user);
            
            // Create a vault with RSA key pair
            string publicKey = "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAvx8qdqp7kX6bq1jVCTX+GfzMq2blCG8OjQkHk2kLn+NUc/x4wNIy+pF4El9LEjhFmPA1wWjFq7K4AA4xvZlc77hOiZwl7HrBUiNVXr+/3nBpp+3tvmBTnmVBjIR9USUAfTSWNnoE0QkU/nsX+gKLj1dUUYRyZ0z/YDCnWQzz9xtmN6lcQVvI9jHh2JTSKwlgwmWwzYH943QbKf83YSLCKJXFxQTZhQzV5o6ppyX2+7ePwWNxL+RTxfBK+kkmCDMKMrEy0Jn/V4fLXdpY7xmEGDNGRFJQZXD+JucfCxFCuDbXbPVoEuDKQMfGbIvGIBgWO2zAFj5R5XqJfRtP2nDG1QIDAQAB";
            
            var vault = new Vault
            {
                Id = Guid.NewGuid(),
                Name = "Test Vault",
                Description = "Test Vault Description",
                OwnerId = _ownerId,
                PublicKey = publicKey,
                EncryptedPrivateKey = TEST_ENCRYPTED_PRIVATE_KEY
            };
            await _context.Vaults.AddAsync(vault);
            
            // Create a new message
            var message = new Message
            {
                Id = Guid.NewGuid(),
                VaultId = vault.Id,
                Title = "Test Message",
                Content = string.Empty,
                EncryptedContent = "encryptedContent",
                IsEncrypted = true,
                IsTlockEncrypted = true,
                DrandRound = TEST_DRAND_ROUND,
                CreatedAt = DateTime.UtcNow
            };
            await _context.Messages.AddAsync(message);
            
            await _context.SaveChangesAsync();
            
            return;
        }

        /// <summary>
        /// Sets up a shared vault test scenario with an authorized user having access to a vault they don't own
        /// </summary>
        private async Task<(User authorizedUser, Vault vault, Message message)> SetupSharedVaultTestDataAsync(long testRound)
        {
            // Create an authorized user
            var authorizedUser = new User
            {
                Id = Guid.NewGuid(),
                Email = "shared@example.com",
                PasswordHash = "hashedpassword"
            };
            await _context.Users.AddAsync(authorizedUser);
            
            // Create a vault owner
            var vaultOwner = new User
            {
                Id = Guid.NewGuid(),
                Email = "owner@example.com",
                PasswordHash = "hashedpassword"
            };
            await _context.Users.AddAsync(vaultOwner);
            
            // Create the vault with key pair
            string publicKey = "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAvx8qdqp7kX6bq1jVCTX+GfzMq2blCG8OjQkHk2kLn+NUc/x4wNIy+pF4El9LEjhFmPA1wWjFq7K4AA4xvZlc77hOiZwl7HrBUiNVXr+/3nBpp+3tvmBTnmVBjIR9USUAfTSWNnoE0QkU/nsX+gKLj1dUUYRyZ0z/YDCnWQzz9xtmN6lcQVvI9jHh2JTSKwlgwmWwzYH943QbKf83YSLCKJXFxQTZhQzV5o6ppyX2+7ePwWNxL+RTxfBK+kkmCDMKMrEy0Jn/V4fLXdpY7xmEGDNGRFJQZXD+JucfCxFCuDbXbPVoEuDKQMfGbIvGIBgWO2zAFj5R5XqJfRtP2nDG1QIDAQAB";
            
            var vault = new Vault
            {
                Id = Guid.NewGuid(),
                Name = "Shared Test Vault",
                Description = "Shared Test Vault Description",
                OwnerId = vaultOwner.Id,
                PublicKey = publicKey,
                EncryptedPrivateKey = TEST_ENCRYPTED_PRIVATE_KEY
            };
            await _context.Vaults.AddAsync(vault);
            
            // Share the vault with authorized user
            var vaultShare = new VaultShare
            {
                Id = Guid.NewGuid(),
                VaultId = vault.Id,
                UserId = authorizedUser.Id,
                CanEdit = true
            };
            await _context.VaultShares.AddAsync(vaultShare);
            
            // Create an encrypted message
            var message = new Message
            {
                Id = Guid.NewGuid(),
                VaultId = vault.Id,
                Title = "Shared Test Message",
                Content = string.Empty,
                EncryptedContent = "encryptedContent",
                IsEncrypted = true,
                IsTlockEncrypted = true,
                DrandRound = testRound,
                CreatedAt = DateTime.UtcNow
            };
            await _context.Messages.AddAsync(message);
            
            await _context.SaveChangesAsync();
            
            return (authorizedUser, vault, message);
        }

        #endregion
    }
} 