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
            // Set the naming convention for PostgreSQL
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Vault> Vaults { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<VaultShare> VaultShares { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Use snake_case naming convention for PostgreSQL
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                // Set the table name to snake_case
                entity.SetTableName(ToSnakeCase(entity.GetTableName()));

                // Set column names to snake_case
                foreach (var property in entity.GetProperties())
                {
                    property.SetColumnName(ToSnakeCase(property.GetColumnName()));
                }

                // Set primary key name to snake_case
                foreach (var key in entity.GetKeys())
                {
                    key.SetName(ToSnakeCase(key.GetName()));
                }

                // Set foreign key constraint names to snake_case
                foreach (var key in entity.GetForeignKeys())
                {
                    key.SetConstraintName(ToSnakeCase(key.GetConstraintName()));
                }

                // Set index names to snake_case
                foreach (var index in entity.GetIndexes())
                {
                    index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName()));
                }
            }

            // Configure the User entity
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();
                
            modelBuilder.Entity<User>()
                .Property(u => u.FirstName)
                .IsRequired(false);
                
            modelBuilder.Entity<User>()
                .Property(u => u.LastName)
                .IsRequired(false);

            // Note: Username field is no longer part of the model but may exist in the database 
            // This will be handled through migrations

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

        // Helper method to convert PascalCase to snake_case
        private static string ToSnakeCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var result = new System.Text.StringBuilder(input.Length + 10);
            result.Append(char.ToLower(input[0]));

            for (int i = 1; i < input.Length; i++)
            {
                var c = input[i];
                if (char.IsUpper(c))
                {
                    result.Append('_');
                    result.Append(char.ToLower(c));
                }
                else
                {
                    result.Append(c);
                }
            }

            return result.ToString();
        }
    }
} 