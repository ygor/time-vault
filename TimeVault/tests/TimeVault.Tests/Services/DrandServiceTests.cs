using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
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

            _drandService = new DrandService(_mockHttpClientFactory.Object);
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

            // Act
            var result = await _drandService.EncryptWithTlockAsync(content, round);

            // Assert
            result.Should().NotBeNullOrEmpty();
            // In our implementation, the content and round should be stored in the encrypted data
            result.Should().Contain(content);
            result.Should().Contain(round.ToString());
        }

        [Fact]
        public async Task DecryptWithTlockAsync_ShouldReturnDecryptedContent_WhenFormatIsValid()
        {
            // Arrange
            var originalContent = "Test content";
            var round = 12345L;
            
            // First encrypt the content
            var encryptedContent = await _drandService.EncryptWithTlockAsync(originalContent, round);

            // Act
            var decryptedContent = await _drandService.DecryptWithTlockAsync(encryptedContent, round);

            // Assert
            decryptedContent.Should().Be(originalContent);
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
} 