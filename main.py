import uvicorn
from fastapi import FastAPI, Depends
from fastapi.middleware.cors import CORSMiddleware
import asyncio
import uuid
from datetime import datetime
from typing import Optional

from app.api.router import router
from app.core.config import settings
from app.core.users import fastapi_users
from app.models.auth import User
from app.schemas.user import UserCreate

app = FastAPI(
    title="Seldon TimeVault API",
    description="API for Seldon TimeVault - a time-locked message vault with secure storage and retrieval of time-locked messages.",
    version="1.1.0",
    docs_url="/docs",
    redoc_url="/redoc",
)

# Set up CORS
app.add_middleware(
    CORSMiddleware,
    allow_origins=settings.CORS_ORIGINS,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Include API router
app.include_router(router, prefix=settings.API_PREFIX)


@app.on_event("startup")
async def on_startup():
    """
    Create a default superuser on startup if it doesn't exist.
    """
    try:
        # Try to create a superuser on startup
        admin_email = "admin@timevault.io"
        
        # Check if superuser exists
        get_user_manager = fastapi_users.get_user_manager
        
        async for user_manager in get_user_manager():
            user = await user_manager.get_by_email(admin_email)
            
            if not user:
                # Create superuser if it doesn't exist
                user_dict = {
                    "email": admin_email,
                    "password": "adminpassword123",  # Change this in production!
                    "is_superuser": True,
                    "is_active": True,
                    "is_verified": True,
                    "username": "admin"
                }
                admin_user = await user_manager.create(
                    UserCreate(**user_dict)
                )
                print(f"Admin user created: {admin_user.email}")
            break
    except Exception as e:
        print(f"Error creating admin user: {e}")


if __name__ == "__main__":
    uvicorn.run(
        "main:app",
        host=settings.HOST,
        port=settings.PORT,
        reload=settings.DEBUG,
    ) 