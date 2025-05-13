# TimeVault API Tests

This directory contains tests for the TimeVault API, focusing on how the drand randomness beacon is used for time-locking messages.

## Test Structure

- **test_e2e.py**: End-to-end tests covering the main user flows
- **test_drand_service.py**: Unit tests for the drand service implementation
- **test_time_locking.py**: Specific tests for the time-locking mechanism

## Running the Tests

To run all tests:

```bash
pytest
```

To run a specific test file:

```bash
pytest tests/test_time_locking.py
```

To run a specific test function:

```bash
pytest tests/test_drand_service.py::test_encrypt_and_decrypt_message
```

To run tests with verbose output:

```bash
pytest -v
```

## Mock Strategy

The tests use mocking to simulate the drand randomness beacon service. This allows us to test the time-locking functionality without waiting for actual time to pass or depending on the external drand service.

Key mocking points:
1. **compute_round_for_time**: To control which round is associated with a specific time
2. **is_round_available**: To simulate whether a round has occurred yet
3. **encrypt_message/decrypt_message**: To control the encryption/decryption process

## Test Scenarios

1. **Basic Message Time-Locking**: 
   - Create a time-locked message
   - Verify it's locked initially
   - Simulate time passing (drand round becoming available)
   - Verify it becomes unlocked

2. **Shared Vault Access**:
   - Shared time-locked messages are locked/unlocked for all users with access
   - Different permission levels work correctly

3. **Different Content Types**:
   - Text messages are properly time-locked
   - Image/video content is properly time-locked 