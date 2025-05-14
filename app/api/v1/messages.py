from fastapi import APIRouter, Depends, HTTPException, status, Path, Query
from typing import List, Optional
import uuid
from datetime import datetime, timezone

from app.models.vault import Message, MessageCreation, MessageStatus, ContentType
from app.models.common import ErrorResponse
from app.models.auth import User
from app.core.users import current_active_user
from app.api.v1.vaults import VAULTS_DB, VAULT_SHARES
from app.services.drand_service import drand_service

router = APIRouter()


@router.get("", response_model=List[Message])
async def get_vault_messages(
    vault_id: str = Path(...),
    limit: int = Query(50, ge=1, le=100),
    offset: int = Query(0, ge=0),
    status: MessageStatus = Query(MessageStatus.ALL),
    user: User = Depends(current_active_user)
):
    """
    Get all messages in a vault with optional filtering.
    """
    # Check if vault exists
    if vault_id not in VAULTS_DB:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="Vault not found"
        )
    
    vault = VAULTS_DB[vault_id]
    
    # Check if user has access to the vault
    if vault.owner_id != str(user.id) and (
        vault_id not in VAULT_SHARES or 
        str(user.id) not in VAULT_SHARES[vault_id]
    ):
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="You don't have access to this vault"
        )
    
    # Update lock status for all messages before filtering
    messages = vault.messages.copy() if vault.messages else []
    now = datetime.now(timezone.utc)
    
    for message in messages:
        message.is_locked = message.unlock_time > now
    
    # Apply status filter
    if status == MessageStatus.LOCKED:
        filtered_messages = [m for m in messages if m.is_locked]
    elif status == MessageStatus.UNLOCKED:
        filtered_messages = [m for m in messages if not m.is_locked]
    else:  # ALL
        filtered_messages = messages
    
    # Apply pagination
    paginated_messages = filtered_messages[offset:offset+limit]
    
    return paginated_messages


@router.post("", response_model=Message, status_code=status.HTTP_201_CREATED)
async def add_vault_message(
    message_data: MessageCreation,
    vault_id: str = Path(...),
    user: User = Depends(current_active_user)
):
    """
    Add a new time-locked message to a vault.
    """
    # Check if vault exists
    if vault_id not in VAULTS_DB:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="Vault not found"
        )
    
    vault = VAULTS_DB[vault_id]
    
    # Check if user has permission to add messages
    has_write_permission = False
    
    if vault.owner_id == str(user.id):
        has_write_permission = True
    elif (vault_id in VAULT_SHARES and 
          str(user.id) in VAULT_SHARES[vault_id] and 
          VAULT_SHARES[vault_id][str(user.id)] == "readwrite"):
        has_write_permission = True
    
    if not has_write_permission:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="You don't have permission to add messages to this vault"
        )
    
    # Ensure unlock time is in the future
    now = datetime.now(timezone.utc)
    if message_data.unlock_time <= now:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Unlock time must be in the future"
        )
    
    # Calculate the drand round for the unlock time
    target_round = await drand_service.compute_round_for_time(message_data.unlock_time)
    
    # Create the message
    message_id = str(uuid.uuid4())
    
    new_message = Message(
        id=message_id,
        title=message_data.title,
        content_type=message_data.content_type,
        unlock_time=message_data.unlock_time,
        created_at=now,
        created_by=str(user.id),
        is_locked=True
    )
    
    # Handle message content based on content type
    if message_data.content_type == ContentType.TEXT:
        # Encrypt the message using drand for time-locking
        encrypted_content, verification_hash = await drand_service.encrypt_message(
            message_data.content, target_round
        )
        
        # In a real implementation, you'd store the encrypted content somewhere
        # and only keep a reference/hash here
        
        # For this example, we're storing a "fake" encrypted hash only
        new_message.encrypted_hash = verification_hash
        new_message.size = len(message_data.content)
        
        # In a real implementation, you would not store the content directly
        # This is just for the example
        new_message.content = None  # Set to None while locked
    
    else:  # IMAGE or VIDEO
        # For media, just store the content hash and set up metadata
        new_message.media_url = None  # Would be set to the URL when unlocked
        new_message.encrypted_hash = message_data.media_content_hash
    
    # Update the vault with the new message
    if not vault.messages:
        vault.messages = []
    
    vault.messages.append(new_message)
    
    # Update message count stats
    if not vault.message_count:
        vault.message_count = {
            "total": 0,
            "locked": 0,
            "unlocked": 0
        }
    
    vault.message_count.total += 1
    vault.message_count.locked += 1
    
    vault.updated_at = now
    VAULTS_DB[vault_id] = vault
    
    return new_message


@router.get("/{message_id}", response_model=Message)
async def get_message_details(
    message_id: str = Path(...),
    vault_id: str = Path(...),
    user: User = Depends(current_active_user)
):
    """
    Get details of a specific message, including content if unlocked.
    """
    # Check if vault exists
    if vault_id not in VAULTS_DB:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="Vault not found"
        )
    
    vault = VAULTS_DB[vault_id]
    
    # Check if user has access to the vault
    if vault.owner_id != str(user.id) and (
        vault_id not in VAULT_SHARES or 
        str(user.id) not in VAULT_SHARES[vault_id]
    ):
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="You don't have access to this vault"
        )
    
    # Find the message
    message = None
    if vault.messages:
        for m in vault.messages:
            if m.id == message_id:
                message = m
                break
    
    if not message:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="Message not found"
        )
    
    # Check if the message is unlocked or needs to be unlocked
    now = datetime.now(timezone.utc)
    
    if message.unlock_time <= now and message.is_locked:
        # First, check if the drand round for this unlock time is available
        target_round = await drand_service.compute_round_for_time(message.unlock_time)
        round_available = await drand_service.is_round_available(target_round)
        
        if round_available:
            # Message should be unlocked
            
            # In a real implementation, this would decrypt from secure storage
            if message.content_type == ContentType.TEXT:
                # For this example, we'll simulate decryption
                # Assume we stored the encrypted content in a secure place with the uuid as the key
                # In a real implementation, you'd retrieve and decrypt it here
                
                # Fake placeholder content
                fake_content = f"This is the decrypted content for message {message_id}"
                message.content = fake_content
            
            elif message.content_type in [ContentType.IMAGE, ContentType.VIDEO]:
                # For media content, set the URL that would point to the decrypted media
                message.media_url = f"https://example.com/media/{message.encrypted_hash}"
            
            message.is_locked = False
            
            # Update message in vault
            for i, m in enumerate(vault.messages):
                if m.id == message_id:
                    vault.messages[i] = message
                    break
            
            # Update message count stats
            vault.message_count.locked -= 1
            vault.message_count.unlocked += 1
            
            VAULTS_DB[vault_id] = vault
    
    return message


@router.delete("/{message_id}", status_code=status.HTTP_204_NO_CONTENT)
async def delete_message(
    message_id: str = Path(...),
    vault_id: str = Path(...),
    user: User = Depends(current_active_user)
):
    """
    Delete a message from a vault.
    """
    # Check if vault exists
    if vault_id not in VAULTS_DB:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="Vault not found"
        )
    
    vault = VAULTS_DB[vault_id]
    
    # Check if user is the vault owner or message creator
    has_delete_permission = vault.owner_id == str(user.id)
    
    if not has_delete_permission:
        # Check if the user is the message creator
        if vault.messages:
            for m in vault.messages:
                if m.id == message_id and m.created_by == str(user.id):
                    has_delete_permission = True
                    break
    
    if not has_delete_permission:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="You don't have permission to delete this message"
        )
    
    # Find the message
    message_index = None
    message = None
    
    if vault.messages:
        for i, m in enumerate(vault.messages):
            if m.id == message_id:
                message_index = i
                message = m
                break
    
    if message_index is None:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="Message not found"
        )
    
    # Delete the message
    vault.messages.pop(message_index)
    
    # Update message count stats
    vault.message_count.total -= 1
    if message.is_locked:
        vault.message_count.locked -= 1
    else:
        vault.message_count.unlocked -= 1
    
    vault.updated_at = datetime.now(timezone.utc)
    VAULTS_DB[vault_id] = vault
    
    return None 