from fastapi import APIRouter, Depends, HTTPException, status, Response
from fastapi.security import HTTPBearer
import uuid
from datetime import datetime, timedelta

from app.models.user import AuthenticationRequest, AuthResponse, Token, User
from app.core.security import create_access_token, verify_verification_code, get_current_user

# This would normally be a database, but we'll use a simple dictionary for this example
USERS_DB = {}

router = APIRouter()


@router.post("/authenticate", response_model=AuthResponse)
async def authenticate(auth_request: AuthenticationRequest):
    """
    Authenticate a user with the provided credentials.
    """
    # Check if verification code is valid
    if not verify_verification_code(
        auth_request.identifier, 
        auth_request.verification_code
    ):
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid verification code"
        )
    
    # Find existing user by identifier or create a new one
    user_id = None
    for uid, user in USERS_DB.items():
        if user.identifier.lower() == auth_request.identifier.lower():
            user_id = uid
            # Update username if provided
            if auth_request.username:
                USERS_DB[uid].username = auth_request.username
            break
    
    if not user_id:
        # Create new user
        now = datetime.now()
        user_id = str(uuid.uuid4())
        USERS_DB[user_id] = User(
            id=user_id,
            identifier=auth_request.identifier,
            username=auth_request.username,
            created_at=now,
            updated_at=now
        )
    
    # Create access token
    expires_delta = timedelta(minutes=60 * 24)  # 1 day
    access_token = create_access_token(
        user_id=user_id,
        identifier=auth_request.identifier,
        expires_delta=expires_delta
    )
    
    # Calculate expiration time
    expires_at = datetime.now() + expires_delta
    
    return AuthResponse(
        user=USERS_DB[user_id],
        token=access_token,
        expires_at=expires_at
    )


@router.post("/signout")
async def sign_out(response: Response, current_user=Depends(get_current_user)):
    """
    Sign out the currently authenticated user.
    
    In a real implementation, this might invalidate the token in a token blacklist.
    For simplicity, we'll just return a success message.
    """
    return {"message": "Successfully signed out"} 