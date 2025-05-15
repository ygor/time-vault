using System;
using System.Threading.Tasks;
using TimeVault.Domain.Entities;

namespace TimeVault.Core.Services.Interfaces
{
    public interface IAuthService
    {
        Task<(bool Success, string Token, User User, string Error)> LoginAsync(string email, string password);
        Task<(bool Success, string Token, User User, string Error)> RegisterAsync(string email, string password);
        Task<(bool Success, string Token, string Error)> RefreshTokenAsync(string token);
        Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
        Task<bool> CreateAdminUserIfNotExists(string email, string password);
    }
} 