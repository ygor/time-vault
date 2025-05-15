using System;
using System.Collections.Generic;

namespace TimeVault.Domain.Entities
{
    public class Vault
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        
        // Foreign keys
        public Guid OwnerId { get; set; }
        
        // Navigation properties
        public User Owner { get; set; }
        public ICollection<Message> Messages { get; set; } = new List<Message>();
    }
} 