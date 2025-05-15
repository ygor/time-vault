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

        public async Task<Message> CreateMessageAsync(Guid vaultId, Guid userId, string title, string content, DateTime? unlockDateTime)
        {
            // Check if user has access to the vault
            if (!await _vaultService.HasVaultAccessAsync(vaultId, userId))
                return null; // Return null explicitly instead of empty message

            // Get the vault to access its public key
            var vault = await _context.Vaults.FindAsync(vaultId);
            if (vault == null)
                return null;

            var message = new Message
            {
                Id = Guid.NewGuid(),
                Title = title,
                VaultId = vaultId,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            // If an unlock time is provided, encrypt the message
            if (unlockDateTime.HasValue)
            {
                // Always use drand-based tlock encryption for all messages
                
                // Calculate the drand round for the unlock time
                var drandRound = await _drandService.CalculateRoundForTimeAsync(unlockDateTime.Value);
                
                // Get the public key for tlock encryption
                var tlockPublicKey = await _drandService.GetPublicKeyAsync();
                
                // Encrypt the content using tlock with the vault's public key
                var encryptedContent = await _drandService.EncryptWithTlockAndVaultKeyAsync(
                    content, 
                    drandRound, 
                    vault.PublicKey);
                
                // Store the encrypted content and tlock metadata
                message.Content = ""; // Empty string instead of null
                message.EncryptedContent = encryptedContent;
                message.IsEncrypted = true;
                message.IsTlockEncrypted = true;
                message.DrandRound = drandRound;
                message.TlockPublicKey = tlockPublicKey;
                message.UnlockDateTime = unlockDateTime;
            }
            else
            {
                // No encryption needed
                message.Content = content;
                message.EncryptedContent = ""; // Use empty string instead of null
                message.IsEncrypted = false;
                message.IsTlockEncrypted = false;
            }

            await _context.Messages.AddAsync(message);
            await _context.SaveChangesAsync();

            return message;
        }

        public async Task<Message> GetMessageByIdAsync(Guid messageId, Guid userId)
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
            if (message.IsEncrypted && message.UnlockDateTime.HasValue && message.UnlockDateTime <= DateTime.UtcNow)
            {
                await UnlockMessageInternalAsync(message, userId);
                await _context.SaveChangesAsync();
            }

            return message;
        }

        public async Task<IEnumerable<Message>> GetVaultMessagesAsync(Guid vaultId, Guid userId)
        {
            // Verify user has access to the vault
            if (!await _vaultService.HasVaultAccessAsync(vaultId, userId))
                return Enumerable.Empty<Message>();

            var messages = await _context.Messages
                .Where(m => m.VaultId == vaultId)
                .ToListAsync();

            // Check for messages that need to be unlocked
            var now = DateTime.UtcNow;
            foreach (var message in messages.Where(m => m.IsEncrypted && m.UnlockDateTime.HasValue && m.UnlockDateTime <= now))
            {
                await UnlockMessageInternalAsync(message, userId);
            }

            await _context.SaveChangesAsync();

            return messages;
        }

        public async Task<bool> UpdateMessageAsync(Guid messageId, Guid userId, string title, string content, DateTime? unlockDateTime)
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

            // Check if we need to encrypt the message
            var needsEncryption = unlockDateTime.HasValue && unlockDateTime > DateTime.UtcNow;
            
            // If no encryption is needed or if unlock datetime is in the past
            if (!needsEncryption)
            {
                // Update without encryption
                message.Title = title;
                message.Content = content;
                message.EncryptedContent = ""; // Use empty string instead of null
                message.IsEncrypted = false;
                message.IsTlockEncrypted = false;
                message.DrandRound = null;
                message.TlockPublicKey = null;
                message.UnlockDateTime = unlockDateTime;
                
                await _context.SaveChangesAsync();
                return true;
            }
            
            // Encryption is needed
            
            // Calculate the drand round for the unlock time
            var drandRound = await _drandService.CalculateRoundForTimeAsync(unlockDateTime.Value);
                
            // Get the public key for tlock encryption
            var tlockPublicKey = await _drandService.GetPublicKeyAsync();
                
            // Encrypt the content using tlock with the vault's public key
            var encryptedContent = await _drandService.EncryptWithTlockAndVaultKeyAsync(
                content, 
                drandRound, 
                vault.PublicKey);
                
            // Update the message with tlock encryption
            message.Title = title;
            message.Content = "";
            message.EncryptedContent = encryptedContent;
            message.IsEncrypted = true;
            message.IsTlockEncrypted = true;
            message.DrandRound = drandRound;
            message.TlockPublicKey = tlockPublicKey;
            message.UnlockDateTime = unlockDateTime;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteMessageAsync(Guid messageId, Guid userId)
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

        public async Task<bool> MarkMessageAsReadAsync(Guid messageId, Guid userId)
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

        public async Task<Message> UnlockMessageAsync(Guid messageId, Guid userId)
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
            if (!message.IsEncrypted || !message.UnlockDateTime.HasValue || message.UnlockDateTime > DateTime.UtcNow)
                return message;

            await UnlockMessageInternalAsync(message, userId);
            await _context.SaveChangesAsync();

            return message;
        }

        public async Task<IEnumerable<Message>> GetUnlockedMessagesAsync(Guid userId)
        {
            // Get all vaults the user has access to
            var userVaults = await _context.Vaults
                .Where(v => v.OwnerId == userId)
                .Select(v => v.Id)
                .ToListAsync();

            var sharedVaults = await _context.VaultShares
                .Where(vs => vs.UserId == userId)
                .Select(vs => vs.VaultId)
                .ToListAsync();

            var allAccessibleVaultIds = userVaults.Concat(sharedVaults).Distinct();

            // Get messages that should be unlocked now
            var now = DateTime.UtcNow;
            var unlockedMessages = await _context.Messages
                .Where(m => allAccessibleVaultIds.Contains(m.VaultId) &&
                            m.IsEncrypted &&
                            m.UnlockDateTime.HasValue &&
                            m.UnlockDateTime <= now)
                .ToListAsync();

            // Unlock all messages
            foreach (var message in unlockedMessages)
            {
                await UnlockMessageInternalAsync(message, userId);
            }

            await _context.SaveChangesAsync();

            return unlockedMessages;
        }

        private async Task UnlockMessageInternalAsync(Message message, Guid userId)
        {
            if (!message.IsEncrypted || string.IsNullOrEmpty(message.EncryptedContent))
                return;

            try
            {
                // With our update, all encrypted messages should use tlock/drand encryption
                if (message.DrandRound.HasValue)
                {
                    // Attempt to decrypt using tlock
                    var isRoundAvailable = await _drandService.IsRoundAvailableAsync(message.DrandRound.Value);
                    
                    if (isRoundAvailable)
                    {
                        try
                        {
                            // Get the vault's private key for decryption
                            var vaultPrivateKey = await _vaultService.GetVaultPrivateKeyAsync(message.VaultId, userId);
                            
                            // Decrypt using tlock and the vault's private key
                            message.Content = await _drandService.DecryptWithTlockAndVaultKeyAsync(
                                message.EncryptedContent, 
                                message.DrandRound.Value,
                                vaultPrivateKey);
                        }
                        catch (Exception ex)
                        {
                            // If vault-specific decryption fails, try legacy decryption
                            message.Content = await _drandService.DecryptWithTlockAsync(
                                message.EncryptedContent, 
                                message.DrandRound.Value);
                        }
                        
                        message.EncryptedContent = ""; // Use empty string instead of null
                        message.IsEncrypted = false;
                        message.IsTlockEncrypted = false;
                    }
                }
                else
                {
                    // For backward compatibility with messages that might have been encrypted with AES
                    // This block should only run for legacy data from before the migration to exclusive drand usage
                    message.Content = "[This message was encrypted with a deprecated method]";
                    message.EncryptedContent = ""; // Use empty string instead of null
                    message.IsEncrypted = false;
                }
            }
            catch (Exception ex)
            {
                await Task.Run(() => {
                    message.Content = $"[Error: Could not decrypt message: {ex.Message}]";
                    message.EncryptedContent = ""; // Use empty string instead of null
                    message.IsEncrypted = false;
                    message.IsTlockEncrypted = false;
                });
            }
        }
    }
} 