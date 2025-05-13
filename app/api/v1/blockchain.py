from fastapi import APIRouter, Depends

from app.models.blockchain import BlockchainStatus, SyncStatus
from app.core.security import get_current_user
from app.services.drand_service import drand_service

router = APIRouter()


@router.get("/status", response_model=BlockchainStatus)
async def get_blockchain_status(current_user=Depends(get_current_user)):
    """
    Get the status of the drand blockchain.
    """
    try:
        # Get the latest round information from drand
        latest_round = await drand_service.get_latest_round()
        chain_info = await drand_service.get_chain_info()
        
        return BlockchainStatus(
            network="drand League of Entropy",
            current_block=int(latest_round.get("round", 0)),
            gas_price="0",  # drand doesn't have gas prices
            sync_status=SyncStatus.SYNCED
        )
    except Exception:
        return BlockchainStatus(
            network="drand League of Entropy",
            current_block=0,
            gas_price="0",
            sync_status=SyncStatus.ERROR
        ) 