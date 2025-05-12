const { ethers } = require("hardhat");

async function main() {
  console.log("Deploying Seldon Time Vault contracts to local network...");

  // Get signers (local accounts)
  const [deployer] = await ethers.getSigners();
  console.log("Deploying with account:", deployer.address);
  
  // Deploy the VDF Verifier first
  const WesolowskiVDFVerifier = await ethers.getContractFactory("WesolowskiVDFVerifier");
  const vdfVerifier = await WesolowskiVDFVerifier.deploy();
  await vdfVerifier.deployed();
  
  console.log("WesolowskiVDFVerifier deployed to:", vdfVerifier.address);

  // Deploy the Seldon Vault with the VDF Verifier address
  const SeldonVaultVDF = await ethers.getContractFactory("SeldonVaultVDF");
  const seldonVault = await SeldonVaultVDF.deploy(vdfVerifier.address);
  await seldonVault.deployed();
  
  console.log("SeldonVaultVDF deployed to:", seldonVault.address);
  
  // Print deployment information for frontend configuration
  console.log("\n----- Deployment Information -----");
  console.log(`
  Update your frontend/src/contracts.js with:
  
  export const SeldonVaultVDFAddress = "${seldonVault.address}";
  `);
  
  // Create a sample time capsule for testing
  console.log("\nCreating a sample time capsule...");
  
  // Sample encrypted message (in a real scenario, this would be encrypted client-side)
  const sampleMessage = ethers.utils.toUtf8Bytes(JSON.stringify({
    iv: "sample_iv",
    data: "sample_encrypted_data"
  }));
  
  // Sample seed for VDF
  const seed = ethers.utils.randomBytes(32);
  
  // Create the time capsule
  const tx = await seldonVault.createTimeCapsule(
    sampleMessage,
    1000, // Low difficulty for quick testing
    seed,
    "Sample Time Capsule"
  );
  
  await tx.wait();
  console.log("Sample time capsule created!");
  
  console.log("\nLocal deployment complete! Your contracts are ready for testing.");
}

main()
  .then(() => process.exit(0))
  .catch((error) => {
    console.error(error);
    process.exit(1);
  }); 