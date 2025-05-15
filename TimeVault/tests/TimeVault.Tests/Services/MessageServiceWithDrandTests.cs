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
    /// <summary>
    /// Tests for MessageService with focus on Drand integration.
    /// These tests verify the time-locked encryption and decryption functionality 
    /// when working with the Drand service.
    /// </summary>
    public class MessageServiceWithDrandTests
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<IVaultService> _mockVaultService;
        private readonly Mock<IDrandService> _mockDrandService;
        private readonly MessageService _messageService;
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _vaultId = Guid.NewGuid();
        private const string TestPublicKey = "testPublicKey";
        private const string TestPrivateKey = "vaultPrivateKey";
        private const long CurrentRound = 1000L;
        private const long FutureRound = 1200L;

        public MessageServiceWithDrandTests()
        {
            // Set up in-memory database with a unique name to avoid test interference
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"MessageServiceWithDrandTests_{Guid.NewGuid()}")
                .EnableSensitiveDataLogging()
                .Options;

            _context = new ApplicationDbContext(options);
            _mockVaultService = new Mock<IVaultService>(MockBehavior.Strict);
            _mockDrandService = new Mock<IDrandService>(MockBehavior.Strict);

            // Setup all the required mocks
            SetupVaultServiceMocks();
            SetupDrandServiceMocks();

            _messageService = new MessageService(_context, _mockVaultService.Object, _mockDrandService.Object);

            // Initialize test data
            SeedTestData();
        }

        /// <summary>
        /// Verifies that message creation uses tlock encryption when an unlock time is provided.
        /// </summary>
        [Fact]
        public async Task CreateMessageAsync_ShouldUseTlockEncryption_ForAllEncryptedMessages()
        {
            // Arrange
            var title = "Tlock Encrypted Message";
            var content = "This is a message encrypted with tlock";
            var unlockTime = DateTime.UtcNow.AddMinutes(2); // Even short unlock times now use tlock

            // Setup specific mock for this test
            _mockDrandService
                .Setup(d => d.EncryptWithTlockAndVaultKeyAsync(content, FutureRound, TestPublicKey))
                .ReturnsAsync("tlock-vault-encrypted-content");

            // Act
            var message = await _messageService.CreateMessageAsync(_vaultId, _userId, title, content, unlockTime);

            // Assert
            message.Should().NotBeNull();
            message.Title.Should().Be(title);
            message.IsEncrypted.Should().BeTrue();
            message.IsTlockEncrypted.Should().BeTrue();
            message.DrandRound.Should().NotBeNull();
            message.TlockPublicKey.Should().NotBeNull();
            message.EncryptedContent.Should().NotBeEmpty();
            message.Content.Should().BeEmpty(); // Content should be empty when encrypted
            
            // Verify drand service was called correctly
            _mockDrandService.Verify(d => d.CalculateRoundForTimeAsync(unlockTime), Times.Once);
            _mockDrandService.Verify(d => d.GetPublicKeyAsync(), Times.Once);
            _mockDrandService.Verify(d => d.EncryptWithTlockAndVaultKeyAsync(content, It.IsAny<long>(), It.IsAny<string>()), Times.Once);
        }

        /// <summary>
        /// Verifies that message unlocking uses tlock decryption for time-locked encrypted messages.
        /// </summary>
        [Fact]
        public async Task UnlockMessageAsync_ShouldUseTlockDecryption_ForTlockEncryptedMessages()
        {
            // Arrange
            var originalContent = "This is tlock encrypted content";
            var messageId = Guid.NewGuid();
            var drandRound = 50L; // A round that's already available (1000L is current)
            var encryptedContent = $"TLOCK_ENCRYPTED({originalContent},round={drandRound})";

            await CreateEncryptedMessageInDatabase(messageId, drandRound, encryptedContent);
            
            // Override mock setup for the decryption method with exact input parameters
            _mockDrandService
                .Setup(d => d.DecryptWithTlockAndVaultKeyAsync(encryptedContent, drandRound, TestPrivateKey))
                .ReturnsAsync(originalContent);
            
            // Act
            var unlockedMessage = await _messageService.UnlockMessageAsync(messageId, _userId);
            
            // Assert
            unlockedMessage.Should().NotBeNull();
            unlockedMessage.IsEncrypted.Should().BeFalse();
            unlockedMessage.IsTlockEncrypted.Should().BeFalse();
            unlockedMessage.Content.Should().Be(originalContent);
            unlockedMessage.EncryptedContent.Should().BeEmpty();
            
            // Verify drand service was called for decryption
            _mockDrandService.Verify(d => d.IsRoundAvailableAsync(drandRound), Times.Once);
        }
        
        /// <summary>
        /// Verifies that message updating uses tlock encryption when changing to a time-locked message.
        /// </summary>
        [Fact]
        public async Task UpdateMessageAsync_ShouldUseTlockEncryption_ForAllEncryptedMessages()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            await CreateUnencryptedMessageInDatabase(messageId);
            
            // Now update it with an unlock time (which will trigger encryption)
            var newTitle = "Updated Tlock Message";
            var newContent = "Updated content with tlock encryption";
            var newUnlockTime = DateTime.UtcNow.AddMinutes(2); // Even short unlock times now use tlock
            var encryptedContent = "tlock-encrypted-content";
            
            // Mock setup specifically for this test
            _mockDrandService
                .Setup(d => d.CalculateRoundForTimeAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(FutureRound);
            _mockDrandService
                .Setup(d => d.GetPublicKeyAsync())
                .ReturnsAsync("test-public-key");
            _mockDrandService
                .Setup(d => d.EncryptWithTlockAndVaultKeyAsync(newContent, FutureRound, TestPublicKey))
                .ReturnsAsync(encryptedContent);
            
            // Act - update with a future unlock time to trigger encryption
            var result = await _messageService.UpdateMessageAsync(messageId, _userId, newTitle, newContent, newUnlockTime);
            
            // Assert
            result.Should().BeTrue();
            
            // Get the updated message from DB
            var updatedMessage = await _context.Messages.FindAsync(messageId);
            updatedMessage.Should().NotBeNull();
            
            // Since we're updating an unencrypted message with a future unlock time,
            // it should be encrypted with tlock now
            updatedMessage!.Title.Should().Be(newTitle);
            updatedMessage.IsEncrypted.Should().BeTrue();
            updatedMessage.IsTlockEncrypted.Should().BeTrue();
            updatedMessage.DrandRound.Should().Be(FutureRound);
            updatedMessage.TlockPublicKey.Should().Be("test-public-key");
            updatedMessage.EncryptedContent.Should().Be(encryptedContent);
            updatedMessage.Content.Should().BeEmpty();
            
            // Verify drand service was called
            _mockDrandService.Verify(d => d.CalculateRoundForTimeAsync(It.IsAny<DateTime>()), Times.Once);
            _mockDrandService.Verify(d => d.GetPublicKeyAsync(), Times.Once);
            _mockDrandService.Verify(d => d.EncryptWithTlockAndVaultKeyAsync(newContent, FutureRound, TestPublicKey), Times.Once);
        }

        #region Test Helpers

        /// <summary>
        /// Sets up all required mock methods for the VaultService
        /// </summary>
        private void SetupVaultServiceMocks()
        {
            _mockVaultService.Setup(v => v.HasVaultAccessAsync(_vaultId, _userId))
                .ReturnsAsync(true);
            _mockVaultService.Setup(v => v.CanEditVaultAsync(_vaultId, _userId))
                .ReturnsAsync(true);
            _mockVaultService.Setup(v => v.GetVaultPrivateKeyAsync(_vaultId, _userId))
                .ReturnsAsync(TestPrivateKey);
        }

        /// <summary>
        /// Sets up all required mock methods for the DrandService
        /// </summary>
        private void SetupDrandServiceMocks()
        {
            _mockDrandService.Setup(d => d.GetCurrentRoundAsync())
                .ReturnsAsync(CurrentRound);
            
            _mockDrandService.Setup(d => d.CalculateRoundForTimeAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(FutureRound);
            
            _mockDrandService.Setup(d => d.GetPublicKeyAsync())
                .ReturnsAsync("test-public-key");
            
            _mockDrandService.Setup(d => d.EncryptWithTlockAsync(It.IsAny<string>(), It.IsAny<long>()))
                .ReturnsAsync("tlock-encrypted-content");

            _mockDrandService.Setup(d => d.EncryptWithTlockAndVaultKeyAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>()))
                .ReturnsAsync("tlock-and-vault-encrypted-content");
            
            _mockDrandService.Setup(d => d.DecryptWithTlockAsync(It.IsAny<string>(), It.IsAny<long>()))
                .ReturnsAsync("decrypted-content");

            _mockDrandService.Setup(d => d.DecryptWithTlockAndVaultKeyAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>()))
                .ReturnsAsync("decrypted-content");
            
            _mockDrandService.Setup(d => d.IsRoundAvailableAsync(It.IsAny<long>()))
                .ReturnsAsync(true);
        }

        /// <summary>
        /// Initializes the database with test data
        /// </summary>
        private void SeedTestData()
        {
            // Add test user
            _context.Users.Add(new User
            {
                Id = _userId,
                Email = "test@example.com",
                PasswordHash = "hashedpassword",
                CreatedAt = DateTime.UtcNow
            });

            // Add test vault
            _context.Vaults.Add(new Vault
            {
                Id = _vaultId,
                Name = "Test Vault",
                Description = "Test Description",
                OwnerId = _userId,
                CreatedAt = DateTime.UtcNow,
                PublicKey = TestPublicKey,
                EncryptedPrivateKey = "encryptedPrivateKey"
            });

            _context.SaveChanges();
        }

        /// <summary>
        /// Helper method to create an encrypted message in the database for testing
        /// </summary>
        private async Task CreateEncryptedMessageInDatabase(Guid messageId, long drandRound, string encryptedContent)
        {
            var message = new Message
            {
                Id = messageId,
                Title = "Tlock Message",
                Content = "", // Empty when encrypted
                EncryptedContent = encryptedContent,
                IsEncrypted = true,
                IsTlockEncrypted = true,
                DrandRound = drandRound,
                TlockPublicKey = "test-public-key",
                CreatedAt = DateTime.UtcNow.AddHours(-1),
                UnlockDateTime = DateTime.UtcNow.AddMinutes(-10), // Already passed
                VaultId = _vaultId
            };
            
            await _context.Messages.AddAsync(message);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Helper method to create an unencrypted message in the database for testing
        /// </summary>
        private async Task CreateUnencryptedMessageInDatabase(Guid messageId)
        {
            var message = new Message
            {
                Id = messageId,
                Title = "Original Message",
                Content = "Original content without encryption",
                EncryptedContent = "", 
                IsEncrypted = false, // Message starts unencrypted
                IsTlockEncrypted = false,
                DrandRound = null,
                TlockPublicKey = null,
                CreatedAt = DateTime.UtcNow,
                UnlockDateTime = null,
                VaultId = _vaultId
            };
            
            await _context.Messages.AddAsync(message);
            await _context.SaveChangesAsync();
        }

        #endregion
    }
} 