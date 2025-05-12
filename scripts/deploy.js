const { ethers } = require("hardhat");

async function main() {
  console.log("Deploying Seldon Time Vault contracts...");

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
  
  // Verify contracts on Polygonscan (optional)
  if (process.env.POLYGONSCAN_API_KEY) {
    console.log("Waiting for block confirmations...");
    // Wait for 6 block confirmations
    await seldonVault.deployTransaction.wait(6);
    
    console.log("Verifying contracts on Polygonscan...");
    await hre.run("verify:verify", {
      address: vdfVerifier.address,
      constructorArguments: [],
    });
    
    await hre.run("verify:verify", {
      address: seldonVault.address,
      constructorArguments: [vdfVerifier.address],
    });
  }
  
  console.log("Deployment complete!");
}

main()
  .then(() => process.exit(0))
  .catch((error) => {
    console.error(error);
    process.exit(1);
  }); 