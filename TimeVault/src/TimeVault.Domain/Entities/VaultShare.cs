using System;

namespace TimeVault.Domain.Entities
{
    public class VaultShare
    {
        public Guid Id { get; set; }
        
        // Foreign keys
        public Guid UserId { get; set; }
        public Guid VaultId { get; set; }
        
        // Navigation properties
        public User User { get; set; }
        public Vault Vault { get; set; }
        
        // Additional properties
        public DateTime SharedAt { get; set; }
        public bool CanEdit { get; set; }
    }
} 