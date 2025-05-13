from datetime import datetime
from typing import List, Optional, Dict, Any
from enum import Enum
from pydantic import BaseModel, Field


class ContentType(str, Enum):
    TEXT = "text"
    IMAGE = "image"
    VIDEO = "video"


class ErrorResponse(BaseModel):
    code: str
    message: str
    details: Optional[Dict[str, Any]] = None


class PaginatedResponse(BaseModel):
    items: List[Any]
    total: int
    offset: int
    limit: int 