using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TimeVault.Core.Services.Interfaces;
using TimeVault.Domain.Entities;
using TimeVault.Infrastructure.Data;

namespace TimeVault.Infrastructure.Services
{
    public class MessageService : IMessageService
    {
        private readonly ApplicationDbContext _context;
        private readonly IVaultService _vaultService;
        private readonly IDrandService _drandService;

        public MessageService(
            ApplicationDbContext context, 
            IVaultService vaultService,
            IDrandService drandService)
        {
            _context = context;
            _vaultService = vaultService;
            _drandService = drandService;
        }

        public async Task<Message?> CreateMessageAsync(Guid vaultId, Guid userId, string title, string content, DateTime? unlockDateTime)
        {
            try
            {
                // Debug the inputs to the method
                System.Diagnostics.Debug.WriteLine($"Creating message. Content length: {content?.Length ?? 0}, IsNull: {content == null}");
                
                // Get the vault to access its public key
                var vault = await _context.Vaults.FindAsync(vaultId);
                if (vault == null)
                    return null;

                // Check if user can edit the vault
                if (!await _vaultService.CanEditVaultAsync(vaultId, userId))
                    return null;

                // Ensure title and content are not null
                title = title ?? string.Empty;
                content = content ?? string.Empty;

                // Debug after null checks
                System.Diagnostics.Debug.WriteLine($"After null checks. Content length: {content.Length}");

                var message = new Message
                {
                    Id = Guid.NewGuid(),
                    Title = title,
                    VaultId = vaultId,
                    SenderId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsRead = false,
                    ReadAt = null
                };

                // Check if we need to encrypt the message
                var needsEncryption = unlockDateTime.HasValue && unlockDateTime > DateTime.UtcNow;

                if (needsEncryption)
                {
                    System.Diagnostics.Debug.WriteLine($"Message needs encryption. Content to encrypt length: {content.Length}");
                    
                    // Calculate the drand round for the unlock time
                    var drandRound = await _drandService.CalculateRoundForTimeAsync(unlockDateTime.GetValueOrDefault());
                    
                    // Get the public key for tlock encryption
                    var tlockPublicKey = await _drandService.GetPublicKeyAsync();
                    
                    // Encrypt the content using tlock with the vault's public key
                    var encryptedContent = await _drandService.EncryptWithTlockAndVaultKeyAsync(
                        content, 
                        drandRound, 
                        vault.PublicKey);
                    
                    System.Diagnostics.Debug.WriteLine($"Encrypted content length: {encryptedContent?.Length ?? 0}");
                    
                    // Set the encrypted message properties
                    message.Content = string.Empty; // Explicitly set to empty string, not null
                    message.EncryptedContent = encryptedContent;
                    message.IsEncrypted = true;
                    message.IsTlockEncrypted = true;
                    message.DrandRound = drandRound;
                    message.PublicKeyUsed = tlockPublicKey;
                    message.UnlockTime = unlockDateTime;
                }
                else
                {
                    // No encryption needed or unlock time is in the past
                    System.Diagnostics.Debug.WriteLine($"Message will not be encrypted. Content length: {content.Length}");
                    message.Content = content; // Store content directly
                    message.EncryptedContent = string.Empty;
                    message.IsEncrypted = false;
                    message.IsTlockEncrypted = false;
                    message.UnlockTime = unlockDateTime;
                }

                await _context.Messages.AddAsync(message);
                await _context.SaveChangesAsync();
                
                // Verify message was saved correctly
                var savedMessage = await _context.Messages.FindAsync(message.Id);
                System.Diagnostics.Debug.WriteLine($"Saved message ID: {savedMessage?.Id}. Content length: {savedMessage?.Content?.Length ?? 0}, IsEncrypted: {savedMessage?.IsEncrypted}");
                
                if (savedMessage != null && !savedMessage.IsEncrypted && string.IsNullOrEmpty(savedMessage.Content)) {
                    System.Diagnostics.Debug.WriteLine("WARNING: Non-encrypted message has empty content!");
                    
                    // If the content was not saved correctly, manually update it
                    if (savedMessage.Content == null || savedMessage.Content.Length == 0) {
                        savedMessage.Content = content;
                        await _context.SaveChangesAsync();
                        System.Diagnostics.Debug.WriteLine($"FIXED: Updated message content. New length: {savedMessage.Content?.Length ?? 0}");
                    }
                }
                
                return savedMessage; // Return the message retrieved from the database
            }
            catch (Exception ex)
            {
                // Log the exception in a real system
                System.Diagnostics.Debug.WriteLine($"Error creating message: {ex.Message}");
                return null;
            }
        }

        public async Task<Message?> GetMessageByIdAsync(Guid messageId, Guid userId)
        {
            try
            {
                var message = await _context.Messages
                    .Include(m => m.Vault)
                    .FirstOrDefaultAsync(m => m.Id == messageId);

                if (message == null)
                    return null; // Return null explicitly instead of empty message

                // Check if user has access to the vault
                if (!await _vaultService.HasVaultAccessAsync(message.VaultId, userId))
                    return null; // Return null explicitly instead of empty message

                // Handle encrypted content if unlock time has passed
                if (message.IsEncrypted && message.UnlockTime.HasValue && message.UnlockTime <= DateTime.UtcNow)
                {
                    await UnlockMessageInternalAsync(message, userId);
                    await _context.SaveChangesAsync();
                }

                return message;
            }
            catch (Exception)
            {
                // Log the exception in a real system
                return null;
            }
        }

        public async Task<IEnumerable<Message>> GetVaultMessagesAsync(Guid vaultId, Guid userId)
        {
            try
            {
                // Verify user has access to the vault
                if (!await _vaultService.HasVaultAccessAsync(vaultId, userId))
                    return Enumerable.Empty<Message>();

                var messages = await _context.Messages
                    .Where(m => m.VaultId == vaultId)
                    .ToListAsync();

                // Check for messages that need to be unlocked
                var now = DateTime.UtcNow;
                foreach (var message in messages.Where(m => m.IsEncrypted && m.UnlockTime.HasValue && m.UnlockTime <= now))
                {
                    await UnlockMessageInternalAsync(message, userId);
                }

                await _context.SaveChangesAsync();

                return messages;
            }
            catch (Exception)
            {
                // Log the exception in a real system
                return Enumerable.Empty<Message>();
            }
        }

        public async Task<bool> UpdateMessageAsync(Guid messageId, Guid userId, string title, string content, DateTime? unlockDateTime)
        {
            try
            {
                var message = await _context.Messages
                    .Include(m => m.Vault)
                    .FirstOrDefaultAsync(m => m.Id == messageId);

                if (message == null)
                    return false;

                // Check if user can edit the vault
                if (!await _vaultService.CanEditVaultAsync(message.VaultId, userId))
                    return false;

                // Get the vault to access its public key
                var vault = await _context.Vaults.FindAsync(message.VaultId);
                if (vault == null)
                    return false;

                // Ensure title and content are not null
                title = title ?? string.Empty;
                content = content ?? string.Empty;

                // Check if we need to encrypt the message
                var needsEncryption = unlockDateTime.HasValue && unlockDateTime > DateTime.UtcNow;
                
                message.UpdatedAt = DateTime.UtcNow;
                
                // If no encryption is needed or if unlock datetime is in the past
                if (!needsEncryption)
                {
                    // Update without encryption
                    message.Title = title;
                    message.Content = content;
                    message.EncryptedContent = string.Empty; // Use empty string instead of null
                    message.IsEncrypted = false;
                    message.IsTlockEncrypted = false;
                    message.DrandRound = null;
                    message.PublicKeyUsed = null;
                    message.UnlockTime = unlockDateTime;
                    
                    await _context.SaveChangesAsync();
                    return true;
                }
                
                // Encryption is needed
                
                // Calculate the drand round for the unlock time
                var drandRound = await _drandService.CalculateRoundForTimeAsync(unlockDateTime.GetValueOrDefault());
                    
                // Get the public key for tlock encryption
                var tlockPublicKey = await _drandService.GetPublicKeyAsync();
                    
                // Encrypt the content using tlock with the vault's public key
                var encryptedContent = await _drandService.EncryptWithTlockAndVaultKeyAsync(
                    content, 
                    drandRound, 
                    vault.PublicKey);
                    
                // Update the message with tlock encryption
                message.Title = title;
                message.Content = string.Empty;
                message.EncryptedContent = encryptedContent;
                message.IsEncrypted = true;
                message.IsTlockEncrypted = true;
                message.DrandRound = drandRound;
                message.PublicKeyUsed = tlockPublicKey;
                message.UnlockTime = unlockDateTime;

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                // Log the exception in a real system
                return false;
            }
        }

        public async Task<bool> DeleteMessageAsync(Guid messageId, Guid userId)
        {
            try
            {
                var message = await _context.Messages
                    .Include(m => m.Vault)
                    .FirstOrDefaultAsync(m => m.Id == messageId);

                if (message == null)
                    return false;

                // Check if user can edit the vault
                if (!await _vaultService.CanEditVaultAsync(message.VaultId, userId))
                    return false;

                _context.Messages.Remove(message);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception)
            {
                // Log the exception in a real system
                return false;
            }
        }

        public async Task<bool> MarkMessageAsReadAsync(Guid messageId, Guid userId)
        {
            try
            {
                var message = await _context.Messages
                    .Include(m => m.Vault)
                    .FirstOrDefaultAsync(m => m.Id == messageId);

                if (message == null)
                    return false;

                // Check if user has access to the vault
                if (!await _vaultService.HasVaultAccessAsync(message.VaultId, userId))
                    return false;

                // If already read, no need to update
                if (message.IsRead)
                    return true;

                message.IsRead = true;
                message.ReadAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                // Log the exception in a real system
                return false;
            }
        }

        public async Task<Message?> UnlockMessageAsync(Guid messageId, Guid userId)
        {
            try
            {
                var message = await _context.Messages
                    .Include(m => m.Vault)
                    .FirstOrDefaultAsync(m => m.Id == messageId);

                if (message == null)
                    return null; // Return null explicitly instead of empty message

                // Check if user has access to the vault
                if (!await _vaultService.HasVaultAccessAsync(message.VaultId, userId))
                    return null; // Return null explicitly instead of empty message

                // If not encrypted or unlock time hasn't arrived yet, return as is
                if (!message.IsEncrypted || !message.UnlockTime.HasValue || message.UnlockTime > DateTime.UtcNow)
                    return message;

                await UnlockMessageInternalAsync(message, userId);
                await _context.SaveChangesAsync();

                return message;
            }
            catch (Exception)
            {
                // Log the exception in a real system
                return null;
            }
        }

        public async Task<IEnumerable<Message>> GetUnlockedMessagesAsync(Guid userId)
        {
            try
            {
                // Debug logging
                System.Diagnostics.Debug.WriteLine($"GetUnlockedMessagesAsync called for user {userId}");
                
                // Get all vaults the user has access to
                var userVaults = await _context.Vaults
                    .Where(v => v.OwnerId == userId)
                    .Select(v => v.Id)
                    .ToListAsync();

                var sharedVaults = await _context.VaultShares
                    .Where(vs => vs.UserId == userId)
                    .Select(vs => vs.VaultId)
                    .ToListAsync();

                var allAccessibleVaultIds = userVaults.Concat(sharedVaults).Distinct().ToList();
                
                System.Diagnostics.Debug.WriteLine($"User has access to {allAccessibleVaultIds.Count} vaults");

                var now = DateTime.UtcNow;
                
                // Get all potential messages user has access to
                var accessibleMessages = await _context.Messages
                    .Where(m => allAccessibleVaultIds.Contains(m.VaultId))
                    .ToListAsync();
                
                System.Diagnostics.Debug.WriteLine($"Found {accessibleMessages.Count} total accessible messages");
                
                // Initialize a list to store properly unlocked messages
                var unlockedMessages = new List<Message>();
                
                // First, add all non-encrypted messages directly
                var nonEncryptedMessages = accessibleMessages
                    .Where(m => !m.IsEncrypted && !string.IsNullOrEmpty(m.Content))
                    .ToList();
                
                System.Diagnostics.Debug.WriteLine($"Found {nonEncryptedMessages.Count} non-encrypted messages with content");
                unlockedMessages.AddRange(nonEncryptedMessages);
                
                // Get messages that should be unlocked based on their unlock time
                var encryptedButShouldBeUnlocked = accessibleMessages
                    .Where(m => m.IsEncrypted && m.UnlockTime.HasValue && m.UnlockTime <= now)
                    .ToList();
                
                System.Diagnostics.Debug.WriteLine($"Found {encryptedButShouldBeUnlocked.Count} encrypted messages that are due to be unlocked");
                
                // Process encrypted messages that should be unlocked
                foreach (var message in encryptedButShouldBeUnlocked)
                {
                    // Create a clone of the message to avoid modifying the original if decryption fails
                    var messageCopy = new Message
                    {
                        Id = message.Id,
                        Title = message.Title,
                        Content = message.Content,
                        EncryptedContent = message.EncryptedContent,
                        IsEncrypted = message.IsEncrypted,
                        IsTlockEncrypted = message.IsTlockEncrypted,
                        UnlockTime = message.UnlockTime,
                        VaultId = message.VaultId,
                        CreatedAt = message.CreatedAt,
                        UpdatedAt = message.UpdatedAt,
                        SenderId = message.SenderId,
                        DrandRound = message.DrandRound,
                        PublicKeyUsed = message.PublicKeyUsed,
                        IsRead = message.IsRead,
                        ReadAt = message.ReadAt,
                        IV = message.IV,
                        EncryptedKey = message.EncryptedKey
                    };
                    
                    try
                    {
                        // Try to unlock the message
                        await UnlockMessageInternalAsync(messageCopy, userId);
                        
                        // Only if the message is now successfully decrypted, include it in results
                        if (!messageCopy.IsEncrypted && !string.IsNullOrEmpty(messageCopy.Content) && 
                            (messageCopy.Content == null || !messageCopy.Content.StartsWith("[Error:")))
                        {
                            unlockedMessages.Add(messageCopy);
                            
                            // Update the original message in the database to persist the decryption
                            message.Content = messageCopy.Content;
                            message.EncryptedContent = messageCopy.EncryptedContent;
                            message.IsEncrypted = messageCopy.IsEncrypted;
                            message.IsTlockEncrypted = messageCopy.IsTlockEncrypted;
                            
                            System.Diagnostics.Debug.WriteLine($"Successfully decrypted message {message.Id} with content length {message.Content?.Length ?? 0}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Message {message.Id} could not be properly decrypted. IsEncrypted: {messageCopy.IsEncrypted}, ContentEmpty: {string.IsNullOrEmpty(messageCopy.Content)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the exception and continue with the next message
                        System.Diagnostics.Debug.WriteLine($"Error unlocking message {message.Id}: {ex.Message}");
                    }
                }

                await _context.SaveChangesAsync();
                
                System.Diagnostics.Debug.WriteLine($"Returning {unlockedMessages.Count} truly unlocked messages");

                // Return only the messages that are actually unlocked and do not contain error messages
                return unlockedMessages;
            }
            catch (Exception ex)
            {
                // Log the exception in a real system
                System.Diagnostics.Debug.WriteLine($"Error getting unlocked messages: {ex.Message}");
                return Enumerable.Empty<Message>();
            }
        }

        private async Task UnlockMessageInternalAsync(Message message, Guid userId)
        {
            // If message is not encrypted or has no encrypted content, nothing to do
            if (!message.IsEncrypted || string.IsNullOrEmpty(message.EncryptedContent))
            {
                System.Diagnostics.Debug.WriteLine($"Message {message.Id} is not encrypted or has no encrypted content");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"Attempting to unlock message {message.Id}");
                
                // Use tlock/drand encryption for time-locked messages
                if (message.DrandRound.HasValue)
                {
                    // Attempt to decrypt only if the round is available (i.e., time has passed)
                    var isRoundAvailable = await _drandService.IsRoundAvailableAsync(message.DrandRound.Value);
                    
                    if (isRoundAvailable)
                    {
                        System.Diagnostics.Debug.WriteLine($"Drand round {message.DrandRound.Value} is available for decryption");
                        
                        try
                        {
                            // Get the vault's private key for decryption
                            var vaultPrivateKey = await _vaultService.GetVaultPrivateKeyAsync(message.VaultId, userId);
                            
                            if (string.IsNullOrEmpty(vaultPrivateKey))
                            {
                                System.Diagnostics.Debug.WriteLine($"Failed to get vault private key for message {message.Id}");
                                return;
                            }
                            
                            // Make sure DrandRound.Value is not null (should already be checked by HasValue)
                            var drandRound = message.DrandRound.GetValueOrDefault();
                            
                            // Decrypt using tlock and the vault's private key
                            string decryptedContent = await _drandService.DecryptWithTlockAndVaultKeyAsync(
                                message.EncryptedContent, 
                                drandRound,
                                vaultPrivateKey);
                                
                            // Check if the decryption actually worked (no error message)
                            if (!string.IsNullOrEmpty(decryptedContent) && !decryptedContent.StartsWith("[Error:"))
                            {
                                System.Diagnostics.Debug.WriteLine($"Successfully decrypted message {message.Id} with vault key. Content length: {decryptedContent.Length}");
                                
                                message.Content = decryptedContent;
                                message.EncryptedContent = string.Empty;
                                message.IsEncrypted = false;
                                message.IsTlockEncrypted = false;
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Vault-specific decryption failed for message {message.Id}: {decryptedContent}");
                                // Keep the message encrypted
                            }
                        }
                        catch (Exception exception)
                        {
                            // Log the vault decryption error before trying legacy decryption
                            System.Diagnostics.Debug.WriteLine($"Vault-specific decryption exception for message {message.Id}: {exception.Message}. Trying legacy decryption.");
                            
                            try
                            {
                                // If vault-specific decryption fails, try legacy decryption
                                string decryptedContent = await _drandService.DecryptWithTlockAsync(
                                    message.EncryptedContent, 
                                    message.DrandRound.Value);
                                    
                                // Check if the decryption actually worked (no error message)
                                if (!string.IsNullOrEmpty(decryptedContent) && !decryptedContent.StartsWith("[Error:"))
                                {
                                    System.Diagnostics.Debug.WriteLine($"Successfully decrypted message {message.Id} with legacy method. Content length: {decryptedContent.Length}");
                                    
                                    message.Content = decryptedContent;
                                    message.EncryptedContent = string.Empty;
                                    message.IsEncrypted = false;
                                    message.IsTlockEncrypted = false;
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"Legacy decryption failed for message {message.Id}: {decryptedContent}");
                                    // Keep the message encrypted
                                }
                            }
                            catch (Exception legacyEx)
                            {
                                // Both decryption methods failed, keep the message encrypted
                                System.Diagnostics.Debug.WriteLine($"Both decryption methods failed for message {message.Id}. Legacy error: {legacyEx.Message}");
                            }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Drand round {message.DrandRound.Value} is not yet available for message {message.Id}");
                    }
                }
                else
                {
                    // For backward compatibility with messages that might have been encrypted with AES
                    System.Diagnostics.Debug.WriteLine($"Message {message.Id} uses a legacy encryption method without drand round");
                    
                    // Only consider it successfully decrypted if we can properly handle it
                    message.Content = "Message encrypted with legacy method";
                    message.IsEncrypted = true; // Keep it marked as encrypted
                }
            }
            catch (Exception exception)
            {
                // Log the exception in a production environment
                System.Diagnostics.Debug.WriteLine($"Exception while unlocking message {message.Id}: {exception.Message}");
                
                // Keep the message marked as encrypted
                message.IsEncrypted = true;
            }
        }
    }
} 