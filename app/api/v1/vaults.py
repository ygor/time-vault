from fastapi import APIRouter, Depends, HTTPException, status, Query, Path
from typing import List, Optional
import uuid
from datetime import datetime

from app.models.vault import Vault, VaultCreate, VaultUpdate, MessageCountStats, ShareVaultRequest, SharePermission
from app.models.user import User
from app.core.security import get_current_user
from app.api.v1.auth import USERS_DB

# Mock database for vaults
VAULTS_DB = {}
VAULT_SHARES = {}  # Maps vault_id -> {user_id: permission}

router = APIRouter()


@router.get("", response_model=List[Vault])
async def get_user_vaults(
    limit: int = Query(50, ge=1, le=100),
    offset: int = Query(0, ge=0),
    current_user=Depends(get_current_user)
):
    """
    Get all vaults owned by the authenticated user.
    """
    user_vaults = [
        vault for vault_id, vault in VAULTS_DB.items()
        if vault.owner_id == current_user.user_id
    ]
    
    # Apply pagination
    paginated_vaults = user_vaults[offset:offset+limit]
    
    return paginated_vaults


@router.post("", response_model=Vault, status_code=status.HTTP_201_CREATED)
async def create_vault(
    vault_data: VaultCreate,
    current_user=Depends(get_current_user)
):
    """
    Create a new vault for the authenticated user.
    """
    now = datetime.now()
    vault_id = str(uuid.uuid4())
    
    new_vault = Vault(
        id=vault_id,
        name=vault_data.name,
        description=vault_data.description,
        owner_id=current_user.user_id,
        created_at=now,
        updated_at=now,
        messages=[],
        shared_with=[],
        message_count=MessageCountStats(total=0, locked=0, unlocked=0)
    )
    
    VAULTS_DB[vault_id] = new_vault
    
    return new_vault


@router.get("/shared", response_model=List[Vault])
async def get_shared_vaults(
    limit: int = Query(50, ge=1, le=100),
    offset: int = Query(0, ge=0),
    current_user=Depends(get_current_user)
):
    """
    Get all vaults shared with the authenticated user.
    """
    shared_vaults = []
    
    for vault_id, shares in VAULT_SHARES.items():
        if current_user.user_id in shares and vault_id in VAULTS_DB:
            shared_vaults.append(VAULTS_DB[vault_id])
    
    # Apply pagination
    paginated_vaults = shared_vaults[offset:offset+limit]
    
    return paginated_vaults


@router.get("/{vault_id}", response_model=Vault)
async def get_vault_details(
    vault_id: str = Path(...),
    current_user=Depends(get_current_user)
):
    """
    Get details of a specific vault.
    """
    if vault_id not in VAULTS_DB:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="Vault not found"
        )
    
    vault = VAULTS_DB[vault_id]
    
    # Check if user has access to the vault
    if vault.owner_id != current_user.user_id and (
        vault_id not in VAULT_SHARES or 
        current_user.user_id not in VAULT_SHARES[vault_id]
    ):
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="You don't have access to this vault"
        )
    
    return vault


@router.put("/{vault_id}", response_model=Vault)
async def update_vault(
    vault_update: VaultUpdate,
    vault_id: str = Path(...),
    current_user=Depends(get_current_user)
):
    """
    Update a vault's details.
    """
    if vault_id not in VAULTS_DB:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="Vault not found"
        )
    
    vault = VAULTS_DB[vault_id]
    
    # Only the owner can update the vault
    if vault.owner_id != current_user.user_id:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="Only the owner can update the vault"
        )
    
    # Update fields if provided
    if vault_update.name is not None:
        vault.name = vault_update.name
        
    if vault_update.description is not None:
        vault.description = vault_update.description
    
    vault.updated_at = datetime.now()
    VAULTS_DB[vault_id] = vault
    
    return vault


@router.delete("/{vault_id}", status_code=status.HTTP_204_NO_CONTENT)
async def delete_vault(
    vault_id: str = Path(...),
    current_user=Depends(get_current_user)
):
    """
    Delete a vault and all its messages.
    """
    if vault_id not in VAULTS_DB:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="Vault not found"
        )
    
    vault = VAULTS_DB[vault_id]
    
    # Only the owner can delete the vault
    if vault.owner_id != current_user.user_id:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="Only the owner can delete the vault"
        )
    
    # Delete the vault
    del VAULTS_DB[vault_id]
    
    # Delete shares
    if vault_id in VAULT_SHARES:
        del VAULT_SHARES[vault_id]
    
    return None


@router.post("/{vault_id}/share")
async def share_vault(
    share_request: ShareVaultRequest,
    vault_id: str = Path(...),
    current_user=Depends(get_current_user)
):
    """
    Share a vault with another user.
    """
    if vault_id not in VAULTS_DB:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="Vault not found"
        )
    
    vault = VAULTS_DB[vault_id]
    
    # Only the owner can share the vault
    if vault.owner_id != current_user.user_id:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="Only the owner can share the vault"
        )
    
    # Find the user to share with
    target_user_id = None
    
    # Find by username if provided
    if share_request.username:
        for uid, user in USERS_DB.items():
            if user.username and user.username.lower() == share_request.username.lower():
                target_user_id = uid
                break
    
    # Find by identifier if provided
    elif share_request.identifier:
        for uid, user in USERS_DB.items():
            if user.identifier.lower() == share_request.identifier.lower():
                target_user_id = uid
                break
    
    if not target_user_id:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="User not found"
        )
    
    # Don't allow sharing with the owner
    if target_user_id == current_user.user_id:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Cannot share vault with yourself"
        )
    
    # Initialize vault shares if needed
    if vault_id not in VAULT_SHARES:
        VAULT_SHARES[vault_id] = {}
    
    # Add share permission
    VAULT_SHARES[vault_id][target_user_id] = share_request.permissions
    
    # Update shared_with list
    if target_user_id not in vault.shared_with:
        vault.shared_with.append(target_user_id)
        vault.updated_at = datetime.now()
        VAULTS_DB[vault_id] = vault
    
    return {"message": "Vault shared successfully"}


@router.get("/{vault_id}/share", response_model=List[User])
async def get_vault_shares(
    vault_id: str = Path(...),
    current_user=Depends(get_current_user)
):
    """
    Get all users the vault is shared with.
    """
    if vault_id not in VAULTS_DB:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="Vault not found"
        )
    
    vault = VAULTS_DB[vault_id]
    
    # Only the owner can view shares
    if vault.owner_id != current_user.user_id:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="Only the owner can view shares"
        )
    
    # Get users the vault is shared with
    shared_users = []
    
    if vault_id in VAULT_SHARES:
        for user_id in VAULT_SHARES[vault_id]:
            if user_id in USERS_DB:
                shared_users.append(USERS_DB[user_id])
    
    return shared_users


@router.delete("/{vault_id}/share/{user_id}", status_code=status.HTTP_204_NO_CONTENT)
async def remove_vault_sharing(
    vault_id: str = Path(...),
    user_id: str = Path(...),
    current_user=Depends(get_current_user)
):
    """
    Stop sharing a vault with a specific user.
    """
    if vault_id not in VAULTS_DB:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="Vault not found"
        )
    
    vault = VAULTS_DB[vault_id]
    
    # Only the owner can remove shares
    if vault.owner_id != current_user.user_id:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="Only the owner can remove shares"
        )
    
    # Check if the user exists
    if user_id not in USERS_DB:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="User not found"
        )
    
    # Check if the vault is shared with the user
    if vault_id not in VAULT_SHARES or user_id not in VAULT_SHARES[vault_id]:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="Vault is not shared with this user"
        )
    
    # Remove the share
    del VAULT_SHARES[vault_id][user_id]
    
    # Remove user from shared_with list
    if user_id in vault.shared_with:
        vault.shared_with.remove(user_id)
        vault.updated_at = datetime.now()
        VAULTS_DB[vault_id] = vault
    
    return None 