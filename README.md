# TimeVault API

A FastAPI-based service that leverages the [drand](https://drand.love/) beacon API to create a time-locked message vault. Messages can be encrypted and will only be accessible after a specific time has passed, verified by the drand randomness beacon.

## Features

- Comprehensive user authentication with FastAPI-Users
- Create and manage vaults of time-locked messages
- Share vaults with other users
- Support for text, image, and video content
- Time-locking based on drand beacon rounds
- API endpoints following OpenAPI specification
- Database integration with SQLAlchemy and Alembic

## Getting Started

### Prerequisites

- Python 3.8 or higher
- pip (Python package manager)

### Installation

1. Clone the repository:
```bash
git clone https://github.com/yourusername/time-vault.git
cd time-vault
```

2. Set up a virtual environment:
```bash
python -m venv venv
source venv/bin/activate  # On Windows: venv\Scripts\activate
```

3. Install dependencies:
```bash
pip install -r requirements.txt
```

4. Run the database migrations:
```bash
alembic upgrade head
```

5. Create a `.env` file in the root directory with the following content:
```
DEBUG=True
HOST=0.0.0.0
PORT=8000
JWT_SECRET=your_secure_jwt_secret
DRAND_API_URL=https://api.drand.sh
DRAND_CHAIN_HASH=8990e7a9aaed2ffed73dbd7092123d6f289930540d7651336225dc172e51b2ce
```

### Running the API

```bash
python main.py
```

The API will be available at http://localhost:8000/docs (Swagger UI) and http://localhost:8000/redoc (ReDoc).

### Default Credentials

A default admin user is created when the application starts:

- Email: admin@timevault.io
- Password: adminpassword123

**Important**: Change these credentials in production!

## How TimeVault Works

1. **Time-Locking with drand**: Messages are time-locked using the drand randomness beacon, which produces a new random value at regular intervals (approximately every 30 seconds for the League of Entropy mainnet).

2. **Message Encryption**: When a user creates a new message, the API calculates which future drand round will occur after the specified unlock time and uses it to encrypt the message.

3. **Time Verification**: When a user tries to access a message, the API checks if the target drand round has occurred. If it has, the message is decrypted and made available.

## API Endpoints

The API follows the OpenAPI specification. For detailed documentation, refer to the Swagger UI at http://localhost:8000/docs.

### Main Endpoints:

- **Authentication**: `/v1/auth/jwt/login`, `/v1/auth/jwt/logout`
- **User Registration**: `/v1/auth/register`
- **User Profile**: `/v1/users/profile`
- **Vaults**: `/v1/vaults`
- **Messages**: `/v1/vaults/{vault_id}/messages`
- **Health**: `/v1/health`

## Authentication System

TimeVault uses FastAPI-Users for authentication, which provides:

- Complete JWT-based authentication
- User registration and email verification
- Password reset functionality
- Role-based access control with superuser support
- Database integration with SQLAlchemy

## Limitations and Future Improvements

- The current implementation uses SQLite for database storage. For production, consider using PostgreSQL or another robust database.
- Media handling is simplified; a production version would integrate with a proper storage solution.
- Message encryption is simplified; a production version would use more sophisticated cryptography.

## License

This project is licensed under the MIT License - see the LICENSE file for details. 