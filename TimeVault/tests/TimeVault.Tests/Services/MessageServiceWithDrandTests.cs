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
    /// Tests for the MessageService class focusing on Drand-based time-lock encryption functionality
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
            // Set up in-memory database
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: $"MessageServiceWithDrandTests_{Guid.NewGuid()}")
                .EnableSensitiveDataLogging()
                .Options;

            _context = new ApplicationDbContext(options);
            
            // Set up mocks
            _mockVaultService = new Mock<IVaultService>();
            _mockDrandService = new Mock<IDrandService>();
            
            // Configure mocks for tests
            SetupVaultServiceMocks();
            SetupDrandServiceMocks();
            
            // Create service with mocks
            _messageService = new MessageService(_context, _mockVaultService.Object, _mockDrandService.Object);
            
            // Seed test data
            SeedTestData();
        }

        [Fact]
        public async Task CreateMessageAsync_ShouldUseTlockEncryption_ForAllEncryptedMessages()
        {
            // Arrange
            var title = "Time-locked Message";
            var content = "This is a secret that will be locked with time-lock encryption";
            var unlockTime = DateTime.UtcNow.AddHours(2); // Future time
            var encryptedContent = "TLOCK_ENCRYPTED_CONTENT";
            
            // Act
            var result = await _messageService.CreateMessageAsync(_vaultId, _userId, title, content, unlockTime);
            
            // Assert
            result.Should().NotBeNull();
            result!.Title.Should().Be(title);
            result.Content.Should().BeEmpty(); // Content should be empty for encrypted messages
            result.EncryptedContent.Should().Be(encryptedContent);
            result.IsEncrypted.Should().BeTrue();
            result.IsTlockEncrypted.Should().BeTrue();
            result.DrandRound.Should().Be(FutureRound);
            result.PublicKeyUsed.Should().NotBeNull();
            result.UnlockTime.Should().Be(unlockTime);
            
            // Verify that Drand encryption was used
            _mockDrandService.Verify(d => d.CalculateRoundForTimeAsync(It.Is<DateTime>(dt => dt == unlockTime)), Times.Once);
            _mockDrandService.Verify(d => d.GetPublicKeyAsync(), Times.Once);
            _mockDrandService.Verify(d => d.EncryptWithTlockAndVaultKeyAsync(content, FutureRound, TestPublicKey), Times.Once);
        }

        [Fact]
        public async Task UnlockMessageAsync_ShouldUseTlockDecryption_ForTlockEncryptedMessages()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            var encryptedContent = "TLOCK_ENCRYPTED_CONTENT";
            var decryptedContent = "This is the decrypted content";
            
            // Create a time-locked message that's ready to be unlocked (round is available)
            await CreateEncryptedMessageInDatabase(messageId, CurrentRound, encryptedContent);
            
            // Configure mock to simulate available round
            _mockDrandService.Setup(d => d.IsRoundAvailableAsync(CurrentRound))
                .ReturnsAsync(true);
                
            _mockDrandService.Setup(d => d.DecryptWithTlockAndVaultKeyAsync(
                    encryptedContent, CurrentRound, TestPrivateKey))
                .ReturnsAsync(decryptedContent);
            
            // Act
            var result = await _messageService.UnlockMessageAsync(messageId, _userId);
            
            // Assert
            result.Should().NotBeNull();
            result!.Content.Should().Be(decryptedContent);
            result.IsEncrypted.Should().BeFalse();
            
            // Verify that Drand decryption was used
            _mockDrandService.Verify(d => d.IsRoundAvailableAsync(CurrentRound), Times.Once);
            _mockDrandService.Verify(d => d.DecryptWithTlockAndVaultKeyAsync(
                encryptedContent, CurrentRound, TestPrivateKey), Times.Once);
        }

        [Fact]
        public async Task UpdateMessageAsync_ShouldUseTlockEncryption_ForAllEncryptedMessages()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            
            // Create an unencrypted message first
            await CreateUnencryptedMessageInDatabase(messageId);
            
            var newTitle = "Updated Time-locked Message";
            var newContent = "This message will be encrypted with time-lock encryption";
            var newUnlockTime = DateTime.UtcNow.AddHours(3); // Future time
            var encryptedContent = "TLOCK_ENCRYPTED_CONTENT_UPDATED";
            
            // Configure mocks
            _mockDrandService.Setup(d => d.CalculateRoundForTimeAsync(newUnlockTime))
                .ReturnsAsync(FutureRound);
                
            _mockDrandService.Setup(d => d.GetPublicKeyAsync())
                .ReturnsAsync("drand-public-key");
                
            _mockDrandService.Setup(d => d.EncryptWithTlockAndVaultKeyAsync(
                    newContent, FutureRound, TestPublicKey))
                .ReturnsAsync(encryptedContent);
            
            // Act
            var result = await _messageService.UpdateMessageAsync(messageId, _userId, newTitle, newContent, newUnlockTime);
            
            // Assert
            result.Should().BeTrue();
            
            // Verify the message was updated in the database
            var updatedMessage = await _context.Messages.FindAsync(messageId);
            updatedMessage.Should().NotBeNull();
            updatedMessage!.Title.Should().Be(newTitle);
            updatedMessage.Content.Should().BeEmpty(); // Content should be empty for encrypted messages
            updatedMessage.EncryptedContent.Should().Be(encryptedContent);
            updatedMessage.IsEncrypted.Should().BeTrue();
            updatedMessage.IsTlockEncrypted.Should().BeTrue();
            updatedMessage.DrandRound.Should().Be(FutureRound);
            updatedMessage.PublicKeyUsed.Should().NotBeNull();
            updatedMessage.UnlockTime.Should().Be(newUnlockTime);
            
            // Verify that Drand encryption was used
            _mockDrandService.Verify(d => d.CalculateRoundForTimeAsync(newUnlockTime), Times.Once);
            _mockDrandService.Verify(d => d.GetPublicKeyAsync(), Times.Once);
            _mockDrandService.Verify(d => d.EncryptWithTlockAndVaultKeyAsync(
                newContent, FutureRound, TestPublicKey), Times.Once);
        }
        
        #region Test Helpers
        
        private void SetupVaultServiceMocks()
        {
            // Mock vault access check
            _mockVaultService.Setup(v => v.HasVaultAccessAsync(_vaultId, _userId))
                .ReturnsAsync(true);
                
            // Mock vault edit permission check
            _mockVaultService.Setup(v => v.CanEditVaultAsync(_vaultId, _userId))
                .ReturnsAsync(true);
                
            // Mock private key retrieval
            _mockVaultService.Setup(v => v.GetVaultPrivateKeyAsync(_vaultId, _userId))
                .ReturnsAsync(TestPrivateKey);
        }
        
        private void SetupDrandServiceMocks()
        {
            // Mock current round
            _mockDrandService.Setup(d => d.GetCurrentRoundAsync())
                .ReturnsAsync(CurrentRound);
                
            // Mock round calculation
            _mockDrandService.Setup(d => d.CalculateRoundForTimeAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(FutureRound);
                
            // Mock public key retrieval
            _mockDrandService.Setup(d => d.GetPublicKeyAsync())
                .ReturnsAsync("drand-public-key");
                
            // Mock encryption
            _mockDrandService.Setup(d => d.EncryptWithTlockAndVaultKeyAsync(
                    It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>()))
                .ReturnsAsync("TLOCK_ENCRYPTED_CONTENT");
                
            // Mock decryption
            _mockDrandService.Setup(d => d.DecryptWithTlockAndVaultKeyAsync(
                    It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>()))
                .ReturnsAsync("DECRYPTED_CONTENT");
        }
        
        private void SeedTestData()
        {
            // Add test user
            _context.Users.Add(new User
            {
                Id = _userId,
                Email = "test@example.com",
                PasswordHash = "hash",
                FirstName = "",
                LastName = "",
                IsAdmin = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            
            // Add test vault
            _context.Vaults.Add(new Vault
            {
                Id = _vaultId,
                Name = "Test Vault",
                Description = "Test vault for time-lock encryption",
                OwnerId = _userId,
                PublicKey = TestPublicKey,
                EncryptedPrivateKey = "encrypted-private-key",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            
            _context.SaveChanges();
        }
        
        private async Task CreateEncryptedMessageInDatabase(Guid messageId, long drandRound, string encryptedContent)
        {
            var now = DateTime.UtcNow;
            var message = new Message
            {
                Id = messageId,
                Title = "Encrypted Test Message",
                Content = "", // Empty for encrypted messages
                EncryptedContent = encryptedContent,
                IV = "test-iv",
                EncryptedKey = "test-key",
                IsEncrypted = true,
                IsTlockEncrypted = true,
                DrandRound = drandRound,
                PublicKeyUsed = "test-public-key",
                VaultId = _vaultId,
                SenderId = _userId,
                CreatedAt = now,
                UpdatedAt = now,
                UnlockTime = now.AddHours(-1) // Already passed unlock time
            };
            
            _context.Messages.Add(message);
            await _context.SaveChangesAsync();
        }
        
        private async Task CreateUnencryptedMessageInDatabase(Guid messageId)
        {
            var now = DateTime.UtcNow;
            var message = new Message
            {
                Id = messageId,
                Title = "Unencrypted Test Message",
                Content = "This is an unencrypted test message",
                EncryptedContent = "", // Empty for unencrypted messages
                IV = "",
                EncryptedKey = "",
                IsEncrypted = false,
                IsTlockEncrypted = false,
                VaultId = _vaultId,
                SenderId = _userId,
                CreatedAt = now,
                UpdatedAt = now
            };
            
            _context.Messages.Add(message);
            await _context.SaveChangesAsync();
        }
        
        #endregion
    }
} 