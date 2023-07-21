# Use the official Microsoft .NET 3.1 SDK image as the base image
FROM mcr.microsoft.com/dotnet/sdk:3.1 as build-env

# Set the working directory in the container
WORKDIR /app

# Copy the test project files into the container
COPY . ./

# Build the test project
RUN dotnet build ./src/Downloader.Test/Downloader.Test.csproj --configuration Release

# Set the entrypoint
ENTRYPOINT ["dotnet", "test", "--logger:trx"]