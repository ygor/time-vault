using System;

namespace TimeVault.Domain.Entities
{
    public class Message
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string? EncryptedContent { get; set; }
        public string? IV { get; set; }
        public string? EncryptedKey { get; set; }
        public bool IsEncrypted { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? UnlockTime { get; set; }
        
        // Drand specific properties
        public long? DrandRound { get; set; }
        public string? PublicKeyUsed { get; set; }
        public bool IsTlockEncrypted { get; set; }
        
        // Status properties
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        
        // Foreign keys
        public Guid VaultId { get; set; }
        public Guid SenderId { get; set; }
        
        // Navigation properties
        public Vault? Vault { get; set; }
        public User? Sender { get; set; }
    }
} 