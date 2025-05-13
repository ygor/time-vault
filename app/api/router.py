from fastapi import APIRouter

from app.api.v1 import auth, users, vaults, messages, blockchain

router = APIRouter()

# Include all API routers
router.include_router(auth.router, prefix="/auth", tags=["Authentication"])
router.include_router(users.router, prefix="/users", tags=["Authentication"])
router.include_router(vaults.router, prefix="/vaults", tags=["Vaults"])
router.include_router(messages.router, prefix="/vaults/{vault_id}/messages", tags=["Messages"])
router.include_router(blockchain.router, prefix="/blockchain", tags=["Blockchain"]) 