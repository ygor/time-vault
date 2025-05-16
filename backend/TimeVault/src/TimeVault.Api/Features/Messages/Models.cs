using System;
using System.Text.Json.Serialization;

namespace TimeVault.Api.Features.Messages
{
    public class MessageDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool IsEncrypted { get; set; }
        
        // Include IsLocked for compatibility with the test's MessageResponse class
        // This property will be serialized as "isLocked" in JSON
        [JsonPropertyName("isLocked")]
        public bool IsLocked 
        { 
            get => IsEncrypted; 
            set => IsEncrypted = value; 
        }
        
        public bool IsTlockEncrypted { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UnlockDateTime { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public Guid VaultId { get; set; }
    }
} 