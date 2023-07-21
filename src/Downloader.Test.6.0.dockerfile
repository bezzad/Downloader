# Use the official Microsoft .NET 6 SDK image as the base image
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build

# Set the working directory in the container
WORKDIR /app

# Copy the test project files into the container
COPY . ./

# Build the project
RUN dotnet restore
RUN dotnet build --configuration Release --no-restore -verbosity:m -f net6.0

# Set the entrypoint
ENTRYPOINT dotnet test --logger "console;verbosity=detailed"