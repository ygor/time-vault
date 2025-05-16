using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TimeVault.Api.Features.Messages;
using TimeVault.Api.Features.Messages.Mapping;
using TimeVault.Core.Services.Interfaces;
using TimeVault.Domain.Entities;
using Xunit;

namespace TimeVault.Tests.Features.Messages
{
    public class GetMessageTests
    {
        private readonly Mock<IMessageService> _mockMessageService;
        private readonly IMapper _mapper;
        private readonly IRequestHandler<GetMessage.Query, MessageDto> _handler;
        private readonly Guid _userId;
        private readonly Guid _messageId;

        public GetMessageTests()
        {
            // Set up AutoMapper with feature-specific mapping profile
            var mapperConfig = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile(new MessagesMappingProfile());
            });
            _mapper = mapperConfig.CreateMapper();

            // Set up mock message service
            _mockMessageService = new Mock<IMessageService>();

            // Create test IDs
            _userId = Guid.NewGuid();
            _messageId = Guid.NewGuid();

            // Create handler with mocked dependencies
            _handler = new GetMessage.Handler(_mockMessageService.Object, _mapper);
        }

        [Fact]
        public async Task Handle_ShouldReturnMessage_WhenMessageExists()
        {
            // Arrange
            var query = new GetMessage.Query
            {
                MessageId = _messageId,
                UserId = _userId
            };

            var message = new Message
            {
                Id = _messageId,
                Title = "Test Message",
                Content = "Message Content",
                VaultId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                IsEncrypted = false,
                IsRead = false
            };

            _mockMessageService.Setup(x => x.GetMessageByIdAsync(_messageId, _userId))
                .ReturnsAsync(message);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(_messageId);
            result.Title.Should().Be("Test Message");
            result.Content.Should().Be("Message Content");
        }

        [Fact]
        public async Task Handle_ShouldReturnNull_WhenMessageDoesNotExist()
        {
            // Arrange
            var query = new GetMessage.Query
            {
                MessageId = _messageId,
                UserId = _userId
            };

            // Suppress nullability warnings for Moq setup methods
#pragma warning disable CS8620 // Argument cannot be used for parameter due to differences in nullability of reference types.
            _mockMessageService.Setup(x => x.GetMessageByIdAsync(_messageId, _userId))
                .ReturnsAsync((Message?)null);
#pragma warning restore CS8620

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().BeNull();
        }
    }
} 