using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TimeVault.Domain.Entities;

namespace TimeVault.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Vault> Vaults { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<VaultShare> VaultShares { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure the User entity
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Configure the Vault entity
            modelBuilder.Entity<Vault>()
                .HasOne(v => v.Owner)
                .WithMany(u => u.Vaults)
                .HasForeignKey(v => v.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
                
            modelBuilder.Entity<Vault>()
                .Property(v => v.PublicKey)
                .IsRequired();
                
            modelBuilder.Entity<Vault>()
                .Property(v => v.EncryptedPrivateKey)
                .IsRequired();

            // Configure the Message entity
            modelBuilder.Entity<Message>()
                .HasOne(m => m.Vault)
                .WithMany(v => v.Messages)
                .HasForeignKey(m => m.VaultId)
                .OnDelete(DeleteBehavior.Cascade);
            
            modelBuilder.Entity<Message>()
                .Property(m => m.Content)
                .IsRequired(false);
            
            modelBuilder.Entity<Message>()
                .Property(m => m.EncryptedContent)
                .IsRequired(false);

            modelBuilder.Entity<Message>()
                .Property(m => m.IV)
                .IsRequired(false);

            modelBuilder.Entity<Message>()
                .Property(m => m.EncryptedKey)
                .IsRequired(false);

            modelBuilder.Entity<Message>()
                .Property(m => m.DrandRound)
                .IsRequired(false);

            modelBuilder.Entity<Message>()
                .Property(m => m.PublicKeyUsed)
                .IsRequired(false);

            modelBuilder.Entity<Message>()
                .Property(m => m.UnlockTime)
                .IsRequired(false);

            // Configure the VaultShare entity
            modelBuilder.Entity<VaultShare>()
                .HasOne(vs => vs.User)
                .WithMany()
                .HasForeignKey(vs => vs.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            modelBuilder.Entity<VaultShare>()
                .HasOne(vs => vs.Vault)
                .WithMany(v => v.SharedWith)
                .HasForeignKey(vs => vs.VaultId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<VaultShare>()
                .HasIndex(vs => new { vs.VaultId, vs.UserId })
                .IsUnique();
        }
        
        /// <summary>
        /// Initialize the PostgreSQL database using the SQL script
        /// </summary>
        public static void InitializePostgresDatabase(string connectionString, string scriptPath)
        {
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                
                // Read the SQL script
                string sql = File.ReadAllText(scriptPath);
                
                // Execute the script
                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }
    }
} 