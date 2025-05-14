from fastapi import APIRouter, Depends, HTTPException, status, File, UploadFile, Form
from typing import Optional
import uuid
import os
from datetime import datetime

from app.models.vault import MediaUploadResponse
from app.models.auth import User
from app.core.users import current_active_user

router = APIRouter()

# This would be a service that handles media storage in a real application
UPLOAD_DIR = "/tmp/media"

@router.post("/upload", response_model=MediaUploadResponse)
async def upload_media(
    file: UploadFile = File(...),
    content_type: str = Form(...),
    user: User = Depends(current_active_user)
):
    """
    Upload media file (image or video)
    """
    # Validate content type
    if content_type not in ["image", "video"]:
        raise HTTPException(
            status_code=status.HTTP_415_UNSUPPORTED_MEDIA_TYPE,
            detail="Unsupported media type. Must be 'image' or 'video'"
        )

    # Check file size (50MB limit)
    content = await file.read()
    file_size = len(content)
    await file.seek(0)  # Reset file position after reading
    
    if file_size > 50 * 1024 * 1024:  # 50MB
        raise HTTPException(
            status_code=status.HTTP_413_REQUEST_ENTITY_TOO_LARGE,
            detail="File too large. Maximum size is 50MB"
        )
    
    # Generate a unique filename
    file_id = str(uuid.uuid4())
    file_extension = os.path.splitext(file.filename)[1]
    unique_filename = f"{file_id}{file_extension}"
    
    # Ensure upload directory exists
    os.makedirs(UPLOAD_DIR, exist_ok=True)
    
    # Save the file
    file_path = os.path.join(UPLOAD_DIR, unique_filename)
    with open(file_path, "wb") as f:
        f.write(content)
    
    # Generate a URL for accessing the media
    # In a real application, this would be a proper URL on a CDN or media server
    media_url = f"https://media.seldontimevault.io/content/{file_id}"
    
    return MediaUploadResponse(
        media_url=media_url,
        mime_type=file.content_type,
        size=file_size
    ) 