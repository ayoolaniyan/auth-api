# Build context is the repo root: docker build -f IdentityServer/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first so this layer is cached until the project file changes
COPY IdentityServer/IdentityServer.csproj IdentityServer/
RUN dotnet restore IdentityServer/IdentityServer.csproj

COPY IdentityServer/ IdentityServer/
RUN dotnet publish IdentityServer/IdentityServer.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "IdentityServer.dll"]
