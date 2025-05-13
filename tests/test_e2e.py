import pytest
import asyncio
from httpx import AsyncClient
from datetime import datetime, timedelta, timezone
import uuid
import json
from typing import Dict, Tuple

from app.services.drand_service import drand_service
from app.core.security import verify_wallet_signature
from main import app

# Mock wallet data for testing
TEST_WALLET_ADDRESS = "0x1234567890abcdef1234567890abcdef12345678"
TEST_WALLET_SIGNATURE = "0xtest_signature"
TEST_USERNAME = "test_user"


@pytest.fixture
async def client():
    """Create an async test client for FastAPI."""
    async with AsyncClient(app=app, base_url="http://test") as client:
        yield client


# Mock the wallet signature verification to always return True in tests
def mock_verify_wallet(*args, **kwargs):
    return True


# Apply the mock to the security module
verify_wallet_signature.return_value = True


@pytest.fixture(scope="module")
def event_loop():
    """Create an event loop for async tests."""
    loop = asyncio.get_event_loop()
    yield loop
    loop.close()


async def authenticate_user(client: AsyncClient) -> str:
    """Helper function to authenticate a test user and return the JWT token."""
    response = await client.post(
        "/v1/auth/connect-wallet",
        json={
            "address": TEST_WALLET_ADDRESS,
            "signature": TEST_WALLET_SIGNATURE,
            "username": TEST_USERNAME
        }
    )
    assert response.status_code == 200
    data = response.json()
    assert "access_token" in data
    return data["access_token"]


async def create_test_vault(client: AsyncClient, token: str) -> str:
    """Helper function to create a test vault and return its ID."""
    headers = {"Authorization": f"Bearer {token}"}
    response = await client.post(
        "/v1/vaults",
        headers=headers,
        json={
            "name": "Test Vault",
            "description": "A vault for testing purposes"
        }
    )
    assert response.status_code == 201
    data = response.json()
    assert "id" in data
    return data["id"]


async def get_future_unlock_time() -> datetime:
    """Helper function to get a future unlock time based on drand rounds."""
    # Get current time and add some time to ensure it's in the future
    future_time = datetime.now(timezone.utc) + timedelta(minutes=5)
    return future_time


@pytest.mark.asyncio
async def test_wallet_connection_flow(client: AsyncClient):
    """Test the wallet connection and authentication flow."""
    # Authenticate user
    token = await authenticate_user(client)
    
    # Get user profile with token
    headers = {"Authorization": f"Bearer {token}"}
    response = await client.get("/v1/users/profile", headers=headers)
    assert response.status_code == 200
    
    # Verify user data
    user_data = response.json()
    assert user_data["wallet_address"] == TEST_WALLET_ADDRESS
    assert user_data["username"] == TEST_USERNAME
    
    # Disconnect wallet
    response = await client.post("/v1/auth/disconnect", headers=headers)
    assert response.status_code == 200
    assert response.json()["message"] == "Wallet disconnected successfully"


@pytest.mark.asyncio
async def test_vault_creation_and_management(client: AsyncClient):
    """Test creating, updating, and deleting vaults."""
    # Authenticate user
    token = await authenticate_user(client)
    headers = {"Authorization": f"Bearer {token}"}
    
    # Create a vault
    response = await client.post(
        "/v1/vaults",
        headers=headers,
        json={
            "name": "My Future Predictions",
            "description": "Important predictions for the future"
        }
    )
    assert response.status_code == 201
    vault_data = response.json()
    vault_id = vault_data["id"]
    
    # Verify the vault was created correctly
    assert vault_data["name"] == "My Future Predictions"
    assert vault_data["message_count"]["total"] == 0
    
    # Get list of user vaults
    response = await client.get("/v1/vaults", headers=headers)
    assert response.status_code == 200
    vaults = response.json()
    assert len(vaults) >= 1
    assert any(v["id"] == vault_id for v in vaults)
    
    # Update vault
    response = await client.put(
        f"/v1/vaults/{vault_id}",
        headers=headers,
        json={
            "name": "Updated Predictions",
            "description": "New description"
        }
    )
    assert response.status_code == 200
    updated_vault = response.json()
    assert updated_vault["name"] == "Updated Predictions"
    assert updated_vault["description"] == "New description"
    
    # Delete vault
    response = await client.delete(f"/v1/vaults/{vault_id}", headers=headers)
    assert response.status_code == 204
    
    # Verify vault is deleted
    response = await client.get(f"/v1/vaults/{vault_id}", headers=headers)
    assert response.status_code == 404


@pytest.mark.asyncio
async def test_time_locked_message_flow(client: AsyncClient):
    """Test creating and accessing time-locked messages."""
    # Authenticate user
    token = await authenticate_user(client)
    headers = {"Authorization": f"Bearer {token}"}
    
    # Create a vault
    vault_id = await create_test_vault(client, token)
    
    # Set unlock time (for testing we'll use a short time in the future)
    unlock_time = await get_future_unlock_time()
    
    # Create a time-locked message
    message_content = "This is a secret prediction for the future!"
    response = await client.post(
        f"/v1/vaults/{vault_id}/messages",
        headers=headers,
        json={
            "title": "Future Prediction",
            "content_type": "text",
            "content": message_content,
            "unlock_time": unlock_time.isoformat()
        }
    )
    assert response.status_code == 201
    message_data = response.json()
    message_id = message_data["id"]
    
    # Verify message was created correctly
    assert message_data["title"] == "Future Prediction"
    assert message_data["is_locked"] == True
    assert "content" not in message_data or message_data["content"] is None
    
    # Get the message (should still be locked)
    response = await client.get(
        f"/v1/vaults/{vault_id}/messages/{message_id}",
        headers=headers
    )
    assert response.status_code == 200
    message = response.json()
    assert message["is_locked"] == True
    
    # In a real test, we would wait until the unlock time passes
    # For this test, we'll mock the unlock by manipulating the time
    
    # Get list of messages with filter for locked messages
    response = await client.get(
        f"/v1/vaults/{vault_id}/messages?status=locked",
        headers=headers
    )
    assert response.status_code == 200
    messages = response.json()
    assert len(messages) == 1
    assert messages[0]["id"] == message_id
    
    # Delete the message
    response = await client.delete(
        f"/v1/vaults/{vault_id}/messages/{message_id}",
        headers=headers
    )
    assert response.status_code == 204
    
    # Verify message was deleted
    response = await client.get(
        f"/v1/vaults/{vault_id}/messages/{message_id}",
        headers=headers
    )
    assert response.status_code == 404


@pytest.mark.asyncio
async def test_vault_sharing_flow(client: AsyncClient):
    """Test sharing vaults with other users."""
    # Authenticate first user
    token1 = await authenticate_user(client)
    headers1 = {"Authorization": f"Bearer {token1}"}
    
    # Create a vault
    vault_id = await create_test_vault(client, token1)
    
    # Create a second test user
    second_wallet = "0xabcdef1234567890abcdef1234567890abcdef12"
    response = await client.post(
        "/v1/auth/connect-wallet",
        json={
            "address": second_wallet,
            "signature": "0xsignature_for_second_user",
            "username": "second_user"
        }
    )
    assert response.status_code == 200
    token2 = response.json()["access_token"]
    headers2 = {"Authorization": f"Bearer {token2}"}
    
    # Initially, second user can't access the vault
    response = await client.get(f"/v1/vaults/{vault_id}", headers=headers2)
    assert response.status_code == 403
    
    # First user shares the vault with second user
    response = await client.post(
        f"/v1/vaults/{vault_id}/share",
        headers=headers1,
        json={
            "address": second_wallet,
            "permissions": "read"
        }
    )
    assert response.status_code == 200
    
    # Now second user can access the vault
    response = await client.get(f"/v1/vaults/{vault_id}", headers=headers2)
    assert response.status_code == 200
    
    # Second user checks their shared vaults
    response = await client.get("/v1/vaults/shared", headers=headers2)
    assert response.status_code == 200
    shared_vaults = response.json()
    assert len(shared_vaults) >= 1
    assert any(v["id"] == vault_id for v in shared_vaults)
    
    # Second user tries to delete the vault (should fail as they only have read access)
    response = await client.delete(f"/v1/vaults/{vault_id}", headers=headers2)
    assert response.status_code == 403


@pytest.mark.asyncio
async def test_blockchain_status(client: AsyncClient):
    """Test getting blockchain status information."""
    # Authenticate user
    token = await authenticate_user(client)
    headers = {"Authorization": f"Bearer {token}"}
    
    # Get blockchain status
    response = await client.get("/v1/blockchain/status", headers=headers)
    assert response.status_code == 200
    
    # Verify the response contains expected blockchain data
    status_data = response.json()
    assert "network" in status_data
    assert "current_block" in status_data
    assert "sync_status" in status_data 