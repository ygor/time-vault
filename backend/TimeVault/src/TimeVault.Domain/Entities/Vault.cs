using System;
using System.Collections.Generic;

namespace TimeVault.Domain.Entities
{
    public class Vault
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Vault-specific cryptographic keys
        public string PublicKey { get; set; } = string.Empty;
        public string EncryptedPrivateKey { get; set; } = string.Empty;
        
        // Foreign keys
        public Guid OwnerId { get; set; }
        
        // Navigation properties
        public User? Owner { get; set; }
        public ICollection<Message> Messages { get; set; } = new List<Message>();
        public ICollection<VaultShare> SharedWith { get; set; } = new List<VaultShare>();
    }
} 