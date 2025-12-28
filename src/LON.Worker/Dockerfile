FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/LON.Worker/LON.Worker.csproj", "src/LON.Worker/"]
COPY ["src/LON.Infrastructure/LON.Infrastructure.csproj", "src/LON.Infrastructure/"]
COPY ["src/LON.Application/LON.Application.csproj", "src/LON.Application/"]
COPY ["src/LON.Domain/LON.Domain.csproj", "src/LON.Domain/"]
RUN dotnet restore "src/LON.Worker/LON.Worker.csproj"
COPY . .
WORKDIR "/src/src/LON.Worker"
RUN dotnet build "LON.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "LON.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LON.Worker.dll"]
