# AuthServices -- IdentityServer Microservices Authentication

![.NET](https://img.shields.io/badge/.NET-8-blue)
![OAuth2](https://img.shields.io/badge/Security-OAuth2-green)
![OpenID](https://img.shields.io/badge/Auth-OpenID%20Connect-orange)
![Architecture](https://img.shields.io/badge/Architecture-Microservices-purple)

A **.NET microservices authentication reference architecture**
demonstrating how to secure distributed services using **IdentityServer,
OAuth2, OpenID Connect, and an API Gateway**.

This project illustrates how modern applications implement **centralized
authentication, token‑based authorization, and secure service-to-service
communication** using a **Zero‑Trust security model**.

------------------------------------------------------------------------

# Overview

The system is composed of:

-   **IdentityServer** -- Central authentication and token issuer -- Duende IdentityServer
-   **API Gateway** -- Single secure entry point for APIs -- Ocelot
-   **Inventories API** -- Protected backend service -- .NET 8
-   **Client Web App** -- Authenticated user interface -- .NET 8

The architecture demonstrates:

-   OAuth2 Authorization Code Flow
-   API Gateway security
-   Token-based API protection
-   Zero‑Trust microservice communication

------------------------------------------------------------------------

# Microservices Authentication Architecture

``` mermaid
flowchart LR
User[User Browser]
Client[Client Application]
Gateway[API Gateway]
Inventory[Inventories API]
Identity[IdentityServer]

User --> Client
Client --> Gateway
Gateway --> Inventory
Client --> Identity
Inventory --> Identity
Identity --> Client
Identity --> Gateway
```

**Explanation**

1.  User accesses the client application.
2.  Client redirects the user to **IdentityServer** for authentication.
3.  IdentityServer issues an **Access Token**.
4.  Client calls the **API Gateway** with the token.
5.  Gateway validates the token.
6.  Request is forwarded to the protected **Inventories API**.

------------------------------------------------------------------------

# OAuth2 Authorization Code Flow

``` mermaid
sequenceDiagram
participant User
participant ClientApp
participant IdentityServer
participant ApiGateway
participant InventoryAPI

User->>ClientApp: Access application
ClientApp->>IdentityServer: Redirect to login
User->>IdentityServer: Authenticate
IdentityServer->>ClientApp: Authorization Code
ClientApp->>IdentityServer: Exchange Code for Token
IdentityServer->>ClientApp: Access Token + ID Token
ClientApp->>ApiGateway: Request with Access Token
ApiGateway->>IdentityServer: Validate Token
ApiGateway->>InventoryAPI: Forward request
InventoryAPI->>ApiGateway: API Response
ApiGateway->>ClientApp: Response
```

------------------------------------------------------------------------

# Authentication Sequence Diagram

``` mermaid
sequenceDiagram
participant Browser
participant Client
participant IdentityServer
participant Gateway
participant API

Browser->>Client: Open application
Client->>IdentityServer: Redirect to login
Browser->>IdentityServer: Credentials
IdentityServer->>Browser: Authentication success
IdentityServer->>Client: Authorization Code
Client->>IdentityServer: Token Request
IdentityServer->>Client: Access Token
Client->>Gateway: API Request + Token
Gateway->>IdentityServer: Validate token
Gateway->>API: Forward request
API->>Gateway: Data
Gateway->>Client: Response
Client->>Browser: Display result
```

------------------------------------------------------------------------

# Token Lifecycle Diagram

``` mermaid
flowchart LR
Login[User Login]
AuthCode[Authorization Code]
AccessToken[Access Token]
RefreshToken[Refresh Token]
API[Protected API]
Expire[Token Expiration]
Renew[Token Refresh]

Login --> AuthCode
AuthCode --> AccessToken
AuthCode --> RefreshToken
AccessToken --> API
AccessToken --> Expire
Expire --> Renew
Renew --> AccessToken
```

**Lifecycle Summary**

1.  User authenticates via IdentityServer.
2.  Client receives an **Authorization Code**.
3.  Code exchanged for **Access Token** and **Refresh Token**.
4.  Access Token is used for API calls.
5.  When expired, Refresh Token obtains a new Access Token.

------------------------------------------------------------------------

# Zero‑Trust Microservices Security Model

``` mermaid
flowchart TD
User
Client
Gateway
Auth[IdentityServer]
Service1[Inventories API]
Service2[Future Microservice]

User --> Client
Client --> Auth
Client --> Gateway
Gateway --> Auth
Gateway --> Service1
Gateway --> Service2
Service1 --> Auth
Service2 --> Auth
```

**Zero‑Trust Principles Applied**

-   Every request must be authenticated
-   Services **never trust internal traffic**
-   Tokens must be validated at each boundary
-   Gateway enforces security policies

------------------------------------------------------------------------

# API Gateway + IdentityServer Security Model

``` mermaid
flowchart LR
User
ClientApp
Gateway
IdentityServer
Service

User --> ClientApp
ClientApp --> IdentityServer
IdentityServer --> ClientApp
ClientApp --> Gateway
Gateway --> IdentityServer
Gateway --> Service
```

**Responsibilities**

| Component      | Responsibility                  |
| --------------- | -------------------------------- |
| IdentityServer | Authentication, token issuance  |
| API Gateway    | Token validation, routing       |
| Services       | Business logic                  |
| Client         | User interaction                |

------------------------------------------------------------------------

# Project Structure

    AuthServices
    │
    ├── IdentityServer
    │   ├── Data/Migrations
    │   ├── Pages
    │   ├── Config.cs
    │   ├── SeedData.cs
    │   └── Program.cs
    │
    ├── Inventories.API
    │   ├── Controllers
    │   ├── Services
    │   └── Models
    │
    ├── ApiGateway
    │
    ├── Inventories.Client
    │
    ├── k8s
    │   ├── charts/auth-services   (Helm chart)
    │   ├── kind-config.yaml
    │   ├── deploy.sh
    │   └── teardown.sh
    │
    ├── .env-example
    ├── .dockerignore
    ├── docker-compose.yml
    └── AuthServices.sln

------------------------------------------------------------------------

# Running the Project

Clone the repository

    git clone https://github.com/yourusername/AuthServices.git
    cd AuthServices

Create your local `.env` file from the template and set the SQL Server password

    cp .env-example .env

`.env` is git-ignored. It is read by `docker compose` and loaded by
IdentityServer at startup, which adds the password to the connection string.

There are three ways to run the project: everything in Docker, the
services locally with `dotnet run` (only the database in Docker), or on
a local Kubernetes cluster with Helm.

## Option A -- Run everything in Docker

    docker compose up -d --build

| Service            | URL                     |
| ------------------ | ----------------------- |
| Inventories.Client | http://localhost:5181   |
| IdentityServer     | http://localhost:5203   |
| ApiGateway         | http://localhost:7232   |
| Inventories.API    | http://localhost:5017   |

Each service has a multi-stage `Dockerfile` and an `appsettings.Docker.json`
used when `ASPNETCORE_ENVIRONMENT=Docker`. Inside Docker, services use plain
HTTP and reach each other by service name (e.g. `http://identityserver:8080`),
while the browser uses `http://localhost:<port>`. IdentityServer's
`IssuerUri` is fixed to `http://localhost:5203` so tokens validate on both
paths, and the client rewrites login/logout redirects to the public URL.

Stop the stack with `docker compose down` (add `-v` to also delete the
database volume).

## Option B -- Run the services locally

Start only the IdentityServer database (SQL Server in Docker)

    docker compose up -d identity-db

IdentityServer stores its configuration data (clients, scopes, identity
resources) and operational data (grants, consents, device codes) in SQL
Server via EF Core. On startup it applies pending migrations and seeds the
configuration tables from `Config.cs` if they are empty. The development
connection string (without the password) lives in
`IdentityServer/appsettings.Development.json`.

To add a new migration after changing the IdentityServer EF model:

    cd IdentityServer
    dotnet ef migrations add <Name> -c ConfigurationDbContext -o Data/Migrations/IdentityServer/ConfigurationDb
    dotnet ef migrations add <Name> -c PersistedGrantDbContext -o Data/Migrations/IdentityServer/PersistedGrantDb

Run each service

    dotnet run --project IdentityServer
    dotnet run --project Inventories.API
    dotnet run --project ApiGateway
    dotnet run --project Inventories.Client

## Option C -- Run on Kubernetes (kind + Helm)

Requires Docker, [kind](https://kind.sigs.k8s.io/), `kubectl` and
[Helm](https://helm.sh/). With `.env` in place, run

    k8s/deploy.sh

The script:

1.  Creates a kind cluster named `auth-services` from
    `k8s/kind-config.yaml` (skipped if it already exists).
2.  Builds the four service images with their existing Dockerfiles and
    loads them into the cluster.
3.  Creates the `identity-db-secret` Secret from `.env`, so the SQL
    Server password is kept out of Helm values and release history.
4.  Installs or upgrades the `k8s/charts/auth-services` Helm chart into
    the `auth-services` namespace and waits for all pods to be ready.

Every `kubectl`/`helm` call targets the `kind-auth-services` context
explicitly, so the script never touches your current kubectl context.

The services are exposed as NodePorts that kind maps to the same host
ports as docker-compose, so the URLs in the Option A table are the same.
Inside the cluster the Kubernetes Service names match the docker-compose
service names, so the pods run with `ASPNETCORE_ENVIRONMENT=Docker` and
reuse `appsettings.Docker.json` and `ocelot.Docker.json` unchanged.

| Chart resource                | Replaces (docker-compose)                 |
| ----------------------------- | ----------------------------------------- |
| `identity-db` StatefulSet+PVC | `identity-db` container + named volume   |
| SQL readiness probe           | `healthcheck`                             |
| IdentityServer init container | `depends_on: condition: service_healthy`  |
| `identity-db-secret` Secret   | `MSSQL_SA_PASSWORD` from `.env`           |
| NodePort Services + kind port mappings | `ports:`                         |

Useful commands

    kubectl --context kind-auth-services -n auth-services get pods
    kubectl --context kind-auth-services -n auth-services logs deploy/identityserver
    helm uninstall auth-services -n auth-services --kube-context kind-auth-services

Delete the cluster, including the database volume, with `k8s/teardown.sh`.

All services run as a single replica. IdentityServer uses a developer
signing key stored on disk and the Inventories API uses an in-memory
database, so running more replicas would need shared key storage and a
real database first.

To render or check the chart without a cluster:

    helm lint k8s/charts/auth-services
    helm template auth-services k8s/charts/auth-services

------------------------------------------------------------------------

# Security Concepts Demonstrated

-   OAuth2 Authorization Code Flow
-   OpenID Connect authentication
-   JWT access tokens
-   API Gateway security
-   Microservice authentication
-   Zero‑Trust architecture

------------------------------------------------------------------------

# Possible Improvements

-   [x] Docker containerization
-   [x] Kubernetes deployment
-   Service discovery
-   Distributed caching
-   Refresh token rotation
-   Rate limiting
-   Observability with OpenTelemetry
