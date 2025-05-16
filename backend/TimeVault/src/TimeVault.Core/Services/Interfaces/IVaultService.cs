using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TimeVault.Domain.Entities;

namespace TimeVault.Core.Services.Interfaces
{
    public interface IVaultService
    {
        Task<Vault?> CreateVaultAsync(Guid userId, string name, string description);
        Task<Vault?> GetVaultByIdAsync(Guid vaultId, Guid userId);
        Task<IEnumerable<Vault>> GetUserVaultsAsync(Guid userId);
        Task<IEnumerable<Vault>> GetSharedVaultsAsync(Guid userId);
        Task<bool> UpdateVaultAsync(Guid vaultId, Guid userId, string name, string description);
        Task<bool> DeleteVaultAsync(Guid vaultId, Guid userId);
        Task<bool> ShareVaultAsync(Guid vaultId, Guid ownerUserId, Guid targetUserId, bool canEdit);
        Task<bool> RevokeVaultShareAsync(Guid vaultId, Guid ownerUserId, Guid targetUserId);
        Task<bool> HasVaultAccessAsync(Guid vaultId, Guid userId);
        Task<bool> CanEditVaultAsync(Guid vaultId, Guid userId);
        Task<string> GetVaultPrivateKeyAsync(Guid vaultId, Guid userId);
        Task<bool> VaultExistsAsync(Guid vaultId);
    }
} 