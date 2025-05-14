from fastapi import APIRouter, Depends, status

from app.core.users import fastapi_users, jwt_backend
from app.schemas.user import UserCreate, UserRead, UserUpdate

router = APIRouter()

# Include the pre-built authentication routers from FastAPI-Users
router.include_router(
    fastapi_users.get_auth_router(jwt_backend),
    prefix="/jwt",
    tags=["Authentication"],
)

# Registration
router.include_router(
    fastapi_users.get_register_router(UserRead, UserCreate),
    prefix="/register",
    tags=["Authentication"],
)

# User management
router.include_router(
    fastapi_users.get_users_router(UserRead, UserUpdate),
    prefix="/users",
    tags=["Users"],
)

# Reset password
router.include_router(
    fastapi_users.get_reset_password_router(),
    prefix="/reset-password",
    tags=["Authentication"],
)

# Verify (email verification)
router.include_router(
    fastapi_users.get_verify_router(UserRead),
    prefix="/verify",
    tags=["Authentication"],
) 