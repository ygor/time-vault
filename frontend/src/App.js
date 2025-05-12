import React, { useState, useEffect } from 'react';
import { ethers } from 'ethers';
import './App.css';
import SeldonVaultVDFClient from './seldon-vault-vdf';
import { SeldonVaultVDFAddress, SeldonVaultVDFABI } from './contracts';

function App() {
  const [account, setAccount] = useState(null);
  const [provider, setProvider] = useState(null);
  const [client, setClient] = useState(null);
  const [capsules, setCapsules] = useState([]);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState('');
  const [title, setTitle] = useState('');
  const [difficulty, setDifficulty] = useState(1000);
  const [selectedCapsule, setSelectedCapsule] = useState(null);
  const [decryptedMessage, setDecryptedMessage] = useState('');

  // Connect to wallet
  const connectWallet = async () => {
    if (window.ethereum) {
      try {
        const accounts = await window.ethereum.request({ method: 'eth_requestAccounts' });
        const provider = new ethers.providers.Web3Provider(window.ethereum);
        const network = await provider.getNetwork();
        
        if (network.chainId !== 80001) { // Mumbai testnet
          alert('Please connect to Polygon Mumbai Testnet');
          try {
            await window.ethereum.request({
              method: 'wallet_switchEthereumChain',
              params: [{ chainId: '0x13881' }], // Mumbai chainId in hex
            });
          } catch (error) {
            console.error(error);
          }
          return;
        }
        
        setAccount(accounts[0]);
        setProvider(provider);
        
        const client = new SeldonVaultVDFClient(SeldonVaultVDFAddress, provider);
        setClient(client);
        
        loadCapsules();
      } catch (error) {
        console.error(error);
      }
    } else {
      alert('Please install MetaMask to use this dApp');
    }
  };

  // Add this function to the App component to handle local network
  const connectLocalWallet = async () => {
    if (window.ethereum) {
      try {
        const accounts = await window.ethereum.request({ method: 'eth_requestAccounts' });
        const provider = new ethers.providers.Web3Provider(window.ethereum);
        const network = await provider.getNetwork();
        
        // For local development, we don't check the chain ID
        setAccount(accounts[0]);
        setProvider(provider);
        
        const client = new SeldonVaultVDFClient(SeldonVaultVDFAddress, provider);
        setClient(client);
        
        loadCapsules();
      } catch (error) {
        console.error(error);
      }
    } else {
      alert('Please install MetaMask to use this dApp');
    }
  };

  // Load user's capsules
  const loadCapsules = async () => {
    if (!client) return;
    
    setLoading(true);
    try {
      // This is a simplified approach - in a real app, you'd use events or a backend to track user's capsules
      // For demo purposes, we'll just check the last 10 capsules
      const capsules = [];
      const nextCapsuleId = await client.contract.nextCapsuleId();
      
      for (let i = Math.max(0, nextCapsuleId - 10); i < nextCapsuleId; i++) {
        try {
          const info = await client.getCapsuleInfo(i);
          if (info.creator.toLowerCase() === account.toLowerCase()) {
            capsules.push({
              id: i,
              title: info.title,
              creationTime: info.creationTime,
              difficulty: info.difficulty,
              revealed: info.revealed
            });
          }
        } catch (error) {
          console.error(`Error loading capsule ${i}:`, error);
        }
      }
      
      setCapsules(capsules);
    } catch (error) {
      console.error('Error loading capsules:', error);
    }
    setLoading(false);
  };

  // Create a new time capsule
  const createCapsule = async () => {
    if (!client || !message || !title) return;
    
    setLoading(true);
    try {
      const result = await client.createTimeCapsule(message, difficulty, title);
      alert(`Time capsule created with ID: ${result.capsuleId}`);
      
      // Store the symmetric key in local storage (in a real app, you'd use a more secure method)
      const userCapsules = JSON.parse(localStorage.getItem('userCapsules') || '{}');
      userCapsules[result.capsuleId] = {
        symmetricKey: result.symmetricKey,
        title: title
      };
      localStorage.setItem('userCapsules', JSON.stringify(userCapsules));
      
      setMessage('');
      setTitle('');
      loadCapsules();
    } catch (error) {
      console.error('Error creating capsule:', error);
      alert('Error creating time capsule');
    }
    setLoading(false);
  };

  // Solve VDF challenge for a capsule
  const solveCapsule = async (capsuleId) => {
    if (!client) return;
    
    setLoading(true);
    try {
      await client.solveVDFChallenge(capsuleId);
      alert('Capsule revealed successfully!');
      loadCapsules();
    } catch (error) {
      console.error('Error solving capsule:', error);
      alert('Error revealing capsule');
    }
    setLoading(false);
  };

  // View a capsule
  const viewCapsule = async (capsuleId) => {
    if (!client) return;
    
    setLoading(true);
    try {
      const info = await client.getCapsuleInfo(capsuleId);
      setSelectedCapsule({
        id: capsuleId,
        ...info
      });
      
      if (info.revealed) {
        try {
          const decrypted = await client.decryptCapsule(capsuleId);
          setDecryptedMessage(decrypted);
        } catch (error) {
          console.error('Error decrypting message:', error);
          setDecryptedMessage('Error decrypting message');
        }
      } else {
        setDecryptedMessage('');
      }
    } catch (error) {
      console.error('Error viewing capsule:', error);
    }
    setLoading(false);
  };

  // Format date
  const formatDate = (date) => {
    return new Date(date).toLocaleString();
  };

  // Calculate estimated time to solve
  const estimatedTime = () => {
    // Very rough estimate: 1000 difficulty ≈ 1 minute
    const minutes = difficulty / 1000;
    if (minutes < 60) return `${minutes.toFixed(1)} minutes`;
    if (minutes < 1440) return `${(minutes / 60).toFixed(1)} hours`;
    return `${(minutes / 1440).toFixed(1)} days`;
  };

  useEffect(() => {
    if (window.ethereum) {
      window.ethereum.on('accountsChanged', (accounts) => {
        setAccount(accounts[0]);
        loadCapsules();
      });
    }
  }, []);

  return (
    <div className="App">
      <header className="App-header">
        <h1>Seldon Time Vault</h1>
        {!account ? (
          <>
            <button onClick={connectWallet}>Connect Wallet</button>
            <button onClick={connectLocalWallet}>Connect to Local Network</button>
          </>
        ) : (
          <p>Connected: {account.substring(0, 6)}...{account.substring(38)}</p>
        )}
      </header>
      
      {account && (
        <div className="container">
          <div className="create-capsule">
            <h2>Create New Time Capsule</h2>
            <input
              type="text"
              placeholder="Message Title"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
            />
            <textarea
              placeholder="Your message to the future..."
              value={message}
              onChange={(e) => setMessage(e.target.value)}
            />
            <div className="difficulty-selector">
              <label>Time Delay (Difficulty): {difficulty}</label>
              <input
                type="range"
                min="100"
                max="10000"
                step="100"
                value={difficulty}
                onChange={(e) => setDifficulty(parseInt(e.target.value))}
              />
              <p>Estimated time to reveal: {estimatedTime()}</p>
            </div>
            <button onClick={createCapsule} disabled={loading || !message || !title}>
              {loading ? 'Creating...' : 'Create Time Capsule'}
            </button>
          </div>
          
          <div className="capsules-list">
            <h2>Your Time Capsules</h2>
            {loading ? (
              <p>Loading...</p>
            ) : capsules.length === 0 ? (
              <p>No time capsules found</p>
            ) : (
              <ul>
                {capsules.map((capsule) => (
                  <li key={capsule.id} className={capsule.revealed ? 'revealed' : ''}>
                    <div className="capsule-info">
                      <h3>{capsule.title}</h3>
                      <p>Created: {formatDate(capsule.creationTime)}</p>
                      <p>Status: {capsule.revealed ? 'Revealed' : 'Sealed'}</p>
                    </div>
                    <div className="capsule-actions">
                      <button onClick={() => viewCapsule(capsule.id)}>
                        View
                      </button>
                      {!capsule.revealed && (
                        <button onClick={() => solveCapsule(capsule.id)}>
                          Reveal
                        </button>
                      )}
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </div>
          
          {selectedCapsule && (
            <div className="capsule-details">
              <h2>{selectedCapsule.title}</h2>
              <p>Created: {formatDate(selectedCapsule.creationTime)}</p>
              <p>Status: {selectedCapsule.revealed ? 'Revealed' : 'Sealed'}</p>
              
              {selectedCapsule.revealed ? (
                <div className="revealed-message">
                  <h3>Message:</h3>
                  <div className="message-content">{decryptedMessage}</div>
                </div>
              ) : (
                <div className="sealed-message">
                  <p>This message is still sealed.</p>
                  <p>Difficulty: {selectedCapsule.difficulty}</p>
                  <button onClick={() => solveCapsule(selectedCapsule.id)}>
                    Start Revealing Process
                  </button>
                </div>
              )}
              
              <button onClick={() => setSelectedCapsule(null)}>Close</button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

export default App; 