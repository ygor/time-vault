const ethers = require('ethers');
const crypto = require('crypto');

class SeldonVaultVDFClient {
  constructor(contractAddress, provider) {
    // ABI for the SeldonVaultVDF contract
    const abi = [
      // ... contract ABI ...
    ];
    
    this.contract = new ethers.Contract(contractAddress, abi, provider);
    this.signer = provider.getSigner();
  }
  
  /**
   * Creates a new time capsule
   * @param {string} message - The message to encrypt
   * @param {number} difficulty - The VDF difficulty (higher = longer delay)
   * @param {string} title - A title for the message
   * @returns {Promise<object>} - The capsule info and encryption key
   */
  async createTimeCapsule(message, difficulty, title) {
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
    
    // Generate a random seed for the VDF
    const seed = crypto.randomBytes(32);
    
    // Store the encrypted message on-chain
    const tx = await this.contract.connect(this.signer).createTimeCapsule(
      ethers.utils.toUtf8Bytes(encryptedData),
      difficulty,
      seed,
      title
    );
    
    const receipt = await tx.wait();
    const event = receipt.events.find(e => e.event === 'CapsuleCreated');
    const capsuleId = event.args.capsuleId;
    
    // Return the capsule ID, VDF parameters, and the symmetric key
    return {
      capsuleId: capsuleId.toString(),
      difficulty,
      seed: ethers.utils.hexlify(seed),
      symmetricKey: symmetricKey.toString('hex')
    };
  }
  
  /**
   * Solves the VDF challenge for a capsule
   * @param {string} capsuleId - The ID of the capsule
   * @returns {Promise<object>} - The VDF solution
   */
  async solveVDFChallenge(capsuleId) {
    // Get the VDF challenge parameters
    const challenge = await this.contract.getVDFChallenge(capsuleId);
    const seed = challenge.seed;
    const difficulty = challenge.difficulty.toNumber();
    
    console.log(`Starting VDF computation with difficulty ${difficulty}...`);
    
    // Perform the VDF computation
    // This is a simplified implementation - real VDFs would be much more complex
    const { output, proof } = await this.computeVDF(seed, difficulty);
    
    console.log('VDF computation complete, submitting proof...');
    
    // Submit the proof to the contract
    const tx = await this.contract.connect(this.signer).submitVDFProof(
      capsuleId,
      proof,
      output
    );
    
    await tx.wait();
    
    return {
      output: ethers.utils.hexlify(output),
      proof: ethers.utils.hexlify(proof)
    };
  }
  
  /**
   * Computes the VDF
   * @param {Uint8Array} seed - The seed for the VDF
   * @param {number} difficulty - The difficulty parameter
   * @returns {Promise<object>} - The VDF output and proof
   */
  async computeVDF(seed, difficulty) {
    // Convert seed to a number
    const x = BigInt('0x' + Buffer.from(seed).toString('hex')) % BigInt(PRIME_MODULUS);
    
    // Perform repeated squaring
    let y = x;
    for (let i = 0; i < difficulty; i++) {
      // This is the time-consuming part that cannot be parallelized
      y = (y * y) % BigInt(PRIME_MODULUS);
      
      // Progress update every 1000 iterations
      if (i % 1000 === 0) {
        console.log(`VDF progress: ${i}/${difficulty} iterations (${(i/difficulty*100).toFixed(2)}%)`);
      }
    }
    
    // Generate the proof (in a real implementation, this would be more complex)
    // For Wesolowski's VDF, we need to compute x^(2^T/l) mod N
    const l = 257n; // The prime used in our verifier
    const r = BigInt(difficulty) % l;
    const exponent = (BigInt(2) ** BigInt(difficulty) - r) / l;
    
    let pi = x;
    for (let i = 0; i < exponent; i++) {
      pi = (pi * pi) % BigInt(PRIME_MODULUS);
    }
    
    // Convert back to bytes
    const outputBytes = hexToBytes(y.toString(16).padStart(64, '0'));
    const proofBytes = hexToBytes(pi.toString(16).padStart(64, '0'));
    
    return {
      output: outputBytes,
      proof: proofBytes
    };
  }
  
  /**
   * Decrypts a revealed capsule
   * @param {string} capsuleId - The ID of the capsule
   * @returns {Promise<string>} - The decrypted message
   */
  async decryptCapsule(capsuleId) {
    // Get the encrypted message and decryption key from the contract
    const encryptedData = await this.contract.getCapsuleMessage(capsuleId);
    const decryptionKey = await this.contract.getCapsuleDecryptionKey(capsuleId);
    
    if (!decryptionKey || ethers.utils.arrayify(decryptionKey).length === 0) {
      throw new Error("Capsule not yet revealed");
    }
    
    // Parse the encrypted data
    const parsedData = JSON.parse(ethers.utils.toUtf8String(encryptedData));
    
    // Decrypt the message
    const decipher = crypto.createDecipheriv(
      'aes-256-cbc',
      Buffer.from(ethers.utils.arrayify(decryptionKey)),
      Buffer.from(parsedData.iv, 'hex')
    );
    
    let decrypted = decipher.update(parsedData.data, 'hex', 'utf8');
    decrypted += decipher.final('utf8');
    
    return decrypted;
  }
  
  /**
   * Gets information about a capsule
   * @param {string} capsuleId - The ID of the capsule
   * @returns {Promise<object>} - The capsule info
   */
  async getCapsuleInfo(capsuleId) {
    const info = await this.contract.getCapsuleInfo(capsuleId);
    
    return {
      title: info.title,
      creator: info.creator,
      creationTime: new Date(info.creationTime.toNumber() * 1000),
      difficulty: info.difficulty.toNumber(),
      revealed: info.revealed
    };
  }
}

// Helper function to convert hex string to bytes
function hexToBytes(hex) {
  const bytes = new Uint8Array(hex.length / 2);
  for (let i = 0; i < hex.length; i += 2) {
    bytes[i / 2] = parseInt(hex.substr(i, 2), 16);
  }
  return bytes;
}

// Constants
const PRIME_MODULUS = '0xFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEFFFFFC2F';

module.exports = SeldonVaultVDFClient; 