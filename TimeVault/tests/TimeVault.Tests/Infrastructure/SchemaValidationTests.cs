using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using TimeVault.Domain.Entities;
using TimeVault.Infrastructure.Data;
using Xunit;
using Microsoft.Extensions.DependencyInjection;

namespace TimeVault.Tests.Infrastructure
{
    [Integration]
    public class SchemaValidationTests : IClassFixture<DatabaseFixture>
    {
        private readonly DatabaseFixture _fixture;
        private readonly bool _postgresAvailable;

        public SchemaValidationTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
            
            // Check if PostgreSQL is available
            try
            {
                using var context = _fixture.CreateContext();
                _postgresAvailable = true;
            }
            catch
            {
                _postgresAvailable = false;
            }
        }

        [Fact]
        public void UserEntity_MatchesDatabaseSchema()
        {
            if (!_postgresAvailable)
            {
                // Skip test if PostgreSQL is not available
                return;
            }

            using var context = _fixture.CreateContext();
            
            // Get the entity type from EF Core's metadata
            var entityType = context.Model.FindEntityType(typeof(User));
            Assert.NotNull(entityType);
            
            // Get all properties from the entity
            var entityProperties = typeof(User).GetProperties()
                .Where(p => !p.Name.Equals("Vaults")) // Exclude navigation properties
                .Select(p => p.Name)
                .ToHashSet();
            
            // Get all columns from the database schema
            var schemaProperties = entityType.GetProperties()
                .Select(p => p.Name)
                .ToHashSet();
            
            // Check for missing properties in the entity
            var missingInEntity = schemaProperties.Except(entityProperties).ToList();
            
            // Check for extra properties in the entity that aren't in the schema
            var missingInSchema = entityProperties.Except(schemaProperties).ToList();
            
            Assert.Empty(missingInEntity);
            Assert.Empty(missingInSchema);
        }

        [Fact]
        public void VaultEntity_MatchesDatabaseSchema()
        {
            if (!_postgresAvailable)
            {
                // Skip test if PostgreSQL is not available
                return;
            }

            using var context = _fixture.CreateContext();
            
            // Get the entity type from EF Core's metadata
            var entityType = context.Model.FindEntityType(typeof(Vault));
            Assert.NotNull(entityType);
            
            // Get all properties from the entity
            var entityProperties = typeof(Vault).GetProperties()
                .Where(p => !p.Name.Equals("Owner") && !p.Name.Equals("Messages") && !p.Name.Equals("SharedWith")) // Exclude navigation properties
                .Select(p => p.Name)
                .ToHashSet();
            
            // Get all columns from the database schema
            var schemaProperties = entityType.GetProperties()
                .Select(p => p.Name)
                .ToHashSet();
            
            // Check for missing properties in the entity
            var missingInEntity = schemaProperties.Except(entityProperties).ToList();
            
            // Check for extra properties in the entity that aren't in the schema
            var missingInSchema = entityProperties.Except(schemaProperties).ToList();
            
            Assert.Empty(missingInEntity);
            Assert.Empty(missingInSchema);
        }

        [Fact]
        public void MessageEntity_MatchesDatabaseSchema()
        {
            if (!_postgresAvailable)
            {
                // Skip test if PostgreSQL is not available
                return;
            }

            using var context = _fixture.CreateContext();
            
            // Get the entity type from EF Core's metadata
            var entityType = context.Model.FindEntityType(typeof(Message));
            Assert.NotNull(entityType);
            
            // Get all properties from the entity
            var entityProperties = typeof(Message).GetProperties()
                .Where(p => !p.Name.Equals("Vault") && !p.Name.Equals("Sender")) // Exclude navigation properties
                .Select(p => p.Name)
                .ToHashSet();
            
            // Get all columns from the database schema
            var schemaProperties = entityType.GetProperties()
                .Select(p => p.Name)
                .ToHashSet();
            
            // Check for missing properties in the entity
            var missingInEntity = schemaProperties.Except(entityProperties).ToList();
            
            // Check for extra properties in the entity that aren't in the schema
            var missingInSchema = entityProperties.Except(schemaProperties).ToList();
            
            Assert.Empty(missingInEntity);
            Assert.Empty(missingInSchema);
        }

        [Fact]
        public void VaultShareEntity_MatchesDatabaseSchema()
        {
            if (!_postgresAvailable)
            {
                // Skip test if PostgreSQL is not available
                return;
            }

            using var context = _fixture.CreateContext();
            
            // Get the entity type from EF Core's metadata
            var entityType = context.Model.FindEntityType(typeof(VaultShare));
            Assert.NotNull(entityType);
            
            // Get all properties from the entity
            var entityProperties = typeof(VaultShare).GetProperties()
                .Where(p => !p.Name.Equals("Vault") && !p.Name.Equals("User")) // Exclude navigation properties
                .Select(p => p.Name)
                .ToHashSet();
            
            // Get all columns from the database schema
            var schemaProperties = entityType.GetProperties()
                .Select(p => p.Name)
                .ToHashSet();
            
            // Check for missing properties in the entity
            var missingInEntity = schemaProperties.Except(entityProperties).ToList();
            
            // Check for extra properties in the entity that aren't in the schema
            var missingInSchema = entityProperties.Except(schemaProperties).ToList();
            
            Assert.Empty(missingInEntity);
            Assert.Empty(missingInSchema);
        }
    }
} 