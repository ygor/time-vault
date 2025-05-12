export const SeldonVaultVDFAddress = "0x..."; // You'll fill this in after deployment

export const SeldonVaultVDFABI = [
  // Events
  "event CapsuleCreated(uint256 indexed capsuleId, string messageTitle, uint256 difficulty, address creator)",
  "event CapsuleRevealed(uint256 indexed capsuleId, bytes decryptionKey)",
  "event VDFChallengeStarted(uint256 indexed capsuleId, bytes seed, uint256 difficulty)",
  
  // View functions
  "function timeCapsules(uint256) view returns (bytes encryptedMessage, uint256 difficulty, bytes seed, bool revealed, bytes decryptionKey, string messageTitle, address creator, uint256 creationTimestamp)",
  "function nextCapsuleId() view returns (uint256)",
  "function getCapsuleMessage(uint256 capsuleId) view returns (bytes)",
  "function getCapsuleDecryptionKey(uint256 capsuleId) view returns (bytes)",
  "function getCapsuleInfo(uint256 capsuleId) view returns (string title, address creator, uint256 creationTime, uint256 difficulty, bool revealed)",
  "function getVDFChallenge(uint256 capsuleId) view returns (bytes seed, uint256 difficulty)",
  
  // State-changing functions
  "function createTimeCapsule(bytes encryptedMessage, uint256 difficulty, bytes seed, string messageTitle) returns (uint256)",
  "function submitVDFProof(uint256 capsuleId, bytes proof, bytes output)"
]; 