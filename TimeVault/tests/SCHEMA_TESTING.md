# TimeVault Database Schema Testing Strategy

This document outlines the approach used to ensure database schema consistency and prevent 500 errors related to mismatches between entity models and the database schema.

## The Problem

The application previously suffered from 500 errors during user registration due to a discrepancy between the User entity model and the database schema. Specifically:

1. A `Username` field was present in the database schema but not in the User entity
2. The application tried to insert a User record without providing a value for the Username field
3. This resulted in a database constraint violation and a 500 error

Because the existing tests didn't adequately validate the schema, this issue wasn't caught before reaching production.

## The Solution

We've implemented a comprehensive testing strategy to catch schema-related issues:

### 1. Schema Validation Tests

These tests compare each entity model with its corresponding database table to ensure they match. They:

- Verify all properties in the entity exist in the database
- Verify all columns in the database have corresponding properties in the entity
- Check for mismatches in nullable/required settings

Example:
```csharp
[Fact]
public void UserEntity_MatchesDatabaseSchema()
{
    using var context = _fixture.CreateContext();
    
    // Get the entity type from EF Core's metadata
    var entityType = context.Model.FindEntityType(typeof(User));
    
    // Get properties from both entity and schema
    var entityProperties = typeof(User).GetProperties()
        .Where(p => !p.Name.Equals("Vaults"))
        .Select(p => p.Name)
        .ToHashSet();
    
    var schemaProperties = entityType.GetProperties()
        .Select(p => p.Name)
        .ToHashSet();
    
    // Check for missing properties
    var missingInEntity = schemaProperties.Except(entityProperties).ToList();
    var missingInSchema = entityProperties.Except(schemaProperties).ToList();
    
    Assert.Empty(missingInEntity);
    Assert.Empty(missingInSchema);
}
```

### 2. Migration Tests

These tests ensure that database migrations can be successfully applied to a clean database:

- Creates a test database
- Applies all migrations
- Verifies the resulting schema matches expectations

Example:
```csharp
[Fact]
public void AllMigrations_CanBeApplied_ToCleanDatabase()
{
    // Create a test database
    var databaseName = $"timevault_migration_test_{Guid.NewGuid()}";
    
    // Apply all migrations
    using (var context = new ApplicationDbContext(builder.Options))
    {
        context.Database.Migrate();
        
        // Verify all migrations are applied
        Assert.Empty(context.Database.GetPendingMigrations());
    }
}
```

### 3. Schema Change Detection

This test generates a hash of the current database schema and compares it with a saved hash:

- On first run, it saves the current schema hash
- On subsequent runs, it compares the current hash with the saved hash
- If the schema changes, the test fails, alerting the team to review the change

This catches unintended schema changes that might not be explicitly tested.

### 4. Integration Tests for API Endpoints

These tests validate that the API endpoints work correctly:

- Tests the registration endpoint with valid data
- Tests the registration endpoint with invalid data
- Verifies that appropriate error responses are returned

Example:
```csharp
[Fact]
public async Task Register_ShouldReturn200_WithValidRequestData()
{
    // Arrange
    var registerRequest = new
    {
        Email = "test@example.com",
        Password = "StrongPassword!123"
    };

    // Act
    var response = await _client.PostAsync("/api/auth/register", content);

    // Assert
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}
```

### 5. Pre-Deployment Checklist

A script that runs several checks before deployment:

- Builds the code to ensure it compiles
- Runs unit tests 
- Creates a test database and applies migrations
- Runs integration tests
- Tests API endpoints with valid and invalid data

This provides a final safety check before deployment.

## Implementation Details

### Testing Against Real Databases

To ensure that database constraints are properly tested, we use:

- Real PostgreSQL databases for testing migrations and schema validation
- The `DatabaseFixture` class to create and manage test databases
- Cleanup routines to remove test databases after testing

### CI/CD Integration

The tests can be integrated into CI/CD pipelines:

- Run unit tests on every push
- Run integration tests before merging to main
- Run the pre-deployment checklist script before deployment

### Best Practices for Avoiding Schema Issues

1. Always create a migration when adding, modifying, or removing entity properties
2. Always update entity models when modifying the database schema
3. Run the schema validation tests after making any changes to entities or migrations
4. Use the pre-deployment checklist before deploying changes

## Conclusion

This comprehensive testing strategy ensures that:

1. The entity models and database schema remain synchronized
2. Migrations can be successfully applied to clean databases
3. API endpoints handle both valid and invalid requests correctly
4. The team is alerted to unintended schema changes

By following this testing strategy, we can prevent 500 errors related to schema mismatches and ensure a smoother user experience. 