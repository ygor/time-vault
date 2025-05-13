from enum import Enum
from pydantic import BaseModel
from typing import Optional


class SyncStatus(str, Enum):
    SYNCED = "synced"
    SYNCING = "syncing"
    ERROR = "error"


class BlockchainStatus(BaseModel):
    network: str
    current_block: int
    gas_price: Optional[str] = None
    sync_status: SyncStatus 