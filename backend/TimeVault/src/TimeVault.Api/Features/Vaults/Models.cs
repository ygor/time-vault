using System;
using System.Collections.Generic;

namespace TimeVault.Api.Features.Vaults
{
    // DTO for vault data to be returned to clients
    public class VaultDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public Guid OwnerId { get; set; }
        public string OwnerEmail { get; set; } = string.Empty;
        public int MessageCount { get; set; }
        public int UnreadMessageCount { get; set; }
        public bool IsOwner { get; set; }
        public bool CanEdit { get; set; }
        public List<VaultShareDto> SharedWith { get; set; } = new List<VaultShareDto>();
    }

    // DTO for vault sharing information
    public class VaultShareDto
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public bool CanEdit { get; set; }
        public DateTime SharedAt { get; set; }
    }
} 