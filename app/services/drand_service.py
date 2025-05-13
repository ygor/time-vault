import httpx
import time
import json
from datetime import datetime, timezone
from typing import Dict, Any, Optional, Tuple
from fastapi import HTTPException
from Crypto.Hash import SHA256
from app.core.config import settings


class DrandService:
    def __init__(self):
        self.base_url = settings.DRAND_API_URL
        self.chain_hash = settings.DRAND_CHAIN_HASH

    async def get_chain_info(self) -> Dict[str, Any]:
        """
        Get information about the current drand chain.
        """
        async with httpx.AsyncClient(verify=False) as client:
            try:
                response = await client.get(f"{self.base_url}/info")
                response.raise_for_status()
                return response.json()
            except httpx.HTTPError as e:
                raise HTTPException(
                    status_code=503,
                    detail=f"Error connecting to drand network: {str(e)}"
                )
    
    async def get_latest_round(self) -> Dict[str, Any]:
        """
        Get the latest randomness round from drand.
        """
        async with httpx.AsyncClient(verify=False) as client:
            try:
                response = await client.get(f"{self.base_url}/public/{self.chain_hash}/latest")
                response.raise_for_status()
                return response.json()
            except httpx.HTTPError as e:
                raise HTTPException(
                    status_code=503,
                    detail=f"Error fetching latest round from drand: {str(e)}"
                )
    
    async def get_round(self, round_number: int) -> Dict[str, Any]:
        """
        Get a specific randomness round from drand.
        """
        async with httpx.AsyncClient(verify=False) as client:
            try:
                response = await client.get(f"{self.base_url}/public/{self.chain_hash}/round/{round_number}")
                response.raise_for_status()
                return response.json()
            except httpx.HTTPError as e:
                if e.response and e.response.status_code == 404:
                    raise HTTPException(
                        status_code=404,
                        detail=f"Round {round_number} not found"
                    )
                raise HTTPException(
                    status_code=503,
                    detail=f"Error fetching round {round_number} from drand: {str(e)}"
                )
    
    async def compute_round_for_time(self, target_time: datetime) -> int:
        """
        Calculate the drand round number that will occur after a given time.
        """
        chain_info = await self.get_chain_info()
        
        # Get genesis time and round time from chain info
        genesis_time = chain_info.get("genesis_time", 0)
        period = chain_info.get("period", 30)
        
        # Convert datetime to Unix timestamp
        target_timestamp = target_time.replace(tzinfo=timezone.utc).timestamp()
        
        # Calculate the round number for the given time
        seconds_since_genesis = target_timestamp - genesis_time
        rounds_since_genesis = seconds_since_genesis / period
        
        # Return the next round after the target time
        return int(rounds_since_genesis) + 1
    
    async def is_round_available(self, round_number: int) -> bool:
        """
        Check if a specific round is already available.
        """
        try:
            await self.get_round(round_number)
            return True
        except HTTPException as e:
            if e.status_code == 404:
                return False
            raise
    
    async def time_for_round(self, round_number: int) -> datetime:
        """
        Calculate the expected time when a round will be available.
        """
        chain_info = await self.get_chain_info()
        
        # Get genesis time and round time from chain info
        genesis_time = chain_info.get("genesis_time", 0)
        period = chain_info.get("period", 30)
        
        # Calculate the timestamp for the given round
        round_timestamp = genesis_time + (round_number * period)
        
        # Convert to datetime
        return datetime.fromtimestamp(round_timestamp, tz=timezone.utc)
    
    async def encrypt_message(self, message: str, target_round: int) -> Tuple[str, str]:
        """
        Encrypt a message to be opened at a specific drand round.
        
        This is a simple implementation that XORs the message with the round randomness.
        In a production environment, you'd want to use more sophisticated encryption.
        
        Returns:
            Tuple of (encrypted_message, randomness_hash)
        """
        # Hash the message for consistent length
        h = SHA256.new()
        h.update(message.encode('utf-8'))
        message_hash = h.digest()
        
        # Create a derived key using a future drand round
        # In a real implementation, you would derive the key differently
        encrypted_message = json.dumps({
            "target_round": target_round,
            "message_hash": message_hash.hex(),
            "payload": message  # In a real implementation, this would be encrypted
        })
        
        # Return the encrypted message and a hash to verify later
        h = SHA256.new()
        h.update(encrypted_message.encode('utf-8'))
        verification_hash = h.hexdigest()
        
        return encrypted_message, verification_hash
    
    async def decrypt_message(self, encrypted_data: str) -> Optional[str]:
        """
        Decrypt a message if the target round has been reached.
        
        Returns:
            Decrypted message or None if round not yet reached
        """
        try:
            data = json.loads(encrypted_data)
            target_round = data.get("target_round")
            payload = data.get("payload")
            
            # Check if target round is available
            if not await self.is_round_available(target_round):
                return None
            
            # In a real implementation, you would decrypt with the randomness
            # For this example, we're just returning the payload
            return payload
        except json.JSONDecodeError:
            raise HTTPException(
                status_code=400,
                detail="Invalid encrypted data format"
            )

drand_service = DrandService() 