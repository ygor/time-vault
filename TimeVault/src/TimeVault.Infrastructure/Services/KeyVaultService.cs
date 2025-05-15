using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TimeVault.Core.Services.Interfaces;
using TimeVault.Infrastructure.Data;

namespace TimeVault.Infrastructure.Services
{
    public class KeyVaultService : IKeyVaultService
    {
        private readonly ApplicationDbContext _context;
        private readonly byte[] _masterKey; // In production, this should be stored in a secure vault or HSM

        public KeyVaultService(ApplicationDbContext context)
        {
            _context = context;
            
            // In a real production environment, this master key would be stored in Azure Key Vault, 
            // AWS KMS, or a hardware security module (HSM)
            // For this implementation, we're using a hardcoded key as an example
            _masterKey = Encoding.UTF8.GetBytes("TimeVault-Master-Encryption-Key-For-User-Keys-!@#$%^&*()_+");
        }

        public async Task<(string publicKey, string privateKey)> GenerateVaultKeyPairAsync()
        {
            // This would be awaitable in a real implementation where key generation might be delegated
            return await Task.Run(() =>
            {
                using var rsa = RSA.Create(2048);
                var privateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey());
                var publicKey = Convert.ToBase64String(rsa.ExportRSAPublicKey());
                
                return (publicKey, privateKey);
            });
        }

        public async Task<string> EncryptVaultPrivateKeyAsync(string privateKey, Guid userId)
        {
            // Get the user-specific key
            byte[] userKey = await DeriveUserKeyAsync(userId);
            
            // Encrypt the private key with the user's key
            byte[] privateKeyBytes = Convert.FromBase64String(privateKey);
            byte[] encryptedKey = EncryptWithAes(privateKeyBytes, userKey);
            
            return Convert.ToBase64String(encryptedKey);
        }

        public async Task<string> DecryptVaultPrivateKeyAsync(string encryptedPrivateKey, Guid userId)
        {
            // Get the user-specific key
            byte[] userKey = await DeriveUserKeyAsync(userId);
            
            // Decrypt the private key with the user's key
            byte[] encryptedKeyBytes = Convert.FromBase64String(encryptedPrivateKey);
            byte[] privateKeyBytes = DecryptWithAes(encryptedKeyBytes, userKey);
            
            return Convert.ToBase64String(privateKeyBytes);
        }

        public async Task<byte[]> EncryptWithVaultPublicKeyAsync(byte[] data, string vaultPublicKey)
        {
            return await Task.Run(() =>
            {
                using var rsa = RSA.Create();
                rsa.ImportRSAPublicKey(Convert.FromBase64String(vaultPublicKey), out _);
                
                // Use RSA for encrypting the data
                // Note: RSA has size limitations for encryption
                // For larger data, a hybrid approach with symmetric keys would be used
                return rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA256);
            });
        }

        public async Task<byte[]> DecryptWithVaultPrivateKeyAsync(byte[] encryptedData, string vaultPrivateKey)
        {
            return await Task.Run(() =>
            {
                using var rsa = RSA.Create();
                rsa.ImportRSAPrivateKey(Convert.FromBase64String(vaultPrivateKey), out _);
                
                // Decrypt the data using RSA
                return rsa.Decrypt(encryptedData, RSAEncryptionPadding.OaepSHA256);
            });
        }

        public async Task<byte[]> DeriveUserKeyAsync(Guid userId)
        {
            // Get the user to extract salt from their data
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                throw new InvalidOperationException("User not found");
            
            // Use the user's ID and password hash as salt
            // In a production system, we'd use a separate salt stored securely
            byte[] userSalt = Encoding.UTF8.GetBytes(user.Id.ToString() + user.PasswordHash);
            
            // Derive a key using PBKDF2
            using var pbkdf2 = new Rfc2898DeriveBytes(
                _masterKey,
                userSalt,
                100000, // High iteration count for security
                HashAlgorithmName.SHA256);
            
            return pbkdf2.GetBytes(32); // 256-bit key
        }

        #region Helper Methods
        
        private byte[] EncryptWithAes(byte[] data, byte[] key)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.GenerateIV(); // Create a random IV
            
            // We need to include the IV with the encrypted data so we can decrypt later
            using var encryptor = aes.CreateEncryptor();
            using var memoryStream = new MemoryStream();
            
            // First, write the IV
            memoryStream.Write(aes.IV, 0, aes.IV.Length);
            
            // Then encrypt the data
            using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
            {
                cryptoStream.Write(data, 0, data.Length);
                cryptoStream.FlushFinalBlock();
            }
            
            return memoryStream.ToArray();
        }
        
        private byte[] DecryptWithAes(byte[] encryptedData, byte[] key)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            
            // Extract the IV from the beginning of the encrypted data
            byte[] iv = new byte[aes.IV.Length];
            Buffer.BlockCopy(encryptedData, 0, iv, 0, iv.Length);
            aes.IV = iv;
            
            // The rest is the actual encrypted data
            using var decryptor = aes.CreateDecryptor();
            using var memoryStream = new MemoryStream();
            
            // Create decryption stream
            using (var inputStream = new MemoryStream(encryptedData, iv.Length, encryptedData.Length - iv.Length))
            using (var cryptoStream = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read))
            {
                // Read the decrypted data
                byte[] buffer = new byte[1024];
                int bytesRead;
                while ((bytesRead = cryptoStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    memoryStream.Write(buffer, 0, bytesRead);
                }
            }
            
            return memoryStream.ToArray();
        }
        
        #endregion
    }
} 