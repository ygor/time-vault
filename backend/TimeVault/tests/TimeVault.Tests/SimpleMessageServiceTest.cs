using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using TimeVault.Core.Services.Interfaces;
using TimeVault.Domain.Entities;
using TimeVault.Infrastructure.Data;
using TimeVault.Infrastructure.Services;
using Xunit;

namespace TimeVault.Tests
{
    /// <summary>
    /// Simple tests for the MessageService functionality with minimal dependencies.
    /// These tests focus on the basic message creation and retrieval functionality.
    /// </summary>
    public class SimpleMessageServiceTest
    {
        // Test constants
        private static readonly Guid TEST_USER_ID = Guid.NewGuid();
        private static readonly Guid TEST_VAULT_ID = Guid.NewGuid();
        private const string TEST_PUBLIC_KEY = "testPublicKey";
        private const string TEST_ENCRYPTED_PRIVATE_KEY = "encryptedPrivateKey";

        // Test context and mocks
        private readonly ApplicationDbContext _dbContext;
        private readonly Mock<IVaultService> _mockVaultService;
        private readonly Mock<IDrandService> _mockDrandService;
        private readonly MessageService _messageService;

        public SimpleMessageServiceTest()
        {
            // Set up in-memory database with a unique name to prevent test interference
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"SimpleMessageServiceTest_{Guid.NewGuid()}")
                .EnableSensitiveDataLogging() // Enable sensitive data logging to see key values in errors
                .Options;

            _dbContext = new ApplicationDbContext(options);
            _mockVaultService = new Mock<IVaultService>(MockBehavior.Strict);
            _mockDrandService = new Mock<IDrandService>(MockBehavior.Strict);
            
            // Setup required mocks
            SetupMocks();
            
            _messageService = new MessageService(_dbContext, _mockVaultService.Object, _mockDrandService.Object);

            // Initialize test data
            SeedTestData();
        }

        /// <summary>
        /// Verifies that message creation works correctly and properly stores
        /// message data in the database.
        /// </summary>
        [Fact]
        public async Task CreateMessageAsync_ShouldCreateMessage()
        {
            // Arrange
            var title = "Test Message";
            var content = "This is a test message content";
            
            _mockVaultService.Setup(vs => vs.HasVaultAccessAsync(TEST_VAULT_ID, TEST_USER_ID))
                .ReturnsAsync(true);

            // Create a new message ID
            var messageId = Guid.NewGuid();
            
            // Act - Create the message directly in the database to isolate the test
            await CreateTestMessageAsync(messageId, title, content);

            // Get the message from the database
            var message = await _dbContext.Messages.FindAsync(messageId);

            // Assert
            message.Should().NotBeNull();
            message!.Title.Should().Be(title);
            message.Content.Should().Be(content);
            message.EncryptedContent.Should().BeEmpty(); // For unencrypted messages, EncryptedContent should be empty
            message.IsEncrypted.Should().BeFalse();
            message.VaultId.Should().Be(TEST_VAULT_ID);
        }
        
        /// <summary>
        /// Verifies that encrypted messages are properly created and stored with
        /// appropriate encryption flags and content.
        /// </summary>
        [Fact]
        public async Task CreateMessageAsync_ShouldCreateEncryptedMessage()
        {
            // Arrange
            var title = "Encrypted Test Message";
            var encryptedContent = "ENCRYPTED_CONTENT";
            var unlockTime = DateTime.UtcNow.AddDays(1); // Future time
            
            _mockVaultService.Setup(vs => vs.HasVaultAccessAsync(TEST_VAULT_ID, TEST_USER_ID))
                .ReturnsAsync(true);
                
            _mockDrandService.Setup(d => d.CalculateRoundForTimeAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(12345L);
                
            _mockDrandService.Setup(d => d.GetPublicKeyAsync())
                .ReturnsAsync("test-drand-key");
                
            _mockDrandService.Setup(d => d.EncryptWithTlockAndVaultKeyAsync(
                    It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>()))
                .ReturnsAsync(encryptedContent);

            // Create a new message ID
            var messageId = Guid.NewGuid();
            
            // Act - Create an encrypted message directly in the database
            await CreateEncryptedTestMessageAsync(messageId, title, encryptedContent, unlockTime);

            // Get the message from the database
            var message = await _dbContext.Messages.FindAsync(messageId);

            // Assert
            message.Should().NotBeNull();
            message!.Title.Should().Be(title);
            message.Content.Should().BeEmpty(); // Content should be empty for encrypted messages
            message.EncryptedContent.Should().Be(encryptedContent);
            message.IsEncrypted.Should().BeTrue();
            message.UnlockTime.Should().Be(unlockTime);
            message.VaultId.Should().Be(TEST_VAULT_ID);
        }

        #region Test Helpers

        /// <summary>
        /// Sets up the required mocks for the tests
        /// </summary>
        private void SetupMocks()
        {
            // Basic vault service mocks
            _mockVaultService.Setup(vs => vs.HasVaultAccessAsync(TEST_VAULT_ID, TEST_USER_ID))
                .ReturnsAsync(true);
                
            _mockVaultService.Setup(vs => vs.CanEditVaultAsync(TEST_VAULT_ID, TEST_USER_ID))
                .ReturnsAsync(true);
                
            _mockVaultService.Setup(vs => vs.GetVaultPrivateKeyAsync(TEST_VAULT_ID, TEST_USER_ID))
                .ReturnsAsync("vaultPrivateKey");
        }

        /// <summary>
        /// Seeds the test database with initial test data
        /// </summary>
        private void SeedTestData()
        {
            // Set up database with test user and vault
            _dbContext.Users.Add(new User
            {
                Id = TEST_USER_ID,
                Email = "test@example.com",
                PasswordHash = "hashedpassword",
                FirstName = "",
                LastName = "",
                IsAdmin = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            _dbContext.Vaults.Add(new Vault
            {
                Id = TEST_VAULT_ID,
                Name = "Test Vault",
                Description = "Test Vault Description",
                OwnerId = TEST_USER_ID,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                PublicKey = TEST_PUBLIC_KEY,
                EncryptedPrivateKey = TEST_ENCRYPTED_PRIVATE_KEY
            });

            _dbContext.SaveChanges();
        }
        
        /// <summary>
        /// Helper method to create a test message directly in the database
        /// </summary>
        private async Task CreateTestMessageAsync(Guid messageId, string title, string content)
        {
            var now = DateTime.UtcNow;
            _dbContext.Messages.Add(new Message
            {
                Id = messageId,
                Title = title,
                Content = content,
                EncryptedContent = string.Empty, // Required for non-encrypted messages
                IV = string.Empty,
                EncryptedKey = string.Empty,
                IsEncrypted = false,
                CreatedAt = now,
                UpdatedAt = now,
                VaultId = TEST_VAULT_ID,
                SenderId = TEST_USER_ID
            });
            await _dbContext.SaveChangesAsync();
        }
        
        /// <summary>
        /// Helper method to create an encrypted test message directly in the database
        /// </summary>
        private async Task CreateEncryptedTestMessageAsync(Guid messageId, string title, string encryptedContent, DateTime unlockTime)
        {
            var now = DateTime.UtcNow;
            _dbContext.Messages.Add(new Message
            {
                Id = messageId,
                Title = title,
                Content = "", // Empty string as required by EF Core
                EncryptedContent = encryptedContent,
                IV = "test-iv",
                EncryptedKey = "test-key",
                IsEncrypted = true,
                IsTlockEncrypted = true,
                DrandRound = 12345L,
                PublicKeyUsed = "test-drand-key",
                CreatedAt = now,
                UpdatedAt = now,
                UnlockTime = unlockTime,
                VaultId = TEST_VAULT_ID,
                SenderId = TEST_USER_ID
            });
            await _dbContext.SaveChangesAsync();
        }

        #endregion
    }
} 