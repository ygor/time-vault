// SPDX-License-Identifier: MIT
pragma solidity ^0.8.17;

contract SeldonTimeVault {
    struct TimeCapsule {
        bytes encryptedMessage;
        uint256 revealTimestamp;
        bool revealed;
        bytes32 keyCommitment;
        bytes decryptionKey;
    }
    
    mapping(uint256 => TimeCapsule) public timeCapsules;
    uint256 public nextCapsuleId;
    
    // Trusted oracle addresses that can reveal keys
    mapping(address => bool) public keyOracles;
    uint256 public requiredOracleSignatures;
    
    event CapsuleCreated(uint256 indexed capsuleId, uint256 revealTimestamp);
    event CapsuleRevealed(uint256 indexed capsuleId);
    
    constructor(address[] memory _initialOracles, uint256 _requiredSignatures) {
        require(_requiredSignatures <= _initialOracles.length, "Too many required signatures");
        requiredOracleSignatures = _requiredSignatures;
        
        for (uint i = 0; i < _initialOracles.length; i++) {
            keyOracles[_initialOracles[i]] = true;
        }
    }
    
    function createTimeCapsule(
        bytes calldata encryptedMessage,
        uint256 revealTimestamp,
        bytes32 keyCommitment
    ) external returns (uint256) {
        require(revealTimestamp > block.timestamp, "Reveal time must be in future");
        
        uint256 capsuleId = nextCapsuleId++;
        
        timeCapsules[capsuleId] = TimeCapsule({
            encryptedMessage: encryptedMessage,
            revealTimestamp: revealTimestamp,
            revealed: false,
            keyCommitment: keyCommitment,
            decryptionKey: new bytes(0)
        });
        
        emit CapsuleCreated(capsuleId, revealTimestamp);
        return capsuleId;
    }
    
    // Oracle signature structure
    struct OracleSignature {
        address oracle;
        bytes signature;
    }
    
    function revealCapsule(
        uint256 capsuleId, 
        bytes calldata decryptionKey, 
        OracleSignature[] calldata signatures
    ) external {
        TimeCapsule storage capsule = timeCapsules[capsuleId];
        
        require(!capsule.revealed, "Capsule already revealed");
        require(block.timestamp >= capsule.revealTimestamp, "Too early to reveal");
        require(keccak256(decryptionKey) == capsule.keyCommitment, "Invalid key");
        require(signatures.length >= requiredOracleSignatures, "Not enough signatures");
        
        // Verify oracle signatures
        bytes32 messageHash = keccak256(abi.encodePacked(capsuleId, decryptionKey));
        address[] memory signers = new address[](signatures.length);
        
        for (uint i = 0; i < signatures.length; i++) {
            address signer = recoverSigner(messageHash, signatures[i].signature);
            require(signer == signatures[i].oracle, "Invalid signature");
            require(keyOracles[signer], "Not an oracle");
            
            // Check for duplicate signers
            for (uint j = 0; j < i; j++) {
                require(signers[j] != signer, "Duplicate signer");
            }
            signers[i] = signer;
        }
        
        capsule.decryptionKey = decryptionKey;
        capsule.revealed = true;
        
        emit CapsuleRevealed(capsuleId);
    }
    
    // Simplified signature recovery function
    function recoverSigner(bytes32 messageHash, bytes memory signature) internal pure returns (address) {
        // Implementation of ECDSA signature recovery
        // This is simplified - a real implementation would use ecrecover
        bytes32 r;
        bytes32 s;
        uint8 v;
        
        assembly {
            r := mload(add(signature, 32))
            s := mload(add(signature, 64))
            v := byte(0, mload(add(signature, 96)))
        }
        
        return ecrecover(messageHash, v, r, s);
    }
    
    // Other utility functions
} 