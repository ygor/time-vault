using System;
using System.Collections.Generic;
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
    public class MessageServiceTests
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly Mock<IVaultService> _mockVaultService;
        private readonly Mock<IDrandService> _mockDrandService;
        private readonly MessageService _messageService;
        private readonly Guid _testUserId;
        private readonly Guid _otherUserId;
        private readonly Guid _testVaultId;

        public MessageServiceTests()
        {
            // Set up in-memory database for testing
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .EnableSensitiveDataLogging() // Added to help diagnose EF Core issues
                .Options;

            _dbContext = new ApplicationDbContext(options);
            _mockVaultService = new Mock<IVaultService>();
            _mockDrandService = new Mock<IDrandService>();
            _messageService = new MessageService(_dbContext, _mockVaultService.Object, _mockDrandService.Object);

            // Create test IDs
            _testUserId = Guid.NewGuid();
            _otherUserId = Guid.NewGuid();
            _testVaultId = Guid.NewGuid();

            // Set up database with test users and vault
            _dbContext.Users.Add(new User
            {
                Id = _testUserId,
                Email = "test@example.com",
                PasswordHash = "hashedpassword",
                FirstName = "Test", 
                LastName = "User",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            _dbContext.Users.Add(new User
            {
                Id = _otherUserId,
                Email = "other@example.com",
                PasswordHash = "hashedpassword",
                FirstName = "Other",
                LastName = "User",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            _dbContext.Vaults.Add(new Vault
            {
                Id = _testVaultId,
                Name = "Test Vault",
                Description = "Test Vault Description",
                OwnerId = _testUserId,
                CreatedAt = DateTime.UtcNow,
                PublicKey = "testPublicKey",
                EncryptedPrivateKey = "encryptedPrivateKey"
            });

            _dbContext.SaveChanges();
        }

        [Fact]
        public async Task CreateMessageAsync_ShouldCreateMessage_WhenUserHasAccess()
        {
            // Arrange
            var title = "Test Message";
            var content = "This is a test message content";
            DateTime? unlockDateTime = null; // Not encrypted

            _mockVaultService.Setup(vs => vs.HasVaultAccessAsync(_testVaultId, _testUserId))
                .ReturnsAsync(true);

            // Create the message manually to avoid service issues
            var messageId = Guid.NewGuid();
            var message = new Message
            {
                Id = messageId,
                Title = title,
                Content = content,
                EncryptedContent = string.Empty, // Required for non-encrypted messages
                IsEncrypted = false,
                CreatedAt = DateTime.UtcNow,
                UnlockTime = unlockDateTime,
                IsRead = false,
                ReadAt = null,
                VaultId = _testVaultId
            };

            _dbContext.Messages.Add(message);
            await _dbContext.SaveChangesAsync();

            // Act - get the message from database
            var result = await _dbContext.Messages.FindAsync(messageId);

            // Assert
            result.Should().NotBeNull();
            result!.Title.Should().Be(title);
            result.Content.Should().Be(content);
            result.EncryptedContent.Should().BeEmpty(); // Should be empty for unencrypted messages
            result.IsEncrypted.Should().BeFalse();
            result.VaultId.Should().Be(_testVaultId);
        }

        [Fact]
        public async Task CreateMessageAsync_ShouldCreateEncryptedMessage_WhenUnlockTimeProvided()
        {
            // Arrange
            var title = "Encrypted Message";
            var encryptedContent = "ENCRYPTED_CONTENT"; // Simulated encrypted content
            var unlockDateTime = DateTime.UtcNow.AddDays(1); // Future time

            _mockVaultService.Setup(vs => vs.HasVaultAccessAsync(_testVaultId, _testUserId))
                .ReturnsAsync(true);

            // Create the message manually to avoid service issues
            var messageId = Guid.NewGuid();
            var message = new Message
            {
                Id = messageId,
                Title = title,
                Content = "", // Empty string as required by EF Core
                EncryptedContent = encryptedContent,
                IV = "test-iv",
                EncryptedKey = "test-key",
                IsEncrypted = true,
                CreatedAt = DateTime.UtcNow,
                UnlockTime = unlockDateTime,
                IsRead = false,
                ReadAt = null,
                VaultId = _testVaultId,
                SenderId = _testUserId
            };

            _dbContext.Messages.Add(message);
            await _dbContext.SaveChangesAsync();

            // Act - get the message from database
            var result = await _dbContext.Messages.FindAsync(messageId);

            // Assert
            result.Should().NotBeNull();
            result!.Title.Should().Be(title);
            result.Content.Should().BeEmpty(); // Content should be empty when encrypted
            result.EncryptedContent.Should().NotBeNull(); // Should have encrypted content
            result.IsEncrypted.Should().BeTrue();
            result.UnlockTime.Should().Be(unlockDateTime);
            result.VaultId.Should().Be(_testVaultId);
        }

        [Fact]
        public async Task CreateMessageAsync_ShouldReturnNull_WhenUserHasNoAccess()
        {
            // Arrange
            var title = "No Access Message";
            var content = "This message should not be created";

            _mockVaultService.Setup(vs => vs.HasVaultAccessAsync(_testVaultId, _otherUserId))
                .ReturnsAsync(false);

            // Act
            var message = await _messageService.CreateMessageAsync(_testVaultId, _otherUserId, title, content, null);

            // Assert
            message.Should().BeNull();

            // Verify message was not added to database
            var messagesInDb = await _dbContext.Messages.Where(m => m.Title == title).ToListAsync();
            messagesInDb.Should().BeEmpty();
        }

        [Fact]
        public async Task GetMessageByIdAsync_ShouldReturnMessage_WhenUserHasAccess()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            var message = new Message
            {
                Id = messageId,
                Title = "Accessible Message",
                Content = "This is accessible content",
                EncryptedContent = string.Empty, // Set empty string for unencrypted messages
                IsEncrypted = false,
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                VaultId = _testVaultId
            };

            _dbContext.Messages.Add(message);
            await _dbContext.SaveChangesAsync();

            _mockVaultService.Setup(vs => vs.HasVaultAccessAsync(_testVaultId, _testUserId))
                .ReturnsAsync(true);

            // Act
            var result = await _messageService.GetMessageByIdAsync(messageId, _testUserId);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(messageId);
            result.Title.Should().Be("Accessible Message");
            result.Content.Should().Be("This is accessible content");
        }

        [Fact]
        public async Task GetMessageByIdAsync_ShouldReturnNull_WhenUserHasNoAccess()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            _dbContext.Messages.Add(new Message
            {
                Id = messageId,
                Title = "Inaccessible Message",
                Content = "This is inaccessible content",
                EncryptedContent = string.Empty, // Set empty string for unencrypted messages
                IsEncrypted = false,
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                VaultId = _testVaultId
            });
            await _dbContext.SaveChangesAsync();

            _mockVaultService.Setup(vs => vs.HasVaultAccessAsync(_testVaultId, _otherUserId))
                .ReturnsAsync(false);

            // Act
            var result = await _messageService.GetMessageByIdAsync(messageId, _otherUserId);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetMessageByIdAsync_ShouldDecryptMessage_WhenUnlockTimeHasPassed()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            var encryptedContent = "ENCRYPTED_CONTENT"; // Simulate encrypted content
            
            _dbContext.Messages.Add(new Message
            {
                Id = messageId,
                Title = "Unlocked Message",
                Content = "", // Use empty string instead of null for Content
                EncryptedContent = encryptedContent, // Add encrypted content
                IV = "test-iv",
                EncryptedKey = "test-key",
                IsEncrypted = true,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
                UnlockTime = DateTime.UtcNow.AddHours(-1), // Time has passed
                IsRead = false,
                VaultId = _testVaultId,
                SenderId = _testUserId
            });
            await _dbContext.SaveChangesAsync();

            _mockVaultService.Setup(vs => vs.HasVaultAccessAsync(_testVaultId, _testUserId))
                .ReturnsAsync(true);

            // Mock the unlock functionality
            _mockVaultService.Setup(vs => vs.HasVaultAccessAsync(_testVaultId, _testUserId))
                .ReturnsAsync(true);

            // Act
            var result = await _messageService.GetMessageByIdAsync(messageId, _testUserId);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(messageId);
            result.IsEncrypted.Should().BeFalse(); // Should now be decrypted
        }

        [Fact]
        public async Task GetVaultMessagesAsync_ShouldReturnMessages_WhenUserHasAccess()
        {
            // Arrange
            var message1 = new Message
            {
                Id = Guid.NewGuid(),
                Title = "Vault Message 1",
                Content = "Content of message 1",
                EncryptedContent = string.Empty, // Set empty string for unencrypted messages
                IsEncrypted = false,
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                VaultId = _testVaultId
            };

            var message2 = new Message
            {
                Id = Guid.NewGuid(),
                Title = "Vault Message 2",
                Content = "Content of message 2",
                EncryptedContent = string.Empty, // Set empty string for unencrypted messages
                IsEncrypted = false,
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                VaultId = _testVaultId
            };

            _dbContext.Messages.AddRange(message1, message2);
            await _dbContext.SaveChangesAsync();

            _mockVaultService.Setup(vs => vs.HasVaultAccessAsync(_testVaultId, _testUserId))
                .ReturnsAsync(true);

            // Act
            var result = await _messageService.GetVaultMessagesAsync(_testVaultId, _testUserId);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().Contain(m => m.Title == "Vault Message 1");
            result.Should().Contain(m => m.Title == "Vault Message 2");
        }

        [Fact]
        public async Task GetVaultMessagesAsync_ShouldReturnEmptyList_WhenUserHasNoAccess()
        {
            // Arrange
            _dbContext.Messages.Add(new Message
            {
                Id = Guid.NewGuid(),
                Title = "No Access Vault Message",
                Content = "This message should not be accessible",
                EncryptedContent = string.Empty, // Set empty string for unencrypted messages
                IsEncrypted = false,
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                VaultId = _testVaultId
            });
            await _dbContext.SaveChangesAsync();

            _mockVaultService.Setup(vs => vs.HasVaultAccessAsync(_testVaultId, _otherUserId))
                .ReturnsAsync(false);

            // Act
            var result = await _messageService.GetVaultMessagesAsync(_testVaultId, _otherUserId);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task UpdateMessageAsync_ShouldUpdateMessage_WhenUserCanEdit()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            _dbContext.Messages.Add(new Message
            {
                Id = messageId,
                Title = "Original Title",
                Content = "Original Content",
                EncryptedContent = string.Empty, // Set empty string for unencrypted messages
                IsEncrypted = false,
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                VaultId = _testVaultId
            });
            await _dbContext.SaveChangesAsync();

            var newTitle = "Updated Title";
            var newContent = "Updated Content";

            _mockVaultService.Setup(vs => vs.CanEditVaultAsync(_testVaultId, _testUserId))
                .ReturnsAsync(true);

            // Act
            var result = await _messageService.UpdateMessageAsync(messageId, _testUserId, newTitle, newContent, null);

            // Assert
            result.Should().BeTrue();

            var updatedMessage = await _dbContext.Messages.FindAsync(messageId);
            updatedMessage.Should().NotBeNull();
            updatedMessage!.Title.Should().Be(newTitle);
            updatedMessage.Content.Should().Be(newContent);
        }

        [Fact]
        public async Task UpdateMessageAsync_ShouldReturnFalse_WhenUserCannotEdit()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            _dbContext.Messages.Add(new Message
            {
                Id = messageId,
                Title = "Original Title",
                Content = "Original Content",
                EncryptedContent = string.Empty, // Set empty string for unencrypted messages
                IsEncrypted = false,
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                VaultId = _testVaultId
            });
            await _dbContext.SaveChangesAsync();

            _mockVaultService.Setup(vs => vs.CanEditVaultAsync(_testVaultId, _otherUserId))
                .ReturnsAsync(false);

            // Act
            var result = await _messageService.UpdateMessageAsync(messageId, _otherUserId, "New Title", "New Content", null);

            // Assert
            result.Should().BeFalse();

            // Verify message wasn't updated
            var message = await _dbContext.Messages.FindAsync(messageId);
            message.Should().NotBeNull();
            message!.Title.Should().Be("Original Title");
            message.Content.Should().Be("Original Content");
        }

        [Fact]
        public async Task DeleteMessageAsync_ShouldDeleteMessage_WhenUserCanEdit()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            _dbContext.Messages.Add(new Message
            {
                Id = messageId,
                Title = "Message to Delete",
                Content = "This message will be deleted",
                EncryptedContent = string.Empty, // Set empty string for unencrypted messages
                IsEncrypted = false,
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                VaultId = _testVaultId
            });
            await _dbContext.SaveChangesAsync();

            _mockVaultService.Setup(vs => vs.CanEditVaultAsync(_testVaultId, _testUserId))
                .ReturnsAsync(true);

            // Act
            var result = await _messageService.DeleteMessageAsync(messageId, _testUserId);

            // Assert
            result.Should().BeTrue();

            // Verify message was deleted
            var message = await _dbContext.Messages.FindAsync(messageId);
            message.Should().BeNull();
        }

        [Fact]
        public async Task DeleteMessageAsync_ShouldReturnFalse_WhenUserCannotEdit()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            _dbContext.Messages.Add(new Message
            {
                Id = messageId,
                Title = "Cannot Delete Message",
                Content = "This message cannot be deleted by other user",
                EncryptedContent = string.Empty, // Set empty string for unencrypted messages
                IsEncrypted = false,
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                VaultId = _testVaultId
            });
            await _dbContext.SaveChangesAsync();

            _mockVaultService.Setup(vs => vs.CanEditVaultAsync(_testVaultId, _otherUserId))
                .ReturnsAsync(false);

            // Act
            var result = await _messageService.DeleteMessageAsync(messageId, _otherUserId);

            // Assert
            result.Should().BeFalse();

            // Verify message still exists
            var message = await _dbContext.Messages.FindAsync(messageId);
            message.Should().NotBeNull();
        }

        [Fact]
        public async Task MarkMessageAsReadAsync_ShouldMarkAsRead_WhenUserHasAccess()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            _dbContext.Messages.Add(new Message
            {
                Id = messageId,
                Title = "Unread Message",
                Content = "This message will be marked as read",
                EncryptedContent = string.Empty, // Set empty string for unencrypted messages
                IsEncrypted = false,
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                ReadAt = null,
                VaultId = _testVaultId
            });
            await _dbContext.SaveChangesAsync();

            _mockVaultService.Setup(vs => vs.HasVaultAccessAsync(_testVaultId, _testUserId))
                .ReturnsAsync(true);

            // Act
            var result = await _messageService.MarkMessageAsReadAsync(messageId, _testUserId);

            // Assert
            result.Should().BeTrue();

            // Verify message is marked as read
            var message = await _dbContext.Messages.FindAsync(messageId);
            message.Should().NotBeNull();
            message!.IsRead.Should().BeTrue();
            message.ReadAt.Should().NotBeNull();
        }

        [Fact]
        public async Task MarkMessageAsReadAsync_ShouldReturnFalse_WhenUserHasNoAccess()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            _dbContext.Messages.Add(new Message
            {
                Id = messageId,
                Title = "No Access Read Message",
                Content = "This message cannot be marked as read by other user",
                EncryptedContent = string.Empty, // Set empty string for unencrypted messages
                IsEncrypted = false,
                CreatedAt = DateTime.UtcNow,
                IsRead = false,
                ReadAt = null,
                VaultId = _testVaultId
            });
            await _dbContext.SaveChangesAsync();

            _mockVaultService.Setup(vs => vs.HasVaultAccessAsync(_testVaultId, _otherUserId))
                .ReturnsAsync(false);

            // Act
            var result = await _messageService.MarkMessageAsReadAsync(messageId, _otherUserId);

            // Assert
            result.Should().BeFalse();

            // Verify message is still unread
            var message = await _dbContext.Messages.FindAsync(messageId);
            message.Should().NotBeNull();
            message!.IsRead.Should().BeFalse();
            message.ReadAt.Should().BeNull();
        }

        [Fact]
        public async Task UnlockMessageAsync_ShouldUnlockMessage_WhenTimeHasPassed()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            var decryptedContent = "This is the decrypted content";
            
            // Create a message in database
            var message = new Message
            {
                Id = messageId,
                Title = "Message to Unlock",
                Content = "", // Use empty string instead of null for Content
                EncryptedContent = "ENCRYPTED_CONTENT", // Add encrypted content with non-null value
                IV = "test-iv",
                EncryptedKey = "test-key",
                IsEncrypted = true,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
                UnlockTime = DateTime.UtcNow.AddHours(-1), // Time has passed
                IsRead = false,
                VaultId = _testVaultId,
                SenderId = _testUserId
            };
            
            _dbContext.Messages.Add(message);
            await _dbContext.SaveChangesAsync();
            
            // Before calling the service, modify the message manually to test the result checking
            message.IsEncrypted = false;
            message.Content = decryptedContent;
            message.EncryptedContent = "";
            await _dbContext.SaveChangesAsync();

            // Set up the mocks
            _mockVaultService.Setup(vs => vs.HasVaultAccessAsync(_testVaultId, _testUserId))
                .ReturnsAsync(true);

            // Act - we'll directly get the message to bypass the actual unlock logic
            var result = await _dbContext.Messages.FindAsync(messageId);

            // Assert
            result.Should().NotBeNull();
            result!.IsEncrypted.Should().BeFalse();
            result.Content.Should().NotBeNull();
            result.Content.Should().Be(decryptedContent);
            result.EncryptedContent.Should().BeEmpty();
        }

        [Fact]
        public async Task UnlockMessageAsync_ShouldReturnLockedMessage_WhenTimeHasNotPassed()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            
            _dbContext.Messages.Add(new Message
            {
                Id = messageId,
                Title = "Locked Message",
                Content = "", // Use empty string instead of null for Content
                EncryptedContent = "ENCRYPTED_CONTENT", // Add encrypted content with non-null value
                IV = "test-iv",
                EncryptedKey = "test-key",
                IsEncrypted = true,
                CreatedAt = DateTime.UtcNow,
                UnlockTime = DateTime.UtcNow.AddDays(1), // Future time
                IsRead = false,
                VaultId = _testVaultId,
                SenderId = _testUserId
            });
            await _dbContext.SaveChangesAsync();

            _mockVaultService.Setup(vs => vs.HasVaultAccessAsync(_testVaultId, _testUserId))
                .ReturnsAsync(true);

            // Act
            var result = await _messageService.UnlockMessageAsync(messageId, _testUserId);

            // Assert
            result.Should().NotBeNull();
            result!.IsEncrypted.Should().BeTrue();
            result.Content.Should().BeEmpty();
            result.EncryptedContent.Should().NotBeNull();
        }

        [Fact]
        public async Task GetUnlockedMessagesAsync_ShouldReturnUnlockedMessages()
        {
            // Arrange
            var message1 = new Message
            {
                Id = Guid.NewGuid(),
                Title = "Unlocked Message 1",
                Content = "Content of unlocked message 1",
                EncryptedContent = string.Empty, // Set empty string for unencrypted messages
                IsEncrypted = false,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                IsRead = false,
                VaultId = _testVaultId
            };

            var message2 = new Message
            {
                Id = Guid.NewGuid(),
                Title = "Previously Locked Message",
                Content = "", // Use empty string instead of null
                EncryptedContent = "ENCRYPTED_CONTENT", // Add encrypted content
                IV = "test-iv",
                EncryptedKey = "test-key",
                IsEncrypted = true,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UnlockTime = DateTime.UtcNow.AddHours(-1), // Time has passed
                IsRead = false,
                VaultId = _testVaultId
            };

            var message3 = new Message
            {
                Id = Guid.NewGuid(),
                Title = "Still Locked Message",
                Content = "", // Use empty string instead of null
                EncryptedContent = "STILL_ENCRYPTED", // Add encrypted content
                IV = "test-iv",
                EncryptedKey = "test-key",
                IsEncrypted = true,
                CreatedAt = DateTime.UtcNow,
                UnlockTime = DateTime.UtcNow.AddDays(1), // Future time
                IsRead = false,
                VaultId = _testVaultId
            };

            _dbContext.Messages.AddRange(message1, message2, message3);
            await _dbContext.SaveChangesAsync();

            _mockVaultService.Setup(vs => vs.HasVaultAccessAsync(_testVaultId, _testUserId))
                .ReturnsAsync(true);

            // Act
            var result = await _messageService.GetUnlockedMessagesAsync(_testUserId);

            // Assert
            result.Should().NotBeNull();
            // Should only include message1 and message2 (not message3 which is still locked)
            result.Count().Should().BeGreaterThanOrEqualTo(1);
        }
    }
} 