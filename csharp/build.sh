#!/bin/bash

# Build and test the CarePet C# application

set -e

echo "Building CarePet C# application..."

# Restore dependencies and build the project
dotnet restore
dotnet build

echo "Build completed successfully!"
echo ""

# Check if Docker is available and running
if command -v docker &> /dev/null && docker info &> /dev/null; then
    echo "Docker is available. You can now:"
    echo "1. Start ScyllaDB cluster: docker-compose up -d"
    echo "2. Wait for cluster to be ready (about 2 minutes)"
    echo "3. Check cluster status: docker exec -it carepet-scylla1 nodetool status"
    echo "4. Get node IP: NODE1=\$(docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' carepet-scylla1)"
    echo "5. Run migration: dotnet run -- migrate --hosts \$NODE1"
    echo "6. Start sensor simulation: dotnet run -- sensor --hosts \$NODE1"
    echo "7. Start web server: dotnet run -- server --port 8000"
else
    echo "Docker not available. Please install Docker to run ScyllaDB cluster."
fi

echo ""
echo "Available commands:"
echo "  dotnet run -- migrate [options]    # Create keyspace and tables"
echo "  dotnet run -- sensor [options]     # Generate sensor data"
echo "  dotnet run -- server [options]     # Start REST API server"
echo ""
echo "For help with any command, run: dotnet run -- <command> --help"
echo "Or run without arguments to see usage: dotnet run"
