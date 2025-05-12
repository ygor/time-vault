// SPDX-License-Identifier: MIT
pragma solidity ^0.8.17;

contract SeldonVault {
    struct TimeCapsule {
        bytes encryptedMessage;
        uint256 revealTimestamp;
        bytes32 commitmentHash;
        bool revealed;
        bytes decryptionKey;
    }
    
    mapping(uint256 => TimeCapsule) public timeCapsules;
    uint256 public nextCapsuleId;
    
    event CapsuleCreated(uint256 indexed capsuleId, uint256 revealTimestamp);
    event CapsuleRevealed(uint256 indexed capsuleId, bytes decryptionKey);
    
    function createTimeCapsule(
        bytes calldata encryptedMessage,
        uint256 revealTimestamp,
        bytes32 commitmentHash
    ) external returns (uint256) {
        require(revealTimestamp > block.timestamp, "Reveal time must be in the future");
        
        uint256 capsuleId = nextCapsuleId++;
        
        timeCapsules[capsuleId] = TimeCapsule({
            encryptedMessage: encryptedMessage,
            revealTimestamp: revealTimestamp,
            commitmentHash: commitmentHash,
            revealed: false,
            decryptionKey: new bytes(0)
        });
        
        emit CapsuleCreated(capsuleId, revealTimestamp);
        return capsuleId;
    }
    
    // This function will be called by the key custodians when the time comes
    function revealCapsule(uint256 capsuleId, bytes calldata decryptionKey) external {
        TimeCapsule storage capsule = timeCapsules[capsuleId];
        
        require(!capsule.revealed, "Capsule already revealed");
        require(block.timestamp >= capsule.revealTimestamp, "Too early to reveal");
        require(keccak256(decryptionKey) == capsule.commitmentHash, "Invalid decryption key");
        
        capsule.decryptionKey = decryptionKey;
        capsule.revealed = true;
        
        emit CapsuleRevealed(capsuleId, decryptionKey);
    }
    
    function getCapsuleMessage(uint256 capsuleId) external view returns (bytes memory) {
        return timeCapsules[capsuleId].encryptedMessage;
    }
    
    function getCapsuleDecryptionKey(uint256 capsuleId) external view returns (bytes memory) {
        TimeCapsule storage capsule = timeCapsules[capsuleId];
        require(capsule.revealed, "Capsule not yet revealed");
        return capsule.decryptionKey;
    }
} 