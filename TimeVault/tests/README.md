# TimeVault Test Suite

This directory contains the test suite for the TimeVault application. It includes unit tests, integration tests, and schema validation tests to ensure the application's reliability.

## Test Structure

- **Unit Tests**: Test individual components in isolation.
- **Integration Tests**: Test the interaction between components.
- **Schema Validation Tests**: Ensure that the entity models match the database schema.
- **Migration Tests**: Verify that all migrations can be applied to a clean database.
- **Endpoint Tests**: Test the API endpoints to ensure they return expected responses.

## Preventing Schema Issues

The test suite includes several mechanisms to detect and prevent schema-related issues:

### 1. Schema Validation Tests

Located in `Infrastructure/SchemaValidationTests.cs`, these tests verify that:

- Each entity's properties match the database schema
- No properties are missing in entities or database tables
- No extra properties exist in entities that aren't in the database

If a schema validation test fails, it means there's a mismatch between the entity model and the database schema. This usually happens when:

- A property is added to an entity but no migration is created
- A property is removed from an entity but no migration is created
- A migration is created that changes the schema but the entity model isn't updated

### 2. Migration Tests

Located in `Infrastructure/MigrationTests.cs`, these tests verify that:

- All migrations can be applied to a clean database
- The migrations create the expected tables
- The final database schema has all the expected tables and columns

These tests can catch issues such as:

- Invalid migrations that would fail when deployed
- Missing migrations for entity changes
- Conflicting migrations

### 3. Schema Change Detection

Located in `Infrastructure/SchemaChangeDetectionTests.cs`, this test generates a hash of the current database schema and compares it to a saved hash. If the schema changes, the test will fail, alerting you to the change.

This helps catch unintended schema changes that might not be caught by the other tests.

### 4. Database Fixture

The `DatabaseFixture` class creates a real test PostgreSQL database for integration tests. This ensures that tests run against a real database rather than just the in-memory database, which might not enforce the same constraints.

## Pre-Deployment Checklist

A pre-deployment checklist script is available in `scripts/pre-deployment-check.sh`. This script runs a series of checks to verify that the application is ready for deployment:

1. Builds the code to ensure it compiles without errors
2. Runs unit tests to ensure they pass
3. Creates a clean test database and applies migrations to ensure they work
4. Runs integration tests to ensure they pass
5. Tests API endpoints to ensure they return expected responses

## Running the Tests

To run all tests:

```bash
dotnet test
```

To run only unit tests:

```bash
dotnet test --filter Category!=Integration
```

To run only integration tests:

```bash
dotnet test --filter Category=Integration
```

## Troubleshooting Schema Issues

If you encounter schema-related issues:

1. Check if the entity models match the database schema
2. Verify that all necessary migrations are created and applied
3. Run the database schema validation tests to identify mismatches
4. Use the pre-deployment checklist script to verify the application is ready for deployment

Remember to always create a migration when changing entity models, and always update entity models when changing the database schema. 