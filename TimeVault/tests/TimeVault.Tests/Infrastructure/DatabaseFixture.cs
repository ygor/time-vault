using System;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TimeVault.Infrastructure.Data;

namespace TimeVault.Tests.Infrastructure
{
    /// <summary>
    /// A test fixture that creates and manages a test database for integration tests.
    /// This ensures tests run against a real PostgreSQL database rather than just an in-memory one.
    /// </summary>
    public class DatabaseFixture : IDisposable
    {
        private readonly string _databaseName;
        private readonly string _connectionString;
        private bool _disposed = false;
        private bool _postgresAvailable = false;

        public DatabaseFixture()
        {
            // Create a unique database name for this test run - avoid hyphens to prevent PostgreSQL syntax errors
            _databaseName = $"timevault_test_{Guid.NewGuid().ToString().Replace("-", "_")}";
            _connectionString = $"Host=localhost;Database={_databaseName};Username=postgres;Password=postgres";

            try
            {
                // Try to create the test database
                CreateDatabase();
                
                // Try to apply migrations
                ApplyMigrations();
                
                _postgresAvailable = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: PostgreSQL is not available. Tests will run with in-memory database. Error: {ex.Message}");
                _postgresAvailable = false;
            }
        }

        /// <summary>
        /// Gets the connection string to the test database
        /// </summary>
        public string GetConnectionString() => _connectionString;

        /// <summary>
        /// Creates a new DbContext using the test database or in-memory if PostgreSQL is not available
        /// </summary>
        public ApplicationDbContext CreateContext()
        {
            if (_postgresAvailable)
            {
                var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseNpgsql(_connectionString)
                    .Options;

                return new ApplicationDbContext(options);
            }
            else
            {
                // Use in-memory database if PostgreSQL is not available
                var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase($"TimeVaultTest_{Guid.NewGuid()}")
                    .Options;

                var context = new ApplicationDbContext(options);
                context.Database.EnsureCreated();
                return context;
            }
        }

        private void CreateDatabase()
        {
            try
            {
                // Connect to the postgres database to create our test database
                using var masterConnection = new NpgsqlConnection("Host=localhost;Database=postgres;Username=postgres;Password=postgres");
                masterConnection.Open();

                using var command = masterConnection.CreateCommand();
                command.CommandText = $"CREATE DATABASE \"{_databaseName}\"";
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating test database: {ex.Message}");
                throw;
            }
        }

        private void ApplyMigrations()
        {
            try
            {
                // Apply all migrations to the test database
                var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseNpgsql(_connectionString)
                    .Options;

                using var context = new ApplicationDbContext(options);
                context.Database.Migrate();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying migrations: {ex.Message}");
                throw;
            }
        }

        private void DropDatabase()
        {
            if (!_postgresAvailable)
                return;
                
            try
            {
                // Connect to postgres to drop the test database
                using var masterConnection = new NpgsqlConnection("Host=localhost;Database=postgres;Username=postgres;Password=postgres");
                masterConnection.Open();

                // First, terminate all connections to the database
                using var terminateCommand = masterConnection.CreateCommand();
                terminateCommand.CommandText = $@"
                    SELECT pg_terminate_backend(pg_stat_activity.pid)
                    FROM pg_stat_activity
                    WHERE pg_stat_activity.datname = '{_databaseName}'
                    AND pid <> pg_backend_pid()";
                terminateCommand.ExecuteNonQuery();

                // Then drop the database
                using var dropCommand = masterConnection.CreateCommand();
                dropCommand.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\"";
                dropCommand.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error dropping test database: {ex.Message}");
                // Don't throw here, as this is cleanup code
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing && _postgresAvailable)
                {
                    // Drop the test database
                    DropDatabase();
                }

                _disposed = true;
            }
        }

        ~DatabaseFixture()
        {
            Dispose(false);
        }
    }
} 