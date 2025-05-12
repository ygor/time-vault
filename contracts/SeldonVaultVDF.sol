// SPDX-License-Identifier: MIT
pragma solidity ^0.8.17;

import "./VDFVerifier.sol"; // This would be a contract that verifies VDF proofs

contract SeldonVaultVDF {
    VDFVerifier public vdfVerifier;
    
    struct TimeCapsule {
        bytes encryptedMessage;
        uint256 difficulty;        // Represents time delay
        bytes seed;                // Random seed for VDF
        bool revealed;
        bytes decryptionKey;
        string messageTitle;       // Title/description of the message
        address creator;           // Who created this time capsule
        uint256 creationTimestamp; // When it was created
    }
    
    mapping(uint256 => TimeCapsule) public timeCapsules;
    uint256 public nextCapsuleId;
    
    // Events
    event CapsuleCreated(uint256 indexed capsuleId, string messageTitle, uint256 difficulty, address creator);
    event CapsuleRevealed(uint256 indexed capsuleId, bytes decryptionKey);
    event VDFChallengeStarted(uint256 indexed capsuleId, bytes seed, uint256 difficulty);
    
    constructor(address _vdfVerifier) {
        vdfVerifier = VDFVerifier(_vdfVerifier);
    }
    
    /**
     * @dev Creates a new time capsule with an encrypted message
     * @param encryptedMessage The message encrypted with a symmetric key
     * @param difficulty The VDF difficulty parameter (higher = longer delay)
     * @param seed Random seed for the VDF
     * @param messageTitle A title or description for the message
     * @return The ID of the created capsule
     */
    function createTimeCapsule(
        bytes calldata encryptedMessage,
        uint256 difficulty,
        bytes calldata seed,
        string calldata messageTitle
    ) external returns (uint256) {
        require(difficulty > 0, "Difficulty must be positive");
        require(seed.length > 0, "Seed cannot be empty");
        
        uint256 capsuleId = nextCapsuleId++;
        
        timeCapsules[capsuleId] = TimeCapsule({
            encryptedMessage: encryptedMessage,
            difficulty: difficulty,
            seed: seed,
            revealed: false,
            decryptionKey: new bytes(0),
            messageTitle: messageTitle,
            creator: msg.sender,
            creationTimestamp: block.timestamp
        });
        
        emit CapsuleCreated(capsuleId, messageTitle, difficulty, msg.sender);
        emit VDFChallengeStarted(capsuleId, seed, difficulty);
        
        return capsuleId;
    }
    
    /**
     * @dev Submits a VDF proof to reveal a time capsule
     * @param capsuleId The ID of the capsule to reveal
     * @param proof The VDF proof
     * @param output The output of the VDF computation (becomes the decryption key)
     */
    function submitVDFProof(uint256 capsuleId, bytes calldata proof, bytes calldata output) external {
        TimeCapsule storage capsule = timeCapsules[capsuleId];
        require(!capsule.revealed, "Capsule already revealed");
        
        // Verify the VDF proof
        bool isValid = vdfVerifier.verify(capsule.seed, capsule.difficulty, proof, output);
        require(isValid, "Invalid VDF proof");
        
        // The output of the VDF is the decryption key
        capsule.decryptionKey = output;
        capsule.revealed = true;
        
        emit CapsuleRevealed(capsuleId, output);
    }
    
    /**
     * @dev Gets the encrypted message from a time capsule
     * @param capsuleId The ID of the capsule
     * @return The encrypted message
     */
    function getCapsuleMessage(uint256 capsuleId) external view returns (bytes memory) {
        return timeCapsules[capsuleId].encryptedMessage;
    }
    
    /**
     * @dev Gets the decryption key for a revealed time capsule
     * @param capsuleId The ID of the capsule
     * @return The decryption key
     */
    function getCapsuleDecryptionKey(uint256 capsuleId) external view returns (bytes memory) {
        TimeCapsule storage capsule = timeCapsules[capsuleId];
        require(capsule.revealed, "Capsule not yet revealed");
        return capsule.decryptionKey;
    }
    
    /**
     * @dev Gets information about a time capsule
     * @param capsuleId The ID of the capsule
     * @return title The title of the message
     * @return creator The address that created the capsule
     * @return creationTime When the capsule was created
     * @return difficulty The VDF difficulty
     * @return revealed Whether the capsule has been revealed
     */
    function getCapsuleInfo(uint256 capsuleId) external view returns (
        string memory title,
        address creator,
        uint256 creationTime,
        uint256 difficulty,
        bool revealed
    ) {
        TimeCapsule storage capsule = timeCapsules[capsuleId];
        return (
            capsule.messageTitle,
            capsule.creator,
            capsule.creationTimestamp,
            capsule.difficulty,
            capsule.revealed
        );
    }
    
    /**
     * @dev Gets the VDF challenge parameters for a capsule
     * @param capsuleId The ID of the capsule
     * @return seed The VDF seed
     * @return difficulty The VDF difficulty
     */
    function getVDFChallenge(uint256 capsuleId) external view returns (bytes memory seed, uint256 difficulty) {
        TimeCapsule storage capsule = timeCapsules[capsuleId];
        return (capsule.seed, capsule.difficulty);
    }
} 