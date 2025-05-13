from fastapi import APIRouter

from app.api.v1 import auth, users, vaults, messages, media, health

router = APIRouter()

# Include all API routers
router.include_router(auth.router, prefix="/auth", tags=["Authentication"])
router.include_router(users.router, prefix="/users", tags=["Authentication"])
router.include_router(vaults.router, prefix="/vaults", tags=["Vaults"])
router.include_router(messages.router, prefix="/vaults/{vault_id}/messages", tags=["Messages"])
router.include_router(media.router, prefix="/media", tags=["Media"])
router.include_router(health.router, prefix="/health", tags=["System"]) 