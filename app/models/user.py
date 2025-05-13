from datetime import datetime
from typing import Optional
from pydantic import BaseModel, Field, validator, constr
import re


class WalletConnection(BaseModel):
    address: constr(pattern=r"^0x[a-fA-F0-9]{40}$")
    signature: str
    username: Optional[constr(min_length=3, max_length=30, pattern=r"^[a-zA-Z0-9_.-]+$")] = None


class User(BaseModel):
    id: str
    username: Optional[str] = None
    wallet_address: str
    created_at: datetime
    updated_at: datetime


class UserCreate(BaseModel):
    username: Optional[str] = None
    wallet_address: str

    @validator("username")
    def validate_username(cls, v):
        if v is not None:
            if len(v) < 3 or len(v) > 30:
                raise ValueError("Username must be between 3 and 30 characters")
            if not re.match(r"^[a-zA-Z0-9_.-]+$", v):
                raise ValueError("Username can only contain letters, numbers, underscores, dots, and hyphens")
        return v


class UserUpdate(BaseModel):
    username: Optional[str] = None

    @validator("username")
    def validate_username(cls, v):
        if v is not None:
            if len(v) < 3 or len(v) > 30:
                raise ValueError("Username must be between 3 and 30 characters")
            if not re.match(r"^[a-zA-Z0-9_.-]+$", v):
                raise ValueError("Username can only contain letters, numbers, underscores, dots, and hyphens")
        return v


class Token(BaseModel):
    access_token: str
    token_type: str = "bearer"


class TokenPayload(BaseModel):
    user_id: str
    wallet_address: str
    exp: Optional[int] = None 