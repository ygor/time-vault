FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Copy published files
COPY ./publish .

# Ensure the PostgreSQL schema file is in the correct location
COPY ./TimeVault/src/TimeVault.Infrastructure/Data/PostgresSchema.sql /app/PostgresSchema.sql

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80

# Run the application
ENTRYPOINT ["dotnet", "TimeVault.Api.dll"] 