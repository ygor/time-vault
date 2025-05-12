# Seldon Time Vault

A blockchain-based implementation of Hari Seldon's time vault concept from Isaac Asimov's Foundation series. This project allows users to create encrypted messages that can only be revealed after a specific amount of computational time has passed, using Verifiable Delay Functions (VDFs).

## Overview

The Seldon Time Vault is a decentralized application that enables:

1. **Time-Locked Messages**: Create messages that cannot be decrypted until a specific amount of computational work is performed
2. **Trustless Verification**: No trusted third parties hold the keys - the system is fully decentralized
3. **Cryptographic Guarantees**: Mathematical guarantees that messages cannot be revealed early

## How It Works

### Core Concept: Verifiable Delay Functions (VDFs)

VDFs are cryptographic functions that:
- Take a minimum amount of sequential computation time to evaluate
- Cannot be parallelized (even with specialized hardware)
- Produce proofs that can be verified quickly

### Technical Implementation

1. **Message Creation**:
   - User writes a message and selects a difficulty level (time delay)
   - Message is encrypted with a symmetric key
   - Encrypted message is stored on-chain with VDF parameters

2. **Time-Lock Mechanism**:
   - The VDF requires sequential computation that takes a predictable amount of time
   - The difficulty parameter determines how long the computation will take
   - No one can shortcut this process, even with specialized hardware

3. **Message Revelation**:
   - When the time comes, anyone can compute the VDF
   - The output of the VDF becomes the decryption key
   - A proof is generated to verify the computation was done correctly
   - The contract verifies the proof and stores the decryption key

4. **Message Decryption**:
   - Once revealed, anyone can retrieve the decryption key
   - The key is used to decrypt the original message

## Project Structure 