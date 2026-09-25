# Build context is the repo root: docker build -f Inventories.API/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first so this layer is cached until the project file changes
COPY Inventories.API/Inventories.API.csproj Inventories.API/
RUN dotnet restore Inventories.API/Inventories.API.csproj

COPY Inventories.API/ Inventories.API/
RUN dotnet publish Inventories.API/Inventories.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Inventories.API.dll"]
