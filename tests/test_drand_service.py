import pytest
import pytest_asyncio
import asyncio
from datetime import datetime, timedelta, timezone
from unittest.mock import patch, AsyncMock
import json

from app.services.drand_service import DrandService


@pytest.fixture
def drand_service():
    """Create a DrandService instance for testing."""
    return DrandService()


@pytest.fixture
def mock_chain_info():
    """Mock response for drand chain info."""
    return {
        "genesis_time": 1595431050,
        "period": 30,
        "hash": "8990e7a9aaed2ffed73dbd7092123d6f289930540d7651336225dc172e51b2ce"
    }


@pytest.fixture
def mock_latest_round():
    """Mock response for drand latest round."""
    return {
        "round": 100000,
        "randomness": "7d5ea30788b1da94219251e9b368cea848cc7ad9a7085ff9fe571c7c82891602",
        "signature": "a912a969b798e5725a27bad8db6e576dab18a19778e8e9468c554733709d6603",
        "previous_signature": "8b18303035d6c92b68a9db19761c2922a57e8e3de869d3711e67b72e29640dec"
    }


@pytest.fixture
def mock_specific_round():
    """Mock response for a specific drand round."""
    return {
        "round": 100500,
        "randomness": "9b5da30788b1da94219251e9b368cea848cc7ad9a7085ff9fe571c7c82891724",
        "signature": "ff12a969b798e5725a27bad8db6e576dab18a19778e8e9468c554733709d1234",
        "previous_signature": "a3d8303035d6c92b68a9db19761c2922a57e8e3de869d3711e67b72e2964deed"
    }


@pytest.mark.asyncio
async def test_compute_round_for_time(drand_service, mock_chain_info):
    """Test computing the expected drand round for a future time."""
    with patch.object(drand_service, 'get_chain_info', new_callable=AsyncMock) as mock_get_info:
        mock_get_info.return_value = mock_chain_info
        
        # Test with current time
        now = datetime.now(timezone.utc)
        current_timestamp = now.timestamp()
        seconds_since_genesis = current_timestamp - mock_chain_info["genesis_time"]
        expected_round = int(seconds_since_genesis / mock_chain_info["period"]) + 1
        
        calculated_round = await drand_service.compute_round_for_time(now)
        assert calculated_round == expected_round
        
        # Test with a future time
        future_time = now + timedelta(hours=1)
        future_timestamp = future_time.timestamp()
        future_seconds = future_timestamp - mock_chain_info["genesis_time"]
        future_expected_round = int(future_seconds / mock_chain_info["period"]) + 1
        
        future_calculated_round = await drand_service.compute_round_for_time(future_time)
        assert future_calculated_round == future_expected_round


@pytest.mark.asyncio
async def test_time_for_round(drand_service, mock_chain_info):
    """Test calculating the expected time for a specific drand round."""
    with patch.object(drand_service, 'get_chain_info', new_callable=AsyncMock) as mock_get_info:
        mock_get_info.return_value = mock_chain_info
        
        test_round = 100000
        expected_timestamp = mock_chain_info["genesis_time"] + (test_round * mock_chain_info["period"])
        expected_time = datetime.fromtimestamp(expected_timestamp, tz=timezone.utc)
        
        calculated_time = await drand_service.time_for_round(test_round)
        # Compare timestamps rounded to seconds to avoid microsecond differences
        assert calculated_time.timestamp() == pytest.approx(expected_time.timestamp(), abs=1)


@pytest.mark.asyncio
async def test_is_round_available(drand_service, mock_specific_round):
    """Test checking if a drand round is available."""
    with patch.object(drand_service, 'get_round', new_callable=AsyncMock) as mock_get_round:
        # Test when round is available
        mock_get_round.return_value = mock_specific_round
        is_available = await drand_service.is_round_available(100500)
        assert is_available is True
        
        # Test when round is not available
        from fastapi import HTTPException
        mock_get_round.side_effect = HTTPException(status_code=404, detail="Round not found")
        is_available = await drand_service.is_round_available(999999)
        assert is_available is False


@pytest.mark.asyncio
async def test_encrypt_and_decrypt_message(drand_service, mock_specific_round):
    """Test encrypting and decrypting a message using drand."""
    test_message = "This is a secret time-locked message"
    target_round = 100500
    
    # Test encryption
    encrypted_message, verification_hash = await drand_service.encrypt_message(test_message, target_round)
    
    # Verify encrypted message format
    encrypted_data = json.loads(encrypted_message)
    assert encrypted_data["target_round"] == target_round
    assert "message_hash" in encrypted_data
    assert encrypted_data["payload"] == test_message  # In a real implementation this would be encrypted
    
    # Mock the is_round_available method for testing decryption
    with patch.object(drand_service, 'is_round_available', new_callable=AsyncMock) as mock_check_round:
        # Test when round is not yet available
        mock_check_round.return_value = False
        decrypted = await drand_service.decrypt_message(encrypted_message)
        assert decrypted is None
        
        # Test when round is available
        mock_check_round.return_value = True
        decrypted = await drand_service.decrypt_message(encrypted_message)
        assert decrypted == test_message


@pytest.mark.asyncio
async def test_end_to_end_time_locking(drand_service, mock_chain_info, mock_specific_round):
    """Test the full time-locking process with drand."""
    with patch.multiple(
        drand_service,
        get_chain_info=AsyncMock(return_value=mock_chain_info),
        get_round=AsyncMock(return_value=mock_specific_round),
        is_round_available=AsyncMock(side_effect=[False, True])  # First not available, then available
    ):
        # 1. Create a message and set unlock time
        test_message = "Top secret prediction for the future"
        now = datetime.now(timezone.utc)
        unlock_time = now + timedelta(hours=1)
        
        # 2. Calculate target round for the unlock time
        target_round = await drand_service.compute_round_for_time(unlock_time)
        
        # 3. Encrypt the message
        encrypted_message, verification_hash = await drand_service.encrypt_message(test_message, target_round)
        
        # 4. Try to decrypt before the target round (should fail)
        decrypted = await drand_service.decrypt_message(encrypted_message)
        assert decrypted is None
        
        # 5. "Time passes" and now the round is available
        # The mock for is_round_available will return True on the second call
        
        # 6. Try to decrypt after the target round (should succeed)
        decrypted = await drand_service.decrypt_message(encrypted_message)
        assert decrypted == test_message 