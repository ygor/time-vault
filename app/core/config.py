import os
from typing import List
from dotenv import load_dotenv

# Load environment variables from .env file
load_dotenv()

class Settings:
    PROJECT_NAME: str = "TimeVault API"
    API_PREFIX: str = "/v1"
    DEBUG: bool = os.getenv("DEBUG", "False").lower() == "true"
    HOST: str = os.getenv("HOST", "0.0.0.0")
    PORT: int = int(os.getenv("PORT", "8000"))
    
    # CORS
    CORS_ORIGINS: List[str] = [
        "http://localhost:3000",
        "http://localhost:8000",
        "https://timevault.io",
    ]
    
    # Security
    JWT_SECRET: str = os.getenv("JWT_SECRET", "supersecret")
    JWT_ALGORITHM: str = "HS256"
    JWT_ACCESS_TOKEN_EXPIRE_MINUTES: int = 60 * 24  # 1 day
    
    # Drand
    DRAND_API_URL: str = os.getenv("DRAND_API_URL", "https://api.drand.sh")
    DRAND_CHAIN_HASH: str = os.getenv(
        "DRAND_CHAIN_HASH", 
        "8990e7a9aaed2ffed73dbd7092123d6f289930540d7651336225dc172e51b2ce"  # League of Entropy Mainnet
    )

settings = Settings() 