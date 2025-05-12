const { ethers } = require("hardhat");
const crypto = require("crypto");

async function main() {
  // Get the deployed contract addresses
  const SeldonVaultVDF = await ethers.getContractFactory("SeldonVaultVDF");
  const seldonVault = await SeldonVaultVDF.attach("YOUR_DEPLOYED_CONTRACT_ADDRESS");
  
  // Get the first capsule
  const capsuleId = 0;
  const challenge = await seldonVault.getVDFChallenge(capsuleId);
  
  console.log("VDF Challenge:");
  console.log("Seed:", challenge.seed);
  console.log("Difficulty:", challenge.difficulty.toString());
  
  // Compute the VDF (simplified version)
  console.log("\nComputing VDF...");
  
  // Convert seed to a number (BigInt)
  const PRIME_MODULUS = BigInt("0xFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEFFFFFC2F");
  const seed = challenge.seed;
  const x = BigInt("0x" + Buffer.from(ethers.utils.arrayify(seed)).toString("hex")) % PRIME_MODULUS;
  
  // Perform repeated squaring
  let y = x;
  const difficulty = challenge.difficulty.toNumber();
  
  console.log(`Starting computation with difficulty ${difficulty}...`);
  const startTime = Date.now();
  
  for (let i = 0; i < difficulty; i++) {
    y = (y * y) % PRIME_MODULUS;
    
    if (i % 100 === 0) {
      const progress = (i / difficulty) * 100;
      console.log(`Progress: ${progress.toFixed(2)}%`);
    }
  }
  
  const endTime = Date.now();
  console.log(`VDF computation completed in ${(endTime - startTime) / 1000} seconds`);
  
  // Generate the proof (simplified)
  const l = BigInt(257); // The prime used in our verifier
  const r = BigInt(difficulty) % l;
  const exponent = (BigInt(2) ** BigInt(difficulty) - r) / l;
  
  let pi = x;
  for (let i = 0; i < Number(exponent); i++) {
    pi = (pi * pi) % PRIME_MODULUS;
  }
  
  // Convert to bytes
  const outputBytes = ethers.utils.arrayify("0x" + y.toString(16).padStart(64, "0"));
  const proofBytes = ethers.utils.arrayify("0x" + pi.toString(16).padStart(64, "0"));
  
  console.log("\nVDF Result:");
  console.log("Output:", ethers.utils.hexlify(outputBytes));
  console.log("Proof:", ethers.utils.hexlify(proofBytes));
  
  // Submit the proof
  console.log("\nSubmitting proof to the contract...");
  const tx = await seldonVault.submitVDFProof(capsuleId, proofBytes, outputBytes);
  await tx.wait();
  
  console.log("Proof submitted successfully!");
  
  // Check if the capsule is revealed
  const info = await seldonVault.getCapsuleInfo(capsuleId);
  console.log("\nCapsule status:", info.revealed ? "Revealed" : "Not revealed");
}

main()
  .then(() => process.exit(0))
  .catch((error) => {
    console.error(error);
    process.exit(1);
  }); 