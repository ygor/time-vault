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
                // Use either traditional AES encryption or drand-based tlock encryption
                // based on configuration or other requirements
                
                // Check if we should use tlock (based on configuration and unlock time being far enough in the future)
                var shouldUseTlock = unlockDateTime.Value > DateTime.UtcNow.AddMinutes(5);
                
                if (shouldUseTlock)
                {
                    // Calculate the drand round for the unlock time
                    var drandRound = await _drandService.CalculateRoundForTimeAsync(unlockDateTime.Value);
                    
                    // Get the public key for tlock encryption
                    var tlockPublicKey = await _drandService.GetPublicKeyAsync();
                    
                    // Encrypt the content using tlock
                    var encryptedContent = await _drandService.EncryptWithTlockAsync(content, drandRound);
                    
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
                    // Use standard AES encryption for shorter time periods
                    message.EncryptedContent = EncryptContent(content);
                    message.Content = ""; // Use empty string instead of null
                    message.IsEncrypted = true;
                    message.IsTlockEncrypted = false;
                    message.UnlockDateTime = unlockDateTime;
                }
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
                await UnlockMessageInternalAsync(message);
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
                await UnlockMessageInternalAsync(message);
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

            // If the message is already unlocked, it can't be re-encrypted
            if (!message.IsEncrypted)
            {
                message.Title = title;
                message.Content = content;
                message.UnlockDateTime = unlockDateTime;
                
                await _context.SaveChangesAsync();
                return true;
            }

            // If the message is still encrypted, we can update it
            var isEncrypted = unlockDateTime.HasValue && unlockDateTime > DateTime.UtcNow;
            
            if (isEncrypted)
            {
                // Determine if we should use tlock or AES based on unlock time
                var shouldUseTlock = unlockDateTime.Value > DateTime.UtcNow.AddMinutes(5);
                
                if (shouldUseTlock)
                {
                    // Calculate the drand round for the unlock time
                    var drandRound = await _drandService.CalculateRoundForTimeAsync(unlockDateTime.Value);
                    
                    // Get the public key for tlock encryption
                    var tlockPublicKey = await _drandService.GetPublicKeyAsync();
                    
                    // Encrypt the content using tlock
                    var encryptedContent = await _drandService.EncryptWithTlockAsync(content, drandRound);
                    
                    // Update the message with new tlock data
                    message.Title = title;
                    message.Content = "";
                    message.EncryptedContent = encryptedContent;
                    message.IsEncrypted = true;
                    message.IsTlockEncrypted = true;
                    message.DrandRound = drandRound;
                    message.TlockPublicKey = tlockPublicKey;
                    message.UnlockDateTime = unlockDateTime;
                }
                else
                {
                    // Use standard AES encryption
                    message.Title = title;
                    message.EncryptedContent = EncryptContent(content);
                    message.Content = "";
                    message.IsEncrypted = true;
                    message.IsTlockEncrypted = false;
                    message.DrandRound = null;
                    message.TlockPublicKey = null;
                    message.UnlockDateTime = unlockDateTime;
                }
            }
            else
            {
                // No encryption needed
                message.Title = title;
                message.Content = content;
                message.EncryptedContent = ""; // Use empty string instead of null
                message.IsEncrypted = false;
                message.IsTlockEncrypted = false;
                message.DrandRound = null;
                message.TlockPublicKey = null;
                message.UnlockDateTime = unlockDateTime;
            }

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

            await UnlockMessageInternalAsync(message);
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
                await UnlockMessageInternalAsync(message);
            }

            await _context.SaveChangesAsync();

            return unlockedMessages;
        }

        private string EncryptContent(string content)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty; // Return empty string instead of null

            using (var aes = Aes.Create())
            {
                // Generate a random key and IV for each message
                aes.GenerateKey();
                aes.GenerateIV();

                // Save the Key and IV along with the encrypted data
                // In a real app, you might want to protect these keys further
                byte[] encryptedData;

                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new System.IO.MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (var sw = new System.IO.StreamWriter(cs))
                    {
                        sw.Write(content);
                    }
                    encryptedData = ms.ToArray();
                }

                // Format: base64(key):base64(iv):base64(encryptedData)
                return Convert.ToBase64String(aes.Key) + ":" + 
                       Convert.ToBase64String(aes.IV) + ":" + 
                       Convert.ToBase64String(encryptedData);
            }
        }

        private string DecryptContent(string encryptedContent)
        {
            if (string.IsNullOrEmpty(encryptedContent))
                return string.Empty; // Return empty string instead of null

            var parts = encryptedContent.Split(':');
            if (parts.Length != 3)
                return string.Empty; // Return empty string instead of null

            try
            {
                var key = Convert.FromBase64String(parts[0]);
                var iv = Convert.FromBase64String(parts[1]);
                var data = Convert.FromBase64String(parts[2]);

                using (var aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;

                    using (var decryptor = aes.CreateDecryptor())
                    using (var ms = new System.IO.MemoryStream(data))
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (var sr = new System.IO.StreamReader(cs))
                    {
                        return sr.ReadToEnd();
                    }
                }
            }
            catch
            {
                return "[Error: Could not decrypt message]";
            }
        }

        private async Task UnlockMessageInternalAsync(Message message)
        {
            if (!message.IsEncrypted || string.IsNullOrEmpty(message.EncryptedContent))
                return;

            try
            {
                if (message.IsTlockEncrypted && message.DrandRound.HasValue)
                {
                    // Attempt to decrypt using tlock
                    var isRoundAvailable = await _drandService.IsRoundAvailableAsync(message.DrandRound.Value);
                    
                    if (isRoundAvailable)
                    {
                        // Decrypt using tlock and the drand round
                        message.Content = await _drandService.DecryptWithTlockAsync(
                            message.EncryptedContent, 
                            message.DrandRound.Value);
                        
                        message.EncryptedContent = ""; // Use empty string instead of null
                        message.IsEncrypted = false;
                        message.IsTlockEncrypted = false;
                    }
                }
                else
                {
                    // Decrypt using standard AES
                    message.Content = DecryptContent(message.EncryptedContent);
                    message.EncryptedContent = ""; // Use empty string instead of null
                    message.IsEncrypted = false;
                }
            }
            catch (Exception)
            {
                await Task.Run(() => {
                    message.Content = "[Error: Could not decrypt message]";
                    message.EncryptedContent = ""; // Use empty string instead of null
                    message.IsEncrypted = false;
                    message.IsTlockEncrypted = false;
                });
            }
        }
    }
} 