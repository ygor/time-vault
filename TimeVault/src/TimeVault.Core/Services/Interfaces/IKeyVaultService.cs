using System;
using System.Threading.Tasks;

namespace TimeVault.Core.Services.Interfaces
{
    public interface IKeyVaultService
    {
        /// <summary>
        /// Generates a new RSA key pair for a vault
        /// </summary>
        /// <returns>Tuple containing (publicKey, privateKey)</returns>
        Task<(string publicKey, string privateKey)> GenerateVaultKeyPairAsync();
        
        /// <summary>
        /// Encrypts the vault's private key using the user's secret key
        /// </summary>
        Task<string> EncryptVaultPrivateKeyAsync(string privateKey, Guid userId);
        
        /// <summary>
        /// Decrypts the vault's private key using the user's secret key
        /// </summary>
        Task<string> DecryptVaultPrivateKeyAsync(string encryptedPrivateKey, Guid userId);
        
        /// <summary>
        /// Encrypts data with the vault's public key
        /// </summary>
        Task<byte[]> EncryptWithVaultPublicKeyAsync(byte[] data, string vaultPublicKey);
        
        /// <summary>
        /// Decrypts data with the vault's private key
        /// </summary>
        Task<byte[]> DecryptWithVaultPrivateKeyAsync(byte[] encryptedData, string vaultPrivateKey);
        
        /// <summary>
        /// Generates a unique user key derived from their password and salt
        /// </summary>
        Task<byte[]> DeriveUserKeyAsync(Guid userId);
    }
} 