// SPDX-License-Identifier: MIT
pragma solidity ^0.8.17;

/**
 * @title VDFVerifier
 * @dev Contract for verifying Verifiable Delay Function proofs
 */
interface VDFVerifier {
    /**
     * @dev Verifies a VDF proof
     * @param seed The initial seed for the VDF
     * @param difficulty The difficulty parameter
     * @param proof The proof of computation
     * @param output The claimed output of the VDF
     * @return True if the proof is valid, false otherwise
     */
    function verify(bytes calldata seed, uint256 difficulty, bytes calldata proof, bytes calldata output) external view returns (bool);
} 