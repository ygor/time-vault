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
    public class MessageServiceWithDrandTests
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<IVaultService> _mockVaultService;
        private readonly Mock<IDrandService> _mockDrandService;
        private readonly MessageService _messageService;
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _vaultId = Guid.NewGuid();

        public MessageServiceWithDrandTests()
        {
            // Set up in-memory database
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .EnableSensitiveDataLogging()
                .Options;

            _context = new ApplicationDbContext(options);
            _mockVaultService = new Mock<IVaultService>();
            _mockDrandService = new Mock<IDrandService>();

            // Set up common mocks
            _mockVaultService.Setup(v => v.HasVaultAccessAsync(_vaultId, _userId))
                .ReturnsAsync(true);
            _mockVaultService.Setup(v => v.CanEditVaultAsync(_vaultId, _userId))
                .ReturnsAsync(true);

            // Set up drand service mocks
            _mockDrandService.Setup(d => d.GetCurrentRoundAsync())
                .ReturnsAsync(1000L);
            
            _mockDrandService.Setup(d => d.CalculateRoundForTimeAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(1200L);
            
            _mockDrandService.Setup(d => d.GetPublicKeyAsync())
                .ReturnsAsync("test-public-key");
            
            _mockDrandService.Setup(d => d.EncryptWithTlockAsync(It.IsAny<string>(), It.IsAny<long>()))
                .ReturnsAsync("tlock-encrypted-content");
            
            _mockDrandService.Setup(d => d.DecryptWithTlockAsync(It.IsAny<string>(), It.IsAny<long>()))
                .ReturnsAsync("decrypted-content");
            
            _mockDrandService.Setup(d => d.IsRoundAvailableAsync(It.IsAny<long>()))
                .ReturnsAsync(true);

            _messageService = new MessageService(_context, _mockVaultService.Object, _mockDrandService.Object);

            // Set up the database with test user and vault
            _context.Users.Add(new User
            {
                Id = _userId,
                Username = "testuser",
                Email = "test@example.com",
                PasswordHash = "hashedpassword",
                CreatedAt = DateTime.UtcNow
            });

            _context.Vaults.Add(new Vault
            {
                Id = _vaultId,
                Name = "Test Vault",
                Description = "Test Description",
                OwnerId = _userId,
                CreatedAt = DateTime.UtcNow
            });

            _context.SaveChanges();
        }

        [Fact]
        public async Task CreateMessageAsync_ShouldUseTlockEncryption_ForAllEncryptedMessages()
        {
            // Arrange
            var title = "Tlock Encrypted Message";
            var content = "This is a message encrypted with tlock";
            var unlockTime = DateTime.UtcNow.AddMinutes(2); // Even short unlock times now use tlock

            // Act
            var message = await _messageService.CreateMessageAsync(_vaultId, _userId, title, content, unlockTime);

            // Assert
            message.Should().NotBeNull();
            message.Title.Should().Be(title);
            message.IsEncrypted.Should().BeTrue();
            message.IsTlockEncrypted.Should().BeTrue();
            message.DrandRound.Should().NotBeNull();
            message.TlockPublicKey.Should().NotBeNull();
            message.EncryptedContent.Should().NotBeNullOrEmpty();
            message.Content.Should().BeEmpty(); // Content should be empty when encrypted
            
            // Verify drand service was called correctly
            _mockDrandService.Verify(d => d.CalculateRoundForTimeAsync(unlockTime), Times.Once);
            _mockDrandService.Verify(d => d.GetPublicKeyAsync(), Times.Once);
            _mockDrandService.Verify(d => d.EncryptWithTlockAsync(content, It.IsAny<long>()), Times.Once);
        }

        [Fact]
        public async Task UnlockMessageAsync_ShouldUseTlockDecryption_ForTlockEncryptedMessages()
        {
            // Arrange
            var originalContent = "This is tlock encrypted content";
            var messageId = Guid.NewGuid();
            var drandRound = 50L; // A round that's already available (1000L is current)
            var encryptedContent = $"TLOCK_ENCRYPTED({originalContent},round={drandRound})";

            // Create a tlock-encrypted message directly in the database
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
            
            // Override mock setup for the decryption method with exact input parameters
            _mockDrandService.Setup(d => d.DecryptWithTlockAsync(encryptedContent, drandRound))
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
            _mockDrandService.Verify(d => d.DecryptWithTlockAsync(encryptedContent, drandRound), Times.Once);
        }
        
        [Fact]
        public async Task UpdateMessageAsync_ShouldUseTlockEncryption_ForAllEncryptedMessages()
        {
            // Arrange
            var messageId = Guid.NewGuid();
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
            
            // Now update it with an unlock time (which will trigger encryption)
            var newTitle = "Updated Tlock Message";
            var newContent = "Updated content with tlock encryption";
            var newUnlockTime = DateTime.UtcNow.AddMinutes(2); // Even short unlock times now use tlock
            var drandRound = 1200L;
            var publicKey = "test-public-key";
            var encryptedContent = "tlock-encrypted-content";
            
            // Mock setup
            _mockDrandService.Setup(d => d.CalculateRoundForTimeAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(drandRound);
            _mockDrandService.Setup(d => d.GetPublicKeyAsync())
                .ReturnsAsync(publicKey);
            _mockDrandService.Setup(d => d.EncryptWithTlockAsync(newContent, drandRound))
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
            updatedMessage.DrandRound.Should().Be(drandRound);
            updatedMessage.TlockPublicKey.Should().Be(publicKey);
            updatedMessage.EncryptedContent.Should().Be(encryptedContent);
            updatedMessage.Content.Should().BeEmpty();
            
            // Verify drand service was called
            _mockDrandService.Verify(d => d.CalculateRoundForTimeAsync(It.IsAny<DateTime>()), Times.Once);
            _mockDrandService.Verify(d => d.GetPublicKeyAsync(), Times.Once);
            _mockDrandService.Verify(d => d.EncryptWithTlockAsync(newContent, drandRound), Times.Once);
        }
    }
} 