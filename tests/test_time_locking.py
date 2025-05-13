import pytest
import pytest_asyncio
import asyncio
from httpx import AsyncClient
from datetime import datetime, timedelta, timezone
from unittest.mock import patch, AsyncMock
import json

from app.services.drand_service import drand_service
from main import app
from tests.test_e2e import authenticate_user, create_test_vault
from app.api.v1.vaults import VAULTS_DB


# Note: client fixture is now provided by conftest.py

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
        "signature": "a912a969b798e5725a27bad8db6e576dab18a19778e8e9468c554733709d6603"
    }


@pytest.mark.asyncio
async def test_message_unlocking_with_drand(client, mock_chain_info, mock_latest_round):
    """
    Test the full flow of creating a time-locked message and unlocking it
    with drand beacon rounds.
    """
    # Authenticate user
    token = await authenticate_user(client)
    headers = {"Authorization": f"Bearer {token}"}
    
    # Create a vault
    vault_id = await create_test_vault(client, token)
    
    # Now we'll patch the drand service to control time and rounds
    # First, set up our timeframe
    now = datetime.now(timezone.utc)
    unlock_time = now + timedelta(minutes=10)
    target_round = 100500  # This would normally be calculated from the unlock time
    
    # Create patches for drand service methods
    with patch.multiple(
        drand_service,
        get_chain_info=AsyncMock(return_value=mock_chain_info),
        get_latest_round=AsyncMock(return_value=mock_latest_round),
        compute_round_for_time=AsyncMock(return_value=target_round),
        is_round_available=AsyncMock(side_effect=[False, True]),  # First locked, then unlocked
        encrypt_message=AsyncMock(return_value=("encrypted_content", "verification_hash")),
        decrypt_message=AsyncMock(side_effect=[None, "Decrypted secret message"])  # First None, then content
    ):
        # Create a time-locked message
        message_content = "This is a secret that should be locked until the future"
        response = await client.post(
            f"/v1/vaults/{vault_id}/messages",
            headers=headers,
            json={
                "title": "Time Locked Secret",
                "content_type": "text",
                "content": message_content,
                "unlock_time": unlock_time.isoformat()
            }
        )
        assert response.status_code == 201
        message_data = response.json()
        message_id = message_data["id"]
        
        # Verify the message is locked
        assert message_data["is_locked"] == True
        assert "content" not in message_data or message_data["content"] is None
        
        # Try to access the message - it should still be locked
        response = await client.get(
            f"/v1/vaults/{vault_id}/messages/{message_id}",
            headers=headers
        )
        assert response.status_code == 200
        message = response.json()
        assert message["is_locked"] == True
        assert "content" not in message or message["content"] is None
        
        # Now simulate time passing and the drand round becoming available
        # The mock for is_round_available will return True on the second call
        
        # Make is_round_available always return True from now on
        drand_service.is_round_available = AsyncMock(return_value=True)
        
        # Manually set unlock time to the past in the database to force message unlocking
        for vault in VAULTS_DB.values():
            if vault.id == vault_id:
                for msg in vault.messages:
                    if msg.id == message_id:
                        # Set unlock time to 5 minutes in the past
                        msg.unlock_time = datetime.now(timezone.utc) - timedelta(minutes=5)
                        break
                break
        
        # Access the message again - it should now be unlocked
        response = await client.get(
            f"/v1/vaults/{vault_id}/messages/{message_id}",
            headers=headers
        )
        assert response.status_code == 200
        message = response.json()
        assert message["is_locked"] == False
        assert message["content"] is not None
        
        # Verify all mocked drand methods were called with expected arguments
        drand_service.encrypt_message.assert_called_once()
        assert drand_service.is_round_available.call_count >= 1


@pytest.mark.asyncio
async def test_concurrent_users_with_shared_vault(client):
    """
    Test multiple users accessing time-locked content in a shared vault.
    """
    # Set up two users
    token1 = await authenticate_user(client)
    headers1 = {"Authorization": f"Bearer {token1}"}
    
    # Create a vault
    vault_id = await create_test_vault(client, token1)
    
    # Create a second test user
    second_wallet = "0xabcdef1234567890abcdef1234567890abcdef12"
    response = await client.post(
        "/v1/auth/authenticate",
        json={
            "identifier": second_wallet,
            "verification_code": "0xsignature_for_second_user",
            "username": "second_user"
        }
    )
    assert response.status_code == 200
    token2 = response.json()["token"]
    headers2 = {"Authorization": f"Bearer {token2}"}
    
    # First user shares the vault with second user with read permission
    response = await client.post(
        f"/v1/vaults/{vault_id}/share",
        headers=headers1,
        json={
            "identifier": second_wallet,
            "permission": "read"
        }
    )
    if response.status_code == 422:
        print(f"DEBUG: 422 Response body for sharing: {response.text}")
    assert response.status_code == 200
    
    # Setup mock for drand service
    target_round = 100500
    with patch.multiple(
        drand_service,
        compute_round_for_time=AsyncMock(return_value=target_round),
        encrypt_message=AsyncMock(return_value=("encrypted_content", "verification_hash")),
        is_round_available=AsyncMock(side_effect=[False, False, True, True])  # Two users check twice
    ):
        # First user creates a time-locked message
        unlock_time = datetime.now(timezone.utc) + timedelta(minutes=30)
        response = await client.post(
            f"/v1/vaults/{vault_id}/messages",
            headers=headers1,
            json={
                "title": "Shared Secret",
                "content_type": "text",
                "content": "This is a secret for both users",
                "unlock_time": unlock_time.isoformat()
            }
        )
        assert response.status_code == 201
        message_id = response.json()["id"]
        
        # Both users try to access the message - it should be locked for both
        response1 = await client.get(
            f"/v1/vaults/{vault_id}/messages/{message_id}",
            headers=headers1
        )
        assert response1.status_code == 200
        message1 = response1.json()
        assert message1["is_locked"] == True
        
        response2 = await client.get(
            f"/v1/vaults/{vault_id}/messages/{message_id}",
            headers=headers2
        )
        assert response2.status_code == 200
        message2 = response2.json()
        assert message2["is_locked"] == True
        
        # Now simulate time passing and the drand round becoming available
        # Both users should now be able to see the unlocked content
        
        # Make is_round_available always return True from now on
        drand_service.is_round_available = AsyncMock(return_value=True)
        
        # Manually set unlock time to the past in the database to force message unlocking
        for vault in VAULTS_DB.values():
            if vault.id == vault_id:
                for msg in vault.messages:
                    if msg.id == message_id:
                        # Set unlock time to 5 minutes in the past
                        msg.unlock_time = datetime.now(timezone.utc) - timedelta(minutes=5)
                        break
                break
        
        response1 = await client.get(
            f"/v1/vaults/{vault_id}/messages/{message_id}",
            headers=headers1
        )
        assert response1.status_code == 200
        message1 = response1.json()
        assert message1["is_locked"] == False
        
        response2 = await client.get(
            f"/v1/vaults/{vault_id}/messages/{message_id}",
            headers=headers2
        )
        assert response2.status_code == 200
        message2 = response2.json()
        assert message2["is_locked"] == False


@pytest.mark.asyncio
async def test_different_message_types(client):
    """
    Test time-locking for different message content types.
    """
    # Authenticate user
    token = await authenticate_user(client)
    headers = {"Authorization": f"Bearer {token}"}
    
    # Create a vault
    vault_id = await create_test_vault(client, token)
    
    # Mock drand service
    target_round = 100500
    with patch.multiple(
        drand_service,
        compute_round_for_time=AsyncMock(return_value=target_round),
        encrypt_message=AsyncMock(return_value=("encrypted_content", "verification_hash")),
        is_round_available=AsyncMock(side_effect=[False, True, False, True])
    ):
        # Create a time-locked text message
        unlock_time = datetime.now(timezone.utc) + timedelta(minutes=30)
        response = await client.post(
            f"/v1/vaults/{vault_id}/messages",
            headers=headers,
            json={
                "title": "Text Message",
                "content_type": "text",
                "content": "This is a text message",
                "unlock_time": unlock_time.isoformat()
            }
        )
        assert response.status_code == 201
        text_message_id = response.json()["id"]
        
        # Create a time-locked image message
        response = await client.post(
            f"/v1/vaults/{vault_id}/messages",
            headers=headers,
            json={
                "title": "Image Message",
                "content_type": "image",
                "media_content_hash": "QmUNLLsPACCz1vLxQVkXqqLX5R1X345qqfHbsf67hvA3Nn",
                "unlock_time": unlock_time.isoformat()
            }
        )
        assert response.status_code == 201
        image_message_id = response.json()["id"]
        
        # Check the text message - first locked, then unlocked
        response = await client.get(
            f"/v1/vaults/{vault_id}/messages/{text_message_id}",
            headers=headers
        )
        message = response.json()
        assert message["is_locked"] == True
        
        # Make is_round_available always return True for the text message
        drand_service.is_round_available = AsyncMock(return_value=True)
        
        # Manually set unlock time to the past in the database to force text message unlocking
        for vault in VAULTS_DB.values():
            if vault.id == vault_id:
                for msg in vault.messages:
                    if msg.id == text_message_id:
                        # Set unlock time to 5 minutes in the past
                        msg.unlock_time = datetime.now(timezone.utc) - timedelta(minutes=5)
                        break
                break
        
        response = await client.get(
            f"/v1/vaults/{vault_id}/messages/{text_message_id}",
            headers=headers
        )
        message = response.json()
        assert message["is_locked"] == False
        
        # Check the image message - first locked, then unlocked
        response = await client.get(
            f"/v1/vaults/{vault_id}/messages/{image_message_id}",
            headers=headers
        )
        message = response.json()
        assert message["is_locked"] == True
        
        # Make is_round_available always return True for the image message
        drand_service.is_round_available = AsyncMock(return_value=True)
        
        # Manually set unlock time to the past in the database to force image message unlocking
        for vault in VAULTS_DB.values():
            if vault.id == vault_id:
                for msg in vault.messages:
                    if msg.id == image_message_id:
                        # Set unlock time to 5 minutes in the past
                        msg.unlock_time = datetime.now(timezone.utc) - timedelta(minutes=5)
                        break
                break
        
        response = await client.get(
            f"/v1/vaults/{vault_id}/messages/{image_message_id}",
            headers=headers
        )
        message = response.json()
        assert message["is_locked"] == False
        assert message["media_url"] is not None 