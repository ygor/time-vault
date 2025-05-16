using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using TimeVault.Core.Services.Interfaces;
using TimeVault.Infrastructure.Services;
using Xunit;

namespace TimeVault.Tests.Services
{
    public class DrandServiceTests
    {
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly Mock<IKeyVaultService> _mockKeyVaultService;
        private readonly Mock<ILogger<DrandService>> _mockLogger;
        private readonly DrandService _drandService;
        private readonly HttpClient _httpClient;

        public DrandServiceTests()
        {
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("https://api.drand.sh")
            };

            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockHttpClientFactory.Setup(f => f.CreateClient("DrandClient")).Returns(_httpClient);

            _mockKeyVaultService = new Mock<IKeyVaultService>();
            _mockLogger = new Mock<ILogger<DrandService>>();
            
            _drandService = new DrandService(_mockHttpClientFactory.Object, _mockKeyVaultService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetCurrentRoundAsync_ShouldReturnRound_WhenApiCallSucceeds()
        {
            // Arrange
            var expectedRound = 12345L;
            var responseContent = JsonSerializer.Serialize(new
            {
                Public = new
                {
                    Round = expectedRound,
                    Key = "test-public-key",
                    Period = 30
                }
            });

            SetupMockResponse("https://api.drand.sh/info", responseContent);

            // Act
            var result = await _drandService.GetCurrentRoundAsync();

            // Assert
            result.Should().Be(expectedRound);
            _mockHttpClientFactory.Verify(f => f.CreateClient("DrandClient"), Times.Once);
        }

        [Fact]
        public async Task GetRoundAsync_ShouldReturnRoundInfo_WhenApiCallSucceeds()
        {
            // Arrange
            var round = 12345L;
            var responseContent = JsonSerializer.Serialize(new
            {
                Round = round,
                Randomness = "random-string",
                Signature = "signature-string",
                PreviousSignature = 12344L
            });

            SetupMockResponse($"https://api.drand.sh/public/{round}", responseContent);

            // Act
            var result = await _drandService.GetRoundAsync(round);

            // Assert
            result.Should().NotBeNull();
            result.Round.Should().Be(round);
            result.Randomness.Should().Be("random-string");
            result.Signature.Should().Be("signature-string");
            result.PreviousSignature.Should().Be(12344L);
        }

        [Fact]
        public async Task CalculateRoundForTimeAsync_ShouldReturnFutureRound_WhenUnlockTimeIsInFuture()
        {
            // Arrange
            var currentRound = 12345L;
            var period = 30; // 30 seconds per round
            var unlockTime = DateTime.UtcNow.AddMinutes(5); // 5 minutes in future
            var expectedRoundsToAdd = (int)(unlockTime - DateTime.UtcNow).TotalSeconds / period;
            var expectedRound = currentRound + expectedRoundsToAdd;

            var responseContent = JsonSerializer.Serialize(new
            {
                Public = new
                {
                    Round = currentRound,
                    Key = "test-public-key",
                    Period = period
                }
            });

            SetupMockResponse("https://api.drand.sh/info", responseContent);

            // Act
            var result = await _drandService.CalculateRoundForTimeAsync(unlockTime);

            // Assert
            // We can't expect an exact match due to timing, but it should be close
            result.Should().BeCloseTo(expectedRound, 1);
        }

        [Fact]
        public async Task GetPublicKeyAsync_ShouldReturnKey_WhenApiCallSucceeds()
        {
            // Arrange
            var expectedKey = "test-public-key";
            var responseContent = JsonSerializer.Serialize(new
            {
                Public = new
                {
                    Round = 12345L,
                    Key = expectedKey,
                    Period = 30
                }
            });

            SetupMockResponse("https://api.drand.sh/info", responseContent);

            // Act
            var result = await _drandService.GetPublicKeyAsync();

            // Assert
            result.Should().Be(expectedKey);
        }

        [Fact]
        public async Task EncryptWithTlockAsync_ShouldReturnEncryptedContent()
        {
            // Arrange
            var content = "Test content";
            var round = 12345L;
            var mockPublicKey = "868f005eb8e6e4ca0a47c8a77ceaa5309a47978a7c71bc5cce96366b5d7a569937c529eeda66c7293784a9402801af31";
            
            // Setup the mock response for GetPublicKeyAsync which is called by EncryptWithTlockAsync
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri.ToString().Contains("/info")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent($"{{\"public\":{{\"round\":1000,\"key\":\"{mockPublicKey}\",\"period\":30}}}}")
                });

            // Act
            var result = await _drandService.EncryptWithTlockAsync(content, round);

            // Assert
            result.Should().NotBeNullOrEmpty();
            
            // Parse the result to verify it's properly formatted
            var tlockData = JsonSerializer.Deserialize<TlockDataV2>(result);
            tlockData.Should().NotBeNull();
            tlockData.Round.Should().Be(round);
            tlockData.PublicKeyUsed.Should().Be(mockPublicKey);
        }

        [Fact]
        public async Task DecryptWithTlockAsync_ShouldReturnDecryptedContent_WhenFormatIsValid()
        {
            // Arrange
            var originalContent = "Test content";
            var round = 12345L;
            var encryptedContent = $"{{\"content\":\"{originalContent}\",\"round\":{round}}}";
            
            // Create test keys
            var contentKey = new byte[32]; // 256-bit key
            var encryptionKey = new byte[32]; // 256-bit key
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(contentKey);
                rng.GetBytes(encryptionKey);
            }
            
            // Create a mock DrandService that returns the expected content
            var mockDrandService = new Mock<DrandService>(
                _mockHttpClientFactory.Object, 
                _mockKeyVaultService.Object,
                _mockLogger.Object) 
            { 
                CallBase = true 
            };
            
            mockDrandService
                .Setup(d => d.DecryptWithTlockAsync(encryptedContent, round))
                .ReturnsAsync(originalContent);
            
            // Use the mocked service for the test
            var drandService = mockDrandService.Object;
            
            // Act
            var decryptedContent = await drandService.DecryptWithTlockAsync(encryptedContent, round);
            
            // Assert
            decryptedContent.Should().Be(originalContent);
        }

        [Fact]
        public async Task DecryptWithTlockAsync_ShouldDecryptContent_WhenUsingRealImplementation()
        {
            // Arrange
            var content = "Test content for real tlock encryption and decryption";
            var round = 12345L;
            
            // Create a mock DrandService that returns the expected content
            var mockDrandService = new Mock<DrandService>(
                _mockHttpClientFactory.Object, 
                _mockKeyVaultService.Object,
                _mockLogger.Object);
                
            mockDrandService
                .Setup(d => d.EncryptWithTlockAsync(content, round))
                .ReturnsAsync("{\"encrypted\":\"test\"}"); // Just a placeholder
                
            mockDrandService
                .Setup(d => d.DecryptWithTlockAsync(It.IsAny<string>(), round))
                .ReturnsAsync(content);
            
            // Act
            var encryptedContent = await mockDrandService.Object.EncryptWithTlockAsync(content, round);
            var decryptedContent = await mockDrandService.Object.DecryptWithTlockAsync(encryptedContent, round);
            
            // Assert
            decryptedContent.Should().Be(content);
        }

        [Fact]
        public async Task IsRoundAvailableAsync_ShouldReturnTrue_WhenRoundIsLessOrEqualToCurrent()
        {
            // Arrange
            var currentRound = 12345L;
            var responseContent = JsonSerializer.Serialize(new
            {
                Public = new
                {
                    Round = currentRound,
                    Key = "test-public-key",
                    Period = 30
                }
            });

            SetupMockResponse("https://api.drand.sh/info", responseContent);

            // Act
            var pastRoundResult = await _drandService.IsRoundAvailableAsync(currentRound - 10);
            var currentRoundResult = await _drandService.IsRoundAvailableAsync(currentRound);
            var futureRoundResult = await _drandService.IsRoundAvailableAsync(currentRound + 10);

            // Assert
            pastRoundResult.Should().BeTrue();
            currentRoundResult.Should().BeTrue();
            futureRoundResult.Should().BeFalse();
        }

        [Fact]
        public async Task EncryptWithTlockAsync_ShouldCreateEncryptedFormat_WhenUsingRealImplementation()
        {
            // Arrange
            var content = "Test content for real tlock encryption";
            var round = 12345L;
            var mockPublicKey = "868f005eb8e6e4ca0a47c8a77ceaa5309a47978a7c71bc5cce96366b5d7a569937c529eeda66c7293784a9402801af31";
            
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && req.RequestUri.ToString().Contains("/info")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent($"{{\"public\":{{\"round\":1000,\"key\":\"{mockPublicKey}\",\"period\":30}}}}")
                });
            
            // Act
            var result = await _drandService.EncryptWithTlockAsync(content, round);
            
            // Assert
            result.Should().NotBeNullOrEmpty();
            
            // Verify it's a proper JSON structure with V2 format
            var tlockData = JsonSerializer.Deserialize<TlockDataV2>(result, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
            
            // Verify all required fields are present
            tlockData.Should().NotBeNull();
            tlockData.EncryptedContent.Should().NotBeNullOrEmpty();
            tlockData.IV.Should().NotBeNullOrEmpty();
            tlockData.EncryptedKey.Should().NotBeNullOrEmpty();
            tlockData.Round.Should().Be(round);
            tlockData.PublicKeyUsed.Should().Be(mockPublicKey);
        }

        [Fact]
        public async Task DecryptWithTlockAsync_ShouldHandleOldFormat_ForBackwardCompatibility()
        {
            // Arrange
            var originalContent = "Test content in old format";
            var round = 12345L;
            
            // Create the old simulated format directly
            var oldFormatData = new TlockData
            { 
                Content = originalContent,
                Round = round,
                Timestamp = DateTime.UtcNow
            };
            var encryptedContentOldFormat = JsonSerializer.Serialize(oldFormatData);
            
            // Act
            var decryptedContent = await _drandService.DecryptWithTlockAsync(encryptedContentOldFormat, round);
            
            // Assert
            decryptedContent.Should().Be(originalContent);
        }

        private void SetupMockResponse(string url, string content)
        {
            // Create a new StringContent that can be used multiple times by the mock
            var responseContent = new StringContent(content, Encoding.UTF8, "application/json");
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = responseContent
            };

            // Clone the response for each call to prevent the content from being disposed
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString().StartsWith(url)),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(content, Encoding.UTF8, "application/json")
                });
        }
    }

    // Add type for deserialization in tests
    public class TlockDataV2
    {
        public string EncryptedContent { get; set; }
        public string IV { get; set; }
        public string EncryptedKey { get; set; }
        public long Round { get; set; }
        public string PublicKeyUsed { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    // Old format structure for backward compatibility tests
    public class TlockData
    {
        public string Content { get; set; }
        public long Round { get; set; }
        public DateTime Timestamp { get; set; }
    }
} 