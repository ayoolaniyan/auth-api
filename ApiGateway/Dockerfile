# Build context is the repo root: docker build -f ApiGateway/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first so this layer is cached until the project file changes
COPY ApiGateway/ApiGateway.csproj ApiGateway/
RUN dotnet restore ApiGateway/ApiGateway.csproj

COPY ApiGateway/ ApiGateway/
RUN dotnet publish ApiGateway/ApiGateway.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ApiGateway.dll"]
