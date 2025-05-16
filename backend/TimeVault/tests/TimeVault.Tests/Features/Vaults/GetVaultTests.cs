using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentAssertions;
using MediatR;
using Moq;
using TimeVault.Api.Features.Vaults;
using TimeVault.Api.Features.Vaults.Mapping;
using TimeVault.Core.Services.Interfaces;
using TimeVault.Domain.Entities;
using Xunit;

namespace TimeVault.Tests.Features.Vaults
{
    public class GetVaultTests
    {
        private readonly Mock<IVaultService> _mockVaultService;
        private readonly IMapper _mapper;
        private readonly IRequestHandler<GetVault.Query, VaultDto> _handler;
        private readonly Guid _userId;
        private readonly Guid _vaultId;

        public GetVaultTests()
        {
            // Set up AutoMapper with feature-specific mapping profile
            var mapperConfig = new MapperConfiguration(cfg =>
            {
                cfg.AddProfile(new VaultsMappingProfile());
            });
            _mapper = mapperConfig.CreateMapper();

            // Set up mock services
            _mockVaultService = new Mock<IVaultService>();

            // Create test IDs
            _userId = Guid.NewGuid();
            _vaultId = Guid.NewGuid();

            // Create handler with mocked dependencies
            _handler = new GetVault.Handler(_mockVaultService.Object, _mapper);
        }

        [Fact]
        public async Task Handle_ShouldReturnVault_WhenVaultExists()
        {
            // Arrange
            var query = new GetVault.Query
            {
                VaultId = _vaultId,
                UserId = _userId
            };

            var vault = new Vault
            {
                Id = _vaultId,
                Name = "Test Vault",
                Description = "A vault for testing",
                OwnerId = _userId,
                CreatedAt = DateTime.UtcNow
            };

            _mockVaultService.Setup(x => x.GetVaultByIdAsync(_vaultId, _userId))
                .ReturnsAsync(vault);
            
            _mockVaultService.Setup(x => x.CanEditVaultAsync(_vaultId, _userId))
                .ReturnsAsync(true);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(_vaultId);
            result.Name.Should().Be("Test Vault");
            result.Description.Should().Be("A vault for testing");
            result.OwnerId.Should().Be(_userId);
            result.IsOwner.Should().BeTrue();
            result.CanEdit.Should().BeTrue();
        }

        [Fact]
        public async Task Handle_ShouldReturnNull_WhenVaultDoesNotExist()
        {
            // Arrange
            var query = new GetVault.Query
            {
                VaultId = _vaultId,
                UserId = _userId
            };

            // Suppress nullability warnings for Moq setup methods
#pragma warning disable CS8620 // Argument cannot be used for parameter due to differences in nullability of reference types.
            _mockVaultService.Setup(x => x.GetVaultByIdAsync(_vaultId, _userId))
                .ReturnsAsync((Vault?)null);
#pragma warning restore CS8620

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().BeNull();
        }
    }
} 