const fs = require('fs');
const path = require('path');

// This script assumes you've already run the local deployment
// and have the contract addresses

// Replace with your actual deployed contract addresses from the local network
const SELDON_VAULT_ADDRESS = "0x5FbDB2315678afecb367f032d93F642f64180aa3"; // Example address, replace with yours

const contractsContent = `
export const SeldonVaultVDFAddress = "${SELDON_VAULT_ADDRESS}";

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
`;

// Ensure the frontend/src directory exists
const frontendDir = path.join(__dirname, '..', 'frontend', 'src');
if (!fs.existsSync(frontendDir)) {
  fs.mkdirSync(frontendDir, { recursive: true });
}

// Write the contracts.js file
fs.writeFileSync(
  path.join(frontendDir, 'contracts.js'),
  contractsContent
);

console.log('Frontend contracts.js file updated for local development!');
console.log('Make sure to update the contract address with your actual deployed address.'); 