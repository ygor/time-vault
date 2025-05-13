from datetime import datetime, timedelta
from typing import Any, Dict, Optional

from jose import jwt
from fastapi import Depends, HTTPException, status
from fastapi.security import HTTPBearer, HTTPAuthorizationCredentials

from app.core.config import settings
from app.models.user import TokenPayload

oauth2_scheme = HTTPBearer()


def create_access_token(
    user_id: str, wallet_address: str, expires_delta: Optional[timedelta] = None
) -> str:
    """
    Create a JWT access token for a user.
    """
    to_encode = {
        "user_id": user_id,
        "wallet_address": wallet_address
    }

    if expires_delta:
        expire = datetime.utcnow() + expires_delta
    else:
        expire = datetime.utcnow() + timedelta(
            minutes=settings.JWT_ACCESS_TOKEN_EXPIRE_MINUTES
        )
    to_encode.update({"exp": expire.timestamp()})

    encoded_jwt = jwt.encode(
        to_encode, settings.JWT_SECRET, algorithm=settings.JWT_ALGORITHM
    )
    return encoded_jwt


def verify_wallet_signature(address: str, signature: str, message: str) -> bool:
    """
    Verify that a signature was produced by the owner of a wallet address.
    
    In a real implementation, this would use web3.py or a similar library to
    recover the address from the signature and compare it to the provided address.
    
    For simplicity, we're just returning True in this example.
    """
    # TODO: Implement proper signature verification using web3.py
    return True


async def get_current_user(
    credentials: HTTPAuthorizationCredentials = Depends(oauth2_scheme),
) -> TokenPayload:
    """
    Dependency to get the current authenticated user based on JWT token.
    """
    try:
        token = credentials.credentials
        payload = jwt.decode(
            token, settings.JWT_SECRET, algorithms=[settings.JWT_ALGORITHM]
        )
        
        token_data = TokenPayload(
            user_id=payload.get("user_id"),
            wallet_address=payload.get("wallet_address"),
            exp=payload.get("exp")
        )
        
        # Check if token has expired
        if token_data.exp and datetime.utcnow().timestamp() > token_data.exp:
            raise HTTPException(
                status_code=status.HTTP_401_UNAUTHORIZED,
                detail="Token expired",
                headers={"WWW-Authenticate": "Bearer"},
            )
            
        return token_data
    except (jwt.JWTError, ValueError):
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Could not validate credentials",
            headers={"WWW-Authenticate": "Bearer"},
        ) 