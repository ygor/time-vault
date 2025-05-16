using System;

namespace TimeVault.Api.Features.Auth
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLogin { get; set; }
    }
    
    public class AuthResult
    {
        public bool Success { get; set; }
        public string Token { get; set; } = string.Empty;
        public UserDto? User { get; set; }
        public DateTime Expiration { get; set; }
        public string Error { get; set; } = string.Empty;
    }
} 