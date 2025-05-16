#!/bin/bash

echo "===================================================="
echo "        Running TimeVault API on port 5010"
echo "===================================================="

echo "Starting TimeVault API..."
cd TimeVault && dotnet run --project src/TimeVault.Api --urls="http://localhost:5010" 