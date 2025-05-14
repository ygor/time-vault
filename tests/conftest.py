import pytest
import pytest_asyncio
import asyncio
from httpx import AsyncClient
from unittest.mock import patch

from main import app

@pytest_asyncio.fixture
async def client():
    """Create an async test client for FastAPI."""
    async with AsyncClient(app=app, base_url="http://test") as client:
        yield client

@pytest.fixture(autouse=True)
def mock_verification_code():
    """Automatically mock the verification code check to always return True."""
    with patch('app.core.security.verify_verification_code', return_value=True):
        yield

@pytest.fixture(autouse=True)
def reset_test_data():
    # Import here to avoid circular imports
    from app.api.v1.auth import USERS_DB
    from app.api.v1.vaults import VAULTS_DB, VAULT_SHARES
    
    # Clear all data before each test
    USERS_DB.clear()
    VAULTS_DB.clear()
    VAULT_SHARES.clear()
    yield 