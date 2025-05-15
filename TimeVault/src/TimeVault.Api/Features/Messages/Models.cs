using System;

namespace TimeVault.Api.Features.Messages
{
    public class MessageDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public bool IsEncrypted { get; set; }
        public bool IsTlockEncrypted { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UnlockDateTime { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public Guid VaultId { get; set; }
    }
} 