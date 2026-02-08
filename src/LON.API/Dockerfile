# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["LON.sln", "./"]
COPY ["src/LON.API/LON.API.csproj", "src/LON.API/"]
COPY ["src/LON.Application/LON.Application.csproj", "src/LON.Application/"]
COPY ["src/LON.Domain/LON.Domain.csproj", "src/LON.Domain/"]
COPY ["src/LON.Infrastructure/LON.Infrastructure.csproj", "src/LON.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "src/LON.API/LON.API.csproj"

# Copy everything else
COPY . .

# Build and publish
WORKDIR "/src/src/LON.API"
RUN dotnet publish "LON.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install sqlcmd for healthcheck
RUN apt-get update && apt-get install -y curl gnupg && \
    curl https://packages.microsoft.com/keys/microsoft.asc | apt-key add - && \
    curl https://packages.microsoft.com/config/debian/11/prod.list > /etc/apt/sources.list.d/mssql-release.list && \
    apt-get update && \
    ACCEPT_EULA=Y apt-get install -y mssql-tools18 unixodbc-dev && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Copy published files
COPY --from=build /app/publish .

# Copy Knowledge Base data files
COPY --from=build /src/kb/processed /app/kb/processed

# Expose port
EXPOSE 5000

# Set entry point
ENTRYPOINT ["dotnet", "LON.API.dll"]