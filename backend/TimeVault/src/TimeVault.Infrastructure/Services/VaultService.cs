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

        public async Task<Vault?> CreateVaultAsync(Guid userId, string name, string description)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return null; // Return null to indicate error

            try
            {
                // Generate a unique key pair for this vault
                var (publicKey, privateKey) = await _keyVaultService.GenerateVaultKeyPairAsync();
                
                // Encrypt the private key with the user's key
                var encryptedPrivateKey = await _keyVaultService.EncryptVaultPrivateKeyAsync(privateKey, userId);
                
                var now = DateTime.UtcNow;
                var vault = new Vault
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Description = description,
                    CreatedAt = now,
                    UpdatedAt = now,
                    OwnerId = userId,
                    PublicKey = publicKey,
                    EncryptedPrivateKey = encryptedPrivateKey
                };

                await _context.Vaults.AddAsync(vault);
                await _context.SaveChangesAsync();

                return vault;
            }
            catch (Exception)
            {
                // Log the exception in a real system
                return null;
            }
        }

        public async Task<Vault?> GetVaultByIdAsync(Guid vaultId, Guid userId)
        {
            // Check if user is the owner or has shared access
            var vault = await _context.Vaults
                .Include(v => v.Messages)
                .Include(v => v.SharedWith)
                    .ThenInclude(vs => vs.User)
                .Include(v => v.Owner)
                .FirstOrDefaultAsync(v => v.Id == vaultId);

            if (vault == null)
                return null; // Vault not found

            if (vault.OwnerId == userId)
                return vault; // User is the owner, return vault

            // If not owner, check if vault is shared with the user
            var isShared = vault.SharedWith.Any(vs => vs.UserId == userId);

            if (isShared)
                return vault; // Vault is shared with user, return vault

            // User has no access
            throw new UnauthorizedAccessException("User does not have access to this vault");
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
            vault.UpdatedAt = DateTime.UtcNow;

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
            // Load the vault with its relationships
            var vault = await _context.Vaults
                .Include(v => v.Owner)
                .Include(v => v.SharedWith)
                .ThenInclude(vs => vs.User)
                .FirstOrDefaultAsync(v => v.Id == vaultId);
                
            if (vault == null || vault.OwnerId != ownerUserId)
                return false;

            // Check if vault is already shared with this user
            var existingShare = vault.SharedWith.FirstOrDefault(vs => vs.UserId == targetUserId);
            
            var now = DateTime.UtcNow;
            
            if (existingShare != null)
            {
                // Update existing share permissions
                existingShare.CanEdit = canEdit;
                existingShare.UpdatedAt = now;
                await _context.SaveChangesAsync();
                return true;
            }

            // Get target user
            var targetUser = await _context.Users.FindAsync(targetUserId);
            if (targetUser == null)
                return false;

            // Create new share
            var share = new VaultShare
            {
                Id = Guid.NewGuid(),
                VaultId = vaultId,
                UserId = targetUserId,
                User = targetUser,
                Vault = vault,
                CreatedAt = now,
                UpdatedAt = now,
                CanEdit = canEdit
            };

            // Add the share to the vault's collection explicitly
            vault.SharedWith.Add(share);
            
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

        // New method to check if a vault exists without checking permissions
        public async Task<bool> VaultExistsAsync(Guid vaultId)
        {
            return await _context.Vaults.AnyAsync(v => v.Id == vaultId);
        }
    }
} 