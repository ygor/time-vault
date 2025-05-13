from fastapi import APIRouter
from datetime import datetime

router = APIRouter()

@router.get("")
async def health_check():
    """
    Check if the API is up and running.
    """
    return {
        "status": "ok",
        "version": "1.1.0",
        "timestamp": datetime.now().isoformat()
    } 