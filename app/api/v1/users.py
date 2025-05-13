from fastapi import APIRouter, Depends, HTTPException, status
from datetime import datetime

from app.models.user import User, UserUpdate
from app.core.security import get_current_user
from app.api.v1.auth import USERS_DB  # Reusing the mock database

router = APIRouter()


@router.get("/profile", response_model=User)
async def get_user_profile(current_user=Depends(get_current_user)):
    """
    Get the profile of the authenticated user.
    """
    if current_user.user_id not in USERS_DB:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="User not found"
        )
    
    return USERS_DB[current_user.user_id]


@router.put("/profile", response_model=User)
async def update_user_profile(
    user_update: UserUpdate,
    current_user=Depends(get_current_user)
):
    """
    Update the profile of the authenticated user.
    """
    if current_user.user_id not in USERS_DB:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="User not found"
        )
    
    # Check if username is already taken
    if user_update.username:
        for uid, user in USERS_DB.items():
            if (uid != current_user.user_id and 
                user.username and 
                user.username.lower() == user_update.username.lower()):
                raise HTTPException(
                    status_code=status.HTTP_409_CONFLICT,
                    detail="Username already taken"
                )
    
    # Update user
    user = USERS_DB[current_user.user_id]
    
    if user_update.username:
        user.username = user_update.username
    
    user.updated_at = datetime.utcnow()
    USERS_DB[current_user.user_id] = user
    
    return user 