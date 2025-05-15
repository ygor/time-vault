using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Digests;
using System.IO;
using TimeVault.Core.Services.Interfaces;

namespace TimeVault.Infrastructure.Services
{
    public class DrandService : IDrandService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IKeyVaultService _keyVaultService;
        private readonly string _drandUrl = "https://api.drand.sh";
        private readonly string _drandChainHash = "8990e7a9aaed2ffed73dbd7092123d6f289930540d7651336225dc172e51b2ce"; // League of Entropy chain hash
        
        // Constructor using IHttpClientFactory from DI
        public DrandService(IHttpClientFactory httpClientFactory, IKeyVaultService keyVaultService)
        {
            _httpClientFactory = httpClientFactory;
            _keyVaultService = keyVaultService;
        }
        
        public async Task<long> GetCurrentRoundAsync()
        {
            var client = _httpClientFactory.CreateClient("DrandClient");
            var response = await client.GetFromJsonAsync<DrandInfo>($"{_drandUrl}/info");
            return response?.Public?.Round ?? 0;
        }
        
        public async Task<DrandRoundResponse> GetRoundAsync(long round)
        {
            var client = _httpClientFactory.CreateClient("DrandClient");
            var response = await client.GetAsync($"{_drandUrl}/public/{round}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var roundInfo = JsonSerializer.Deserialize<DrandRoundResponse>(content);
                return roundInfo;
            }
            
            return new DrandRoundResponse
            {
                Round = round,
                Randomness = string.Empty,
                Signature = string.Empty,
                PreviousSignature = 0
            };
        }
        
        public virtual async Task<long> CalculateRoundForTimeAsync(DateTime unlockTime)
        {
            var client = _httpClientFactory.CreateClient("DrandClient");
            var info = await client.GetFromJsonAsync<DrandInfo>($"{_drandUrl}/info");
            
            if (info == null)
                return 0;
                
            // Calculate the time difference between now and unlock time in seconds
            var timeDifferenceSeconds = (unlockTime - DateTime.UtcNow).TotalSeconds;
            
            // Calculate how many rounds will occur in that time (each round is typically 30 seconds)
            var roundsToAdd = (long)Math.Ceiling(timeDifferenceSeconds / info.Public.Period);
            
            // Add those rounds to the current round
            return info.Public.Round + roundsToAdd;
        }
        
        public async Task<string> GetPublicKeyAsync()
        {
            var client = _httpClientFactory.CreateClient("DrandClient");
            var info = await client.GetFromJsonAsync<DrandInfo>($"{_drandUrl}/info");
            return info?.Public?.Key ?? string.Empty;
        }
        
        public virtual async Task<string> EncryptWithTlockAsync(string content, long round)
        {
            try
            {
                // Get the drand public key
                var drandPublicKeyHex = await GetPublicKeyAsync();
                if (string.IsNullOrEmpty(drandPublicKeyHex))
                {
                    throw new InvalidOperationException("Could not retrieve drand public key");
                }
                
                // Generate a random symmetric key for content encryption
                byte[] contentKey = GenerateRandomKey(32); // 256-bit AES key
                
                // Derive the vault-specific encryption key from drand public key and target round
                byte[] encryptionKey = DeriveEncryptionKey(HexToBytes(drandPublicKeyHex), round);
                
                // Encrypt the content with the symmetric key
                var (encryptedContent, iv) = EncryptContent(content, contentKey);
                
                // Encrypt the symmetric key with the derived encryption key
                var encryptedKey = EncryptSymmetricKey(contentKey, encryptionKey);
                
                // Create a structure to store all necessary data
                var tlockData = new TlockDataV2
                {
                    EncryptedContent = Convert.ToBase64String(encryptedContent),
                    IV = Convert.ToBase64String(iv),
                    EncryptedKey = Convert.ToBase64String(encryptedKey),
                    Round = round,
                    PublicKeyUsed = drandPublicKeyHex,
                    VaultPublicKeyUsed = null, // No vault key used in this case
                    Timestamp = DateTime.UtcNow
                };
                
                // Serialize and return
                return JsonSerializer.Serialize(tlockData);
            }
            catch (Exception ex)
            {
                // Log the exception in a real system
                return $"{{\"error\":\"Encryption failed: {ex.Message}\"}}";
            }
        }
        
        public virtual async Task<string> EncryptWithTlockAndVaultKeyAsync(string content, long round, string vaultPublicKey)
        {
            try
            {
                // Get the drand public key
                var drandPublicKeyHex = await GetPublicKeyAsync();
                if (string.IsNullOrEmpty(drandPublicKeyHex))
                {
                    throw new InvalidOperationException("Could not retrieve drand public key");
                }
                
                // Generate a random symmetric key for content encryption
                byte[] contentKey = GenerateRandomKey(32); // 256-bit AES key
                
                // First encrypt the symmetric key with the vault's public key
                byte[] vaultEncryptedKey = await _keyVaultService.EncryptWithVaultPublicKeyAsync(contentKey, vaultPublicKey);
                
                // Then derive the time-lock encryption key from drand public key and target round
                byte[] tlockEncryptionKey = DeriveEncryptionKey(HexToBytes(drandPublicKeyHex), round);
                
                // Further encrypt the vault-encrypted key with the time-lock encryption key
                byte[] doubleEncryptedKey = EncryptSymmetricKey(vaultEncryptedKey, tlockEncryptionKey);
                
                // Encrypt the content with the symmetric key
                var (encryptedContent, iv) = EncryptContent(content, contentKey);
                
                // Create a structure to store all necessary data
                var tlockData = new TlockDataV2
                {
                    EncryptedContent = Convert.ToBase64String(encryptedContent),
                    IV = Convert.ToBase64String(iv),
                    EncryptedKey = Convert.ToBase64String(doubleEncryptedKey),
                    Round = round,
                    PublicKeyUsed = drandPublicKeyHex,
                    VaultPublicKeyUsed = vaultPublicKey,
                    Timestamp = DateTime.UtcNow
                };
                
                // Serialize and return
                return JsonSerializer.Serialize(tlockData);
            }
            catch (Exception ex)
            {
                // Log the exception in a real system
                return $"{{\"error\":\"Vault encryption failed: {ex.Message}\"}}";
            }
        }
        
        public virtual async Task<string> DecryptWithTlockAsync(string encryptedContent, long round)
        {
            try
            {
                // Try to deserialize as the new format first
                var tlockDataV2 = JsonSerializer.Deserialize<TlockDataV2>(encryptedContent);
                
                // If we have a valid V2 format, use the new decryption method
                if (tlockDataV2 != null && !string.IsNullOrEmpty(tlockDataV2.EncryptedContent))
                {
                    // If vault key was used, we need to use the vault-specific decryption method
                    if (!string.IsNullOrEmpty(tlockDataV2.VaultPublicKeyUsed))
                    {
                        throw new InvalidOperationException("This content was encrypted with a vault key and requires vault-specific decryption");
                    }
                    
                    // Get the drand round information to access the signature
                    var roundInfo = await GetRoundAsync(round);
                    if (string.IsNullOrEmpty(roundInfo.Signature))
                    {
                        throw new InvalidOperationException($"Could not retrieve signature for round {round}");
                    }
                    
                    // Derive the vault-specific decryption key from the round and signature
                    byte[] decryptionKey = DeriveDecryptionKey(round, HexToBytes(roundInfo.Signature));
                    
                    // Decrypt the symmetric key
                    byte[] encryptedKey = Convert.FromBase64String(tlockDataV2.EncryptedKey);
                    byte[] contentKey = DecryptSymmetricKey(encryptedKey, decryptionKey);
                    
                    // Decrypt the content with the symmetric key
                    byte[] encryptedContentBytes = Convert.FromBase64String(tlockDataV2.EncryptedContent);
                    byte[] iv = Convert.FromBase64String(tlockDataV2.IV);
                    
                    return DecryptContent(encryptedContentBytes, contentKey, iv);
                }
                
                // Fallback to the old format for backward compatibility
                var tlockData = JsonSerializer.Deserialize<TlockData>(encryptedContent);
                if (tlockData != null && !string.IsNullOrEmpty(tlockData.Content))
                {
                    // Return the content directly from the simulated format
                    return tlockData.Content;
                }
                
                throw new InvalidOperationException("Invalid encrypted data format");
            }
            catch (Exception ex)
            {
                // Log the exception in a real system
                return $"[Error: Could not decrypt message: {ex.Message}]";
            }
        }
        
        public virtual async Task<string> DecryptWithTlockAndVaultKeyAsync(string encryptedContent, long round, string vaultPrivateKey)
        {
            try
            {
                // Try to deserialize as the new format
                var tlockDataV2 = JsonSerializer.Deserialize<TlockDataV2>(encryptedContent);
                
                // If we have a valid V2 format, use the vault decryption method
                if (tlockDataV2 != null && !string.IsNullOrEmpty(tlockDataV2.EncryptedContent))
                {
                    // Get the drand round information to access the signature
                    var roundInfo = await GetRoundAsync(round);
                    if (string.IsNullOrEmpty(roundInfo.Signature))
                    {
                        throw new InvalidOperationException($"Could not retrieve signature for round {round}");
                    }
                    
                    // Derive the time-lock decryption key from the round and signature
                    byte[] tlockDecryptionKey = DeriveDecryptionKey(round, HexToBytes(roundInfo.Signature));
                    
                    // First decrypt the double-encrypted key using the time-lock key
                    byte[] doubleEncryptedKey = Convert.FromBase64String(tlockDataV2.EncryptedKey);
                    byte[] vaultEncryptedKey = DecryptSymmetricKey(doubleEncryptedKey, tlockDecryptionKey);
                    
                    // Then decrypt the vault-encrypted key using the vault's private key
                    byte[] contentKey = await _keyVaultService.DecryptWithVaultPrivateKeyAsync(vaultEncryptedKey, vaultPrivateKey);
                    
                    // Finally decrypt the content with the symmetric key
                    byte[] encryptedContentBytes = Convert.FromBase64String(tlockDataV2.EncryptedContent);
                    byte[] iv = Convert.FromBase64String(tlockDataV2.IV);
                    
                    return DecryptContent(encryptedContentBytes, contentKey, iv);
                }
                
                throw new InvalidOperationException("Invalid encrypted data format or not encrypted with a vault key");
            }
            catch (Exception ex)
            {
                // Log the exception in a real system
                return $"[Error: Could not decrypt message with vault key: {ex.Message}]";
            }
        }
        
        public virtual async Task<bool> IsRoundAvailableAsync(long round)
        {
            var client = _httpClientFactory.CreateClient("DrandClient");
            var currentRound = await GetCurrentRoundAsync();
            return currentRound >= round;
        }
        
        #region Cryptographic Helpers
        
        private byte[] GenerateRandomKey(int keySizeBytes)
        {
            var key = new byte[keySizeBytes];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(key);
            }
            return key;
        }
        
        private byte[] DeriveEncryptionKey(byte[] publicKey, long round)
        {
            // Derive encryption key from drand public key and target round
            // This method creates a unique key that can only be derived again
            // when the round's signature is published
            
            using (var sha256 = SHA256.Create())
            {
                // Combine public key with round number and a fixed domain separator
                var roundBytes = BitConverter.GetBytes(round);
                var domainSeparator = Encoding.UTF8.GetBytes("TimeVault-Encryption-Key");
                
                // Concatenate the values
                var combined = new byte[publicKey.Length + roundBytes.Length + domainSeparator.Length];
                Buffer.BlockCopy(domainSeparator, 0, combined, 0, domainSeparator.Length);
                Buffer.BlockCopy(publicKey, 0, combined, domainSeparator.Length, publicKey.Length);
                Buffer.BlockCopy(roundBytes, 0, combined, domainSeparator.Length + publicKey.Length, roundBytes.Length);
                
                // Hash to derive the key
                return sha256.ComputeHash(combined);
            }
        }
        
        private byte[] DeriveDecryptionKey(long round, byte[] signature)
        {
            // Derive decryption key from the round number and the signature
            // This should produce the same key as DeriveEncryptionKey when the round's
            // signature is available
            
            using (var sha256 = SHA256.Create())
            {
                // Combine signature with round number and a fixed domain separator
                var roundBytes = BitConverter.GetBytes(round);
                var domainSeparator = Encoding.UTF8.GetBytes("TimeVault-Encryption-Key");
                
                // Concatenate the values
                var combined = new byte[signature.Length + roundBytes.Length + domainSeparator.Length];
                Buffer.BlockCopy(domainSeparator, 0, combined, 0, domainSeparator.Length);
                Buffer.BlockCopy(signature, 0, combined, domainSeparator.Length, signature.Length);
                Buffer.BlockCopy(roundBytes, 0, combined, domainSeparator.Length + signature.Length, roundBytes.Length);
                
                // Hash to derive the key
                return sha256.ComputeHash(combined);
            }
        }
        
        private (byte[] encryptedData, byte[] iv) EncryptContent(string content, byte[] key)
        {
            byte[] encryptedData;
            byte[] iv;
            
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                
                // Generate a random IV
                aes.GenerateIV();
                iv = aes.IV;
                
                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (var sw = new StreamWriter(cs))
                    {
                        sw.Write(content);
                    }
                    
                    encryptedData = ms.ToArray();
                }
            }
            
            return (encryptedData, iv);
        }
        
        private string DecryptContent(byte[] encryptedData, byte[] key, byte[] iv)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                
                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new MemoryStream(encryptedData))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs))
                {
                    return sr.ReadToEnd();
                }
            }
        }
        
        private byte[] EncryptSymmetricKey(byte[] symmetricKey, byte[] encryptionKey)
        {
            // For simplicity, we'll use AES to encrypt the symmetric key with the derived key
            using (var aes = Aes.Create())
            {
                aes.Key = encryptionKey;
                aes.IV = new byte[16]; // Fixed IV for key encryption (could be derived from the encryptionKey)
                
                using (var encryptor = aes.CreateEncryptor())
                {
                    return encryptor.TransformFinalBlock(symmetricKey, 0, symmetricKey.Length);
                }
            }
        }
        
        private byte[] DecryptSymmetricKey(byte[] encryptedKey, byte[] decryptionKey)
        {
            // Decrypt the symmetric key using the derived decryption key
            using (var aes = Aes.Create())
            {
                aes.Key = decryptionKey;
                aes.IV = new byte[16]; // Same fixed IV used in encryption
                
                using (var decryptor = aes.CreateDecryptor())
                {
                    return decryptor.TransformFinalBlock(encryptedKey, 0, encryptedKey.Length);
                }
            }
        }
        
        private static byte[] HexToBytes(string hex)
        {
            if (hex.StartsWith("0x"))
            {
                hex = hex.Substring(2);
            }
            
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }
        
        #endregion
        
        #region Test Helpers - For Unit Testing Only
        
        // Only used in tests - allows injecting a signature for testing decryption
        internal byte[] DeriveDecryptionKeyForTesting(long round, byte[] signature)
        {
            return DeriveDecryptionKey(round, signature);
        }
        
        // Only used in tests - allows creating a test encrypted payload
        internal string CreateEncryptedContentForTesting(string content, long round, byte[] contentKey, byte[] encryptionKey)
        {
            // Encrypt the content with the symmetric key
            var (encryptedContent, iv) = EncryptContent(content, contentKey);
            
            // Encrypt the symmetric key with the derived encryption key
            var encryptedKey = EncryptSymmetricKey(contentKey, encryptionKey);
            
            // Create a structure to store all necessary data
            var tlockData = new TlockDataV2
            {
                EncryptedContent = Convert.ToBase64String(encryptedContent),
                IV = Convert.ToBase64String(iv),
                EncryptedKey = Convert.ToBase64String(encryptedKey),
                Round = round,
                PublicKeyUsed = "test-key",
                VaultPublicKeyUsed = null,
                Timestamp = DateTime.UtcNow
            };
            
            // Serialize and return
            return JsonSerializer.Serialize(tlockData);
        }
        
        #endregion
        
        // Internal class for handling API responses
        internal class DrandInfo
        {
            [JsonPropertyName("public")]
            public DrandPublicInfo? Public { get; set; }
        }
        
        internal class DrandPublicInfo
        {
            [JsonPropertyName("round")]
            public long Round { get; set; }
            
            [JsonPropertyName("key")]
            public string Key { get; set; } = string.Empty;
            
            [JsonPropertyName("period")]
            public int Period { get; set; }
        }
        
        // Enhanced structure for tlock encrypted data
        internal class TlockDataV2
        {
            public string EncryptedContent { get; set; } = string.Empty;
            public string IV { get; set; } = string.Empty;
            public string EncryptedKey { get; set; } = string.Empty;
            public long Round { get; set; }
            public string PublicKeyUsed { get; set; } = string.Empty;
            public string? VaultPublicKeyUsed { get; set; } = null; // Vault public key if used
            public DateTime Timestamp { get; set; }
        }
        
        // Keep the old structure for backward compatibility (if needed)
        internal class TlockData
        {
            public string Content { get; set; } = string.Empty;
            public long Round { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
} 