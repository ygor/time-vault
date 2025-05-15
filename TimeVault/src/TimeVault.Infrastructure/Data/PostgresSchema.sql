-- PostgreSQL Schema for TimeVault
-- This script creates the database schema for the TimeVault application

-- Users table
CREATE TABLE IF NOT EXISTS "Users" (
    "Id" UUID PRIMARY KEY,
    "Email" VARCHAR(255) UNIQUE NOT NULL,
    "PasswordHash" VARCHAR(255) NOT NULL,
    "FirstName" VARCHAR(100),
    "LastName" VARCHAR(100),
    "IsAdmin" BOOLEAN NOT NULL DEFAULT FALSE,
    "CreatedAt" TIMESTAMP NOT NULL,
    "UpdatedAt" TIMESTAMP NOT NULL,
    "LastLogin" TIMESTAMP NULL
);

-- Vaults table
CREATE TABLE IF NOT EXISTS "Vaults" (
    "Id" UUID PRIMARY KEY,
    "Name" VARCHAR(100) NOT NULL,
    "Description" TEXT,
    "PublicKey" TEXT NOT NULL,
    "EncryptedPrivateKey" TEXT NOT NULL,
    "CreatedAt" TIMESTAMP NOT NULL,
    "UpdatedAt" TIMESTAMP NOT NULL,
    "OwnerId" UUID NOT NULL,
    CONSTRAINT "FK_Vaults_Users_OwnerId" FOREIGN KEY ("OwnerId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

-- Messages table
CREATE TABLE IF NOT EXISTS "Messages" (
    "Id" UUID PRIMARY KEY,
    "Title" VARCHAR(200) NOT NULL,
    "Content" TEXT NULL,
    "EncryptedContent" TEXT NULL,
    "IV" TEXT NULL,
    "EncryptedKey" TEXT NULL,
    "IsEncrypted" BOOLEAN NOT NULL DEFAULT FALSE,
    "IsTlockEncrypted" BOOLEAN NOT NULL DEFAULT FALSE,
    "DrandRound" BIGINT NULL,
    "PublicKeyUsed" TEXT NULL,
    "CreatedAt" TIMESTAMP NOT NULL,
    "UpdatedAt" TIMESTAMP NOT NULL,
    "UnlockTime" TIMESTAMP NULL,
    "IsRead" BOOLEAN NOT NULL DEFAULT FALSE,
    "ReadAt" TIMESTAMP NULL,
    "VaultId" UUID NOT NULL,
    "SenderId" UUID NOT NULL,
    CONSTRAINT "FK_Messages_Vaults_VaultId" FOREIGN KEY ("VaultId") REFERENCES "Vaults" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Messages_Users_SenderId" FOREIGN KEY ("SenderId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

-- VaultShares table
CREATE TABLE IF NOT EXISTS "VaultShares" (
    "Id" UUID PRIMARY KEY,
    "VaultId" UUID NOT NULL,
    "UserId" UUID NOT NULL,
    "CanEdit" BOOLEAN NOT NULL DEFAULT FALSE,
    "CreatedAt" TIMESTAMP NOT NULL,
    "UpdatedAt" TIMESTAMP NOT NULL,
    CONSTRAINT "FK_VaultShares_Vaults_VaultId" FOREIGN KEY ("VaultId") REFERENCES "Vaults" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_VaultShares_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE,
    CONSTRAINT "UK_VaultShares_VaultId_UserId" UNIQUE ("VaultId", "UserId")
);

-- Create indexes for foreign keys to improve query performance
CREATE INDEX IF NOT EXISTS "IX_Vaults_OwnerId" ON "Vaults" ("OwnerId");
CREATE INDEX IF NOT EXISTS "IX_Messages_VaultId" ON "Messages" ("VaultId");
CREATE INDEX IF NOT EXISTS "IX_Messages_SenderId" ON "Messages" ("SenderId");
CREATE INDEX IF NOT EXISTS "IX_VaultShares_VaultId" ON "VaultShares" ("VaultId");
CREATE INDEX IF NOT EXISTS "IX_VaultShares_UserId" ON "VaultShares" ("UserId");

-- Create index for email lookup
CREATE INDEX IF NOT EXISTS "IX_Users_Email" ON "Users" ("Email");

-- Create index for unlockable messages
CREATE INDEX IF NOT EXISTS "IX_Messages_UnlockTime" ON "Messages" ("UnlockTime") WHERE "IsEncrypted" = TRUE; 