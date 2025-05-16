using System;
using System.Threading.Tasks;

namespace TimeVault.Core.Services.Interfaces
{
    public interface IDrandService
    {
        /// <summary>
        /// Gets the current round from the drand network
        /// </summary>
        Task<long> GetCurrentRoundAsync();

        /// <summary>
        /// Gets a specific round information from the drand network
        /// </summary>
        Task<DrandRoundResponse> GetRoundAsync(long round);

        /// <summary>
        /// Calculates the round number for a specific unlock time
        /// </summary>
        Task<long> CalculateRoundForTimeAsync(DateTime unlockTime);

        /// <summary>
        /// Gets the public key used for tlock encryption
        /// </summary>
        Task<string> GetPublicKeyAsync();

        /// <summary>
        /// Encrypts content using tlock with a specific round
        /// </summary>
        Task<string> EncryptWithTlockAsync(string content, long round);
        
        /// <summary>
        /// Encrypts content using tlock with a specific round and the vault's public key
        /// </summary>
        Task<string> EncryptWithTlockAndVaultKeyAsync(string content, long round, string vaultPublicKey);

        /// <summary>
        /// Attempts to decrypt tlock encrypted content
        /// </summary>
        Task<string> DecryptWithTlockAsync(string encryptedContent, long round);
        
        /// <summary>
        /// Attempts to decrypt tlock encrypted content using the vault's private key
        /// </summary>
        Task<string> DecryptWithTlockAndVaultKeyAsync(string encryptedContent, long round, string vaultPrivateKey);

        /// <summary>
        /// Checks if a specific round has been reached or exceeded
        /// </summary>
        Task<bool> IsRoundAvailableAsync(long round);
    }

    public class DrandRoundResponse
    {
        public long Round { get; set; }
        public string Randomness { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public long PreviousSignature { get; set; }
    }
} 