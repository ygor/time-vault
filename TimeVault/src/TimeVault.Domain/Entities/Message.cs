using System;

namespace TimeVault.Domain.Entities
{
    public class Message
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public string EncryptedContent { get; set; }
        public bool IsEncrypted { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UnlockDateTime { get; set; }
        
        // Drand specific properties
        public long? DrandRound { get; set; }
        public string? TlockPublicKey { get; set; }
        public bool IsTlockEncrypted { get; set; }
        
        // Status properties
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        
        // Foreign keys
        public Guid VaultId { get; set; }
        
        // Navigation properties
        public Vault Vault { get; set; }
    }
} 