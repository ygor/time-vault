from fastapi import APIRouter, Depends

from app.core.users import current_active_user
from app.models.auth import User
from app.schemas.user import UserRead

router = APIRouter()


@router.get("/profile", response_model=UserRead)
async def get_user_profile(user: User = Depends(current_active_user)):
    """
    Get the profile of the authenticated user.
    """
    return user 