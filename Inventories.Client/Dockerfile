# Build context is the repo root: docker build -f Inventories.Client/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first so this layer is cached until the project file changes
COPY Inventories.Client/Inventories.Client.csproj Inventories.Client/
RUN dotnet restore Inventories.Client/Inventories.Client.csproj

COPY Inventories.Client/ Inventories.Client/
RUN dotnet publish Inventories.Client/Inventories.Client.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Inventories.Client.dll"]
