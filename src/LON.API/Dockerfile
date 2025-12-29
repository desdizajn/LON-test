FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/LON.API/LON.API.csproj", "src/LON.API/"]
COPY ["src/LON.Infrastructure/LON.Infrastructure.csproj", "src/LON.Infrastructure/"]
COPY ["src/LON.Application/LON.Application.csproj", "src/LON.Application/"]
COPY ["src/LON.Domain/LON.Domain.csproj", "src/LON.Domain/"]
RUN dotnet restore "src/LON.API/LON.API.csproj"
COPY . .
WORKDIR "/src/src/LON.API"
RUN dotnet build "LON.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "LON.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Копирај Knowledge Base податоци за seed
COPY kb/ /app/kb/
ENTRYPOINT ["dotnet", "LON.API.dll"]
