using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TimeVault.Core.Services.Interfaces;
using TimeVault.Domain.Entities;
using TimeVault.Infrastructure.Data;

namespace TimeVault.Infrastructure.Services
{
    public class VaultService : IVaultService
    {
        private readonly ApplicationDbContext _context;
        private readonly IKeyVaultService _keyVaultService;

        public VaultService(ApplicationDbContext context, IKeyVaultService keyVaultService)
        {
            _context = context;
            _keyVaultService = keyVaultService;
        }

        public async Task<Vault> CreateVaultAsync(Guid userId, string name, string description)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return new Vault();

            // Generate a unique key pair for this vault
            var (publicKey, privateKey) = await _keyVaultService.GenerateVaultKeyPairAsync();
            
            // Encrypt the private key with the user's key
            var encryptedPrivateKey = await _keyVaultService.EncryptVaultPrivateKeyAsync(privateKey, userId);
            
            var vault = new Vault
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = description,
                CreatedAt = DateTime.UtcNow,
                OwnerId = userId,
                PublicKey = publicKey,
                EncryptedPrivateKey = encryptedPrivateKey
            };

            await _context.Vaults.AddAsync(vault);
            await _context.SaveChangesAsync();

            return vault;
        }

        public async Task<Vault> GetVaultByIdAsync(Guid vaultId, Guid userId)
        {
            // Check if user is the owner
            var vault = await _context.Vaults
                .Include(v => v.Messages)
                .FirstOrDefaultAsync(v => v.Id == vaultId);

            if (vault == null)
                return null; // Return null explicitly

            if (vault.OwnerId == userId)
                return vault;

            // If not owner, check if vault is shared with the user
            var isShared = await _context.VaultShares
                .AnyAsync(vs => vs.VaultId == vaultId && vs.UserId == userId);

            if (isShared)
                return vault;

            // User has no access
            return null; // Return null explicitly
        }

        public async Task<IEnumerable<Vault>> GetUserVaultsAsync(Guid userId)
        {
            return await _context.Vaults
                .Where(v => v.OwnerId == userId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Vault>> GetSharedVaultsAsync(Guid userId)
        {
            return await _context.Vaults
                .Join(_context.VaultShares.Where(vs => vs.UserId == userId),
                      v => v.Id,
                      vs => vs.VaultId,
                      (v, vs) => v)
                .Include(v => v.Owner)
                .ToListAsync();
        }

        public async Task<bool> UpdateVaultAsync(Guid vaultId, Guid userId, string name, string description)
        {
            var vault = await _context.Vaults.FindAsync(vaultId);
            if (vault == null)
                return false;

            // Check if user can edit the vault
            if (!await CanEditVaultAsync(vaultId, userId))
                return false;

            vault.Name = name;
            vault.Description = description;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteVaultAsync(Guid vaultId, Guid userId)
        {
            var vault = await _context.Vaults.FindAsync(vaultId);
            if (vault == null)
                return false;

            // Only the owner can delete a vault
            if (vault.OwnerId != userId)
                return false;

            // Delete related shares
            var shares = await _context.VaultShares
                .Where(vs => vs.VaultId == vaultId)
                .ToListAsync();

            _context.VaultShares.RemoveRange(shares);

            // Delete the vault
            _context.Vaults.Remove(vault);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> ShareVaultAsync(Guid vaultId, Guid ownerUserId, Guid targetUserId, bool canEdit)
        {
            var vault = await _context.Vaults.FindAsync(vaultId);
            if (vault == null || vault.OwnerId != ownerUserId)
                return false;

            // Check if vault is already shared with this user
            var existingShare = await _context.VaultShares
                .FirstOrDefaultAsync(vs => vs.VaultId == vaultId && vs.UserId == targetUserId);

            if (existingShare != null)
            {
                // Update existing share permissions
                existingShare.CanEdit = canEdit;
                await _context.SaveChangesAsync();
                return true;
            }

            // Create new share
            var share = new VaultShare
            {
                Id = Guid.NewGuid(),
                VaultId = vaultId,
                UserId = targetUserId,
                SharedAt = DateTime.UtcNow,
                CanEdit = canEdit
            };

            await _context.VaultShares.AddAsync(share);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> RevokeVaultShareAsync(Guid vaultId, Guid ownerUserId, Guid targetUserId)
        {
            var vault = await _context.Vaults.FindAsync(vaultId);
            if (vault == null || vault.OwnerId != ownerUserId)
                return false;

            var share = await _context.VaultShares
                .FirstOrDefaultAsync(vs => vs.VaultId == vaultId && vs.UserId == targetUserId);

            if (share == null)
                return false;

            _context.VaultShares.Remove(share);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> HasVaultAccessAsync(Guid vaultId, Guid userId)
        {
            // Check if user is the owner
            var isOwner = await _context.Vaults
                .AnyAsync(v => v.Id == vaultId && v.OwnerId == userId);

            if (isOwner)
                return true;

            // Check if vault is shared with the user
            var isShared = await _context.VaultShares
                .AnyAsync(vs => vs.VaultId == vaultId && vs.UserId == userId);

            return isShared;
        }

        public async Task<bool> CanEditVaultAsync(Guid vaultId, Guid userId)
        {
            // Check if user is the owner
            var isOwner = await _context.Vaults
                .AnyAsync(v => v.Id == vaultId && v.OwnerId == userId);

            if (isOwner)
                return true;

            // Check if vault is shared with the user with edit permissions
            var canEdit = await _context.VaultShares
                .AnyAsync(vs => vs.VaultId == vaultId && vs.UserId == userId && vs.CanEdit);

            return canEdit;
        }
        
        // Helper method to get the decrypted private key for a vault
        public async Task<string> GetVaultPrivateKeyAsync(Guid vaultId, Guid userId)
        {
            // First check if the user has access to the vault
            if (!await HasVaultAccessAsync(vaultId, userId))
                throw new UnauthorizedAccessException("User does not have access to this vault");
                
            // Get the vault
            var vault = await _context.Vaults.FindAsync(vaultId);
            if (vault == null)
                throw new InvalidOperationException("Vault not found");
                
            // Decrypt the private key using the user's key
            return await _keyVaultService.DecryptVaultPrivateKeyAsync(vault.EncryptedPrivateKey, userId);
        }
    }
} 