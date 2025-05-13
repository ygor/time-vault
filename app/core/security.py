from datetime import datetime, timedelta
from typing import Any, Dict, Optional

from jose import jwt
from fastapi import Depends, HTTPException, status
from fastapi.security import HTTPBearer, HTTPAuthorizationCredentials

from app.core.config import settings
from app.models.user import TokenPayload

oauth2_scheme = HTTPBearer()


def create_access_token(
    user_id: str, identifier: str, expires_delta: Optional[timedelta] = None
) -> str:
    """
    Create a JWT access token for a user.
    """
    to_encode = {
        "user_id": user_id,
        "identifier": identifier
    }

    if expires_delta:
        expire = datetime.now() + expires_delta
    else:
        expire = datetime.now() + timedelta(
            minutes=settings.JWT_ACCESS_TOKEN_EXPIRE_MINUTES
        )
    to_encode.update({"exp": expire.timestamp()})

    encoded_jwt = jwt.encode(
        to_encode, settings.JWT_SECRET, algorithm=settings.JWT_ALGORITHM
    )
    return encoded_jwt


def verify_verification_code(identifier: str, verification_code: str) -> bool:
    """
    Verify that the verification code provided is valid for the given identifier.
    
    In a real implementation, this would check against a database of sent codes
    or integrate with an authentication service.
    
    For simplicity, we're just returning True in this example.
    """
    # TODO: Implement proper verification code validation
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
            identifier=payload.get("identifier"),
            exp=payload.get("exp")
        )
        
        # Check if token has expired
        if token_data.exp and datetime.now().timestamp() > token_data.exp:
            raise HTTPException(
                status_code=status.HTTP_401_UNAUTHORIZED,
                detail="Token expired",
                headers={"WWW-Authenticate": "Bearer"},
            )
            
        return token_data
    except (jwt.JWTError, ValueError) as e:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Could not validate credentials",
            headers={"WWW-Authenticate": "Bearer"},
        ) 