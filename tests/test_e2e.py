import pytest
import pytest_asyncio
import asyncio
from httpx import AsyncClient
from datetime import datetime, timedelta, timezone
import uuid
import json
from typing import Dict, Tuple

from app.services.drand_service import drand_service
from app.core.security import get_current_user
from app.api.v1.auth import USERS_DB
from main import app

# Mock user data for testing
TEST_USER_IDENTIFIER = "0x1234567890abcdef1234567890abcdef12345678"
TEST_VERIFICATION_CODE = "0xtest_signature"
TEST_USERNAME = "test_user"


# Note: client fixture is now provided by conftest.py

async def authenticate_user(client: AsyncClient) -> str:
    """Helper function to authenticate a test user and return the JWT token."""
    response = await client.post(
        "/v1/auth/authenticate",
        json={
            "identifier": TEST_USER_IDENTIFIER,
            "verification_code": TEST_VERIFICATION_CODE,
            "username": TEST_USERNAME
        }
    )
    assert response.status_code == 200
    data = response.json()
    assert "token" in data
    return data["token"]


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
async def test_authentication_flow(client: AsyncClient):
    """Test the user authentication flow."""
    # Authenticate user
    token = await authenticate_user(client)
    
    # Get user profile with token
    headers = {"Authorization": f"Bearer {token}"}
    response = await client.get("/v1/users/profile", headers=headers)
    assert response.status_code == 200
    
    # Verify user data
    user_data = response.json()
    assert user_data["identifier"] == TEST_USER_IDENTIFIER
    assert user_data["username"] == TEST_USERNAME
    
    # Sign out
    response = await client.post("/v1/auth/signout", headers=headers)
    assert response.status_code == 200
    assert response.json()["message"] == "Successfully signed out"


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
    second_user_identifier = "0xabcdef1234567890abcdef1234567890abcdef12"
    response = await client.post(
        "/v1/auth/authenticate",
        json={
            "identifier": second_user_identifier,
            "verification_code": "0xsignature_for_second_user",
            "username": "second_user"
        }
    )
    assert response.status_code == 200
    data = response.json()
    token2 = data["token"]
    headers2 = {"Authorization": f"Bearer {token2}"}
    
    # Share vault with second user
    response = await client.post(
        f"/v1/vaults/{vault_id}/share",
        headers=headers1,
        json={
            "identifier": second_user_identifier,
            "permission": "read"
        }
    )
    assert response.status_code == 200
    
    # Second user should now be able to access the vault
    response = await client.get(
        f"/v1/vaults/{vault_id}",
        headers=headers2
    )
    assert response.status_code == 200
    shared_vault = response.json()
    assert shared_vault["id"] == vault_id
    
    # List shared vaults for second user
    response = await client.get(
        "/v1/vaults/shared",
        headers=headers2
    )
    assert response.status_code == 200
    shared_vaults = response.json()
    assert len(shared_vaults) >= 1
    assert any(v["id"] == vault_id for v in shared_vaults)
    
    # Second user should not be able to delete the vault
    response = await client.delete(
        f"/v1/vaults/{vault_id}",
        headers=headers2
    )
    assert response.status_code in [403, 404]  # Either forbidden or not found
    
    # Get user ID for the second user from the shared_vaults response
    second_user_id = None
    for user_id, user in USERS_DB.items():
        if user.identifier == second_user_identifier:
            second_user_id = user_id
            break
    
    # Owner revokes access from second user
    response = await client.delete(
        f"/v1/vaults/{vault_id}/share/{second_user_id}",
        headers=headers1
    )
    assert response.status_code == 204
    
    # Second user should no longer be able to access the vault
    response = await client.get(
        f"/v1/vaults/{vault_id}",
        headers=headers2
    )
    assert response.status_code in [403, 404]  # Either forbidden or not found 