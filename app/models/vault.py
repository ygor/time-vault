from datetime import datetime
from typing import List, Optional, Dict
from pydantic import BaseModel, Field, field_validator, constr, model_validator
from enum import Enum

from app.models.common import ContentType


class MessageCountStats(BaseModel):
    total: int
    locked: int
    unlocked: int


class Message(BaseModel):
    id: str
    title: str
    content_type: ContentType
    unlock_time: datetime
    created_at: datetime
    created_by: str
    is_locked: bool
    content: Optional[str] = None
    media_url: Optional[str] = None
    encrypted_hash: Optional[str] = None
    transaction_hash: Optional[str] = None
    size: Optional[int] = None


class MessageCreation(BaseModel):
    title: constr(min_length=1, max_length=100)
    content_type: ContentType
    content: Optional[constr(max_length=10000)] = None
    unlock_time: datetime
    media_content_hash: Optional[str] = None

    @field_validator('content')
    def validate_content(cls, v, info):
        content_type = info.data.get('content_type')
        if content_type == ContentType.TEXT and not v:
            raise ValueError('Content is required for text messages')
        return v

    @field_validator('media_content_hash')
    def validate_media_hash(cls, v, info):
        content_type = info.data.get('content_type')
        if content_type in [ContentType.IMAGE, ContentType.VIDEO] and not v:
            raise ValueError('Media content hash is required for image or video messages')
        return v


class Vault(BaseModel):
    id: str
    name: str
    description: Optional[str] = None
    owner_id: str
    created_at: datetime
    updated_at: datetime
    messages: Optional[List[Message]] = None
    shared_with: Optional[List[str]] = None
    transaction_hash: Optional[str] = None
    message_count: Optional[MessageCountStats] = None


class VaultCreate(BaseModel):
    name: constr(max_length=100)
    description: Optional[constr(max_length=500)] = None


class VaultUpdate(BaseModel):
    name: Optional[constr(max_length=100)] = None
    description: Optional[constr(max_length=500)] = None


class SharePermission(str, Enum):
    READ = "read"
    READWRITE = "readwrite"


class ShareVaultRequest(BaseModel):
    identifier: Optional[str] = None
    username: Optional[constr(pattern=r"^[a-zA-Z0-9_.-]+$")] = None
    permissions: SharePermission = SharePermission.READ

    @model_validator(mode='after')
    def validate_share_target(self):
        if self.identifier is None and self.username is None:
            raise ValueError('Either identifier or username must be provided')
        return self


class MessageStatus(str, Enum):
    LOCKED = "locked"
    UNLOCKED = "unlocked"
    ALL = "all"


class MediaUploadResponse(BaseModel):
    media_url: str
    mime_type: str
    size: int 