using System;
using System.Collections.Generic;

namespace TimeVault.Domain.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLogin { get; set; }
        
        // Navigation properties
        public ICollection<Vault> Vaults { get; set; } = new List<Vault>();
    }
} 