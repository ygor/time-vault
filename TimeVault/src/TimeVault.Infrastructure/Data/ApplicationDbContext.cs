using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
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
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Configure the Vault entity
            modelBuilder.Entity<Vault>()
                .HasOne(v => v.Owner)
                .WithMany(u => u.Vaults)
                .HasForeignKey(v => v.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
                
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
                .IsRequired();
            
            modelBuilder.Entity<Message>()
                .Property(m => m.EncryptedContent)
                .IsRequired();

            modelBuilder.Entity<Message>()
                .Property(m => m.DrandRound)
                .IsRequired(false);

            modelBuilder.Entity<Message>()
                .Property(m => m.TlockPublicKey)
                .IsRequired(false);

            modelBuilder.Entity<Message>()
                .Property(m => m.IsTlockEncrypted)
                .IsRequired()
                .HasDefaultValue(false);

            // Configure the VaultShare entity
            modelBuilder.Entity<VaultShare>()
                .HasOne(vs => vs.User)
                .WithMany()
                .HasForeignKey(vs => vs.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            
            modelBuilder.Entity<VaultShare>()
                .HasOne(vs => vs.Vault)
                .WithMany()
                .HasForeignKey(vs => vs.VaultId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
} 