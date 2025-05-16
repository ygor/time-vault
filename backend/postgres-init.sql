-- Initialize the PostgreSQL schema for TimeVault

-- Drop tables if they exist (for clean restart)
DROP TABLE IF EXISTS "vault_shares";
DROP TABLE IF EXISTS "messages";
DROP TABLE IF EXISTS "vaults";
DROP TABLE IF EXISTS "users";

-- Create users table
CREATE TABLE "users" (
    "id" UUID PRIMARY KEY,
    "email" VARCHAR(255) NOT NULL,
    "password_hash" VARCHAR(255) NOT NULL,
    "first_name" VARCHAR(100),
    "last_name" VARCHAR(100),
    "is_admin" BOOLEAN DEFAULT FALSE,
    "created_at" TIMESTAMP NOT NULL,
    "updated_at" TIMESTAMP NOT NULL,
    "last_login" TIMESTAMP
);

-- Create unique index on email
CREATE UNIQUE INDEX "ix_users_email" ON "users" ("email");

-- Create vaults table
CREATE TABLE "vaults" (
    "id" UUID PRIMARY KEY,
    "name" VARCHAR(255) NOT NULL,
    "description" TEXT,
    "owner_id" UUID NOT NULL,
    "public_key" TEXT NOT NULL,
    "encrypted_private_key" TEXT NOT NULL,
    "created_at" TIMESTAMP NOT NULL,
    CONSTRAINT "fk_vaults_users" FOREIGN KEY ("owner_id") REFERENCES "users" ("id") ON DELETE CASCADE
);

-- Create index on owner_id
CREATE INDEX "ix_vaults_owner_id" ON "vaults" ("owner_id");

-- Create messages table
CREATE TABLE "messages" (
    "id" UUID PRIMARY KEY,
    "title" VARCHAR(255) NOT NULL,
    "content" TEXT,
    "encrypted_content" TEXT,
    "iv" TEXT,
    "encrypted_key" TEXT,
    "is_encrypted" BOOLEAN DEFAULT FALSE,
    "is_tlock_encrypted" BOOLEAN DEFAULT FALSE,
    "drand_round" BIGINT,
    "public_key_used" TEXT,
    "created_at" TIMESTAMP NOT NULL,
    "unlock_time" TIMESTAMP,
    "is_read" BOOLEAN DEFAULT FALSE,
    "read_at" TIMESTAMP,
    "vault_id" UUID NOT NULL,
    CONSTRAINT "fk_messages_vaults" FOREIGN KEY ("vault_id") REFERENCES "vaults" ("id") ON DELETE CASCADE
);

-- Create index on vault_id
CREATE INDEX "ix_messages_vault_id" ON "messages" ("vault_id");

-- Create vault_shares table
CREATE TABLE "vault_shares" (
    "id" UUID PRIMARY KEY,
    "user_id" UUID NOT NULL,
    "vault_id" UUID NOT NULL,
    "shared_at" TIMESTAMP NOT NULL,
    "can_edit" BOOLEAN DEFAULT FALSE,
    CONSTRAINT "fk_vault_shares_users" FOREIGN KEY ("user_id") REFERENCES "users" ("id") ON DELETE CASCADE,
    CONSTRAINT "fk_vault_shares_vaults" FOREIGN KEY ("vault_id") REFERENCES "vaults" ("id") ON DELETE CASCADE
);

-- Create unique index on vault_id and user_id to prevent duplicate shares
CREATE UNIQUE INDEX "ix_vault_shares_vault_id_user_id" ON "vault_shares" ("vault_id", "user_id");

-- Create indices on user_id and vault_id
CREATE INDEX "ix_vault_shares_user_id" ON "vault_shares" ("user_id");
CREATE INDEX "ix_vault_shares_vault_id" ON "vault_shares" ("vault_id"); 