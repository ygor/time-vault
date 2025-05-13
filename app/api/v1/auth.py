from fastapi import APIRouter, Depends, HTTPException, status, Response
from fastapi.security import HTTPBearer
import uuid
from datetime import datetime

from app.models.user import WalletConnection, Token, User
from app.core.security import create_access_token, verify_wallet_signature, get_current_user

# This would normally be a database, but we'll use a simple dictionary for this example
USERS_DB = {}

router = APIRouter()


@router.post("/connect-wallet", response_model=Token)
async def connect_wallet(wallet_connection: WalletConnection):
    """
    Connect a blockchain wallet and authenticate the user.
    """
    # Check if signature is valid
    if not verify_wallet_signature(
        wallet_connection.address, 
        wallet_connection.signature, 
        "Connect to TimeVault"  # This would be a standard message
    ):
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid wallet signature"
        )
    
    # Find existing user by wallet address or create a new one
    user_id = None
    for uid, user in USERS_DB.items():
        if user.wallet_address.lower() == wallet_connection.address.lower():
            user_id = uid
            # Update username if provided
            if wallet_connection.username:
                USERS_DB[uid].username = wallet_connection.username
            break
    
    if not user_id:
        # Create new user
        now = datetime.utcnow()
        user_id = str(uuid.uuid4())
        USERS_DB[user_id] = User(
            id=user_id,
            wallet_address=wallet_connection.address,
            username=wallet_connection.username,
            created_at=now,
            updated_at=now
        )
    
    # Create access token
    access_token = create_access_token(
        user_id=user_id,
        wallet_address=wallet_connection.address
    )
    
    return Token(access_token=access_token)


@router.post("/disconnect")
async def disconnect_wallet(response: Response, current_user=Depends(get_current_user)):
    """
    Disconnect the currently connected wallet.
    
    In a real implementation, this might invalidate the token in a token blacklist.
    For simplicity, we'll just return a success message.
    """
    return {"message": "Wallet disconnected successfully"} 