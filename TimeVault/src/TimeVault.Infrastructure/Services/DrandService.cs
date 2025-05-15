using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TimeVault.Core.Services.Interfaces;

namespace TimeVault.Infrastructure.Services
{
    public class DrandService : IDrandService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _drandUrl = "https://api.drand.sh";
        
        // Constructor using IHttpClientFactory from DI
        public DrandService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
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
        
        public async Task<long> CalculateRoundForTimeAsync(DateTime unlockTime)
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
        
        public async Task<string> EncryptWithTlockAsync(string content, long round)
        {
            // Simulate tlock encryption (in a real implementation, this would use the drand API)
            var tlockData = new TlockData
            {
                Content = content,
                Round = round,
                Timestamp = DateTime.UtcNow
            };
            
            return JsonSerializer.Serialize(tlockData);
        }
        
        public async Task<string> DecryptWithTlockAsync(string encryptedContent, long round)
        {
            try
            {
                // In a real implementation, we would verify that the round has been reached
                // and use the beacon information to decrypt the content
                
                // Simulate decryption by deserializing our simulated encrypted data
                var tlockData = JsonSerializer.Deserialize<TlockData>(encryptedContent);
                return tlockData?.Content ?? "[Error: Could not decrypt message]";
            }
            catch
            {
                return "[Error: Could not decrypt message]";
            }
        }
        
        public async Task<bool> IsRoundAvailableAsync(long round)
        {
            var client = _httpClientFactory.CreateClient("DrandClient");
            var currentRound = await GetCurrentRoundAsync();
            return currentRound >= round;
        }
        
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
        
        // Simulated structure for tlock encrypted data (for demo purposes)
        internal class TlockData
        {
            public string Content { get; set; } = string.Empty;
            public long Round { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
} 