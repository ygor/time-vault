const ethers = require('ethers');
const crypto = require('crypto');

class SeldonVaultClient {
  constructor(contractAddress, provider) {
    this.contract = new ethers.Contract(
      contractAddress,
      [/* ABI here */],
      provider
    );
  }
  
  async createTimeCapsule(message, revealTimestamp) {
    // Generate a random symmetric key
    const symmetricKey = crypto.randomBytes(32);
    
    // Encrypt the message with the symmetric key
    const iv = crypto.randomBytes(16);
    const cipher = crypto.createCipheriv('aes-256-cbc', symmetricKey, iv);
    let encryptedMessage = cipher.update(message, 'utf8', 'hex');
    encryptedMessage += cipher.final('hex');
    
    // Combine IV and encrypted message
    const encryptedData = JSON.stringify({
      iv: iv.toString('hex'),
      data: encryptedMessage
    });
    
    // Create a commitment to the key
    const keyCommitment = ethers.utils.keccak256(symmetricKey);
    
    // Store the encrypted message and commitment on-chain
    const tx = await this.contract.createTimeCapsule(
      ethers.utils.toUtf8Bytes(encryptedData),
      revealTimestamp,
      keyCommitment
    );
    
    const receipt = await tx.wait();
    const capsuleId = receipt.events[0].args.capsuleId;
    
    // Return the capsule ID and the symmetric key (to be stored securely)
    return {
      capsuleId,
      symmetricKey: symmetricKey.toString('hex')
    };
  }
  
  async decryptCapsule(capsuleId) {
    // Get the encrypted message and decryption key from the contract
    const encryptedData = await this.contract.getCapsuleMessage(capsuleId);
    const decryptionKey = await this.contract.getCapsuleDecryptionKey(capsuleId);
    
    if (!decryptionKey || decryptionKey.length === 0) {
      throw new Error("Capsule not yet revealed");
    }
    
    // Parse the encrypted data
    const parsedData = JSON.parse(ethers.utils.toUtf8String(encryptedData));
    
    // Decrypt the message
    const decipher = crypto.createDecipheriv(
      'aes-256-cbc',
      Buffer.from(decryptionKey.slice(2), 'hex'),
      Buffer.from(parsedData.iv, 'hex')
    );
    
    let decrypted = decipher.update(parsedData.data, 'hex', 'utf8');
    decrypted += decipher.final('utf8');
    
    return decrypted;
  }
}

module.exports = SeldonVaultClient; 