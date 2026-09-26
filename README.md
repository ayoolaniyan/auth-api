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
-   **Consul** -- Service registry used by the gateway to find backend services -- HashiCorp Consul
-   **Redis** -- Distributed cache shared by all service instances -- Redis

The architecture demonstrates:

-   OAuth2 Authorization Code Flow
-   API Gateway security
-   Token-based API protection
-   Zero‑Trust microservice communication
-   Service discovery with health-checked, load-balanced routing
-   Distributed caching and shared Data Protection keys with Redis

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
5.  When expired, Refresh Token obtains a new Access Token **and a new
    Refresh Token**; the old one stops working (see
    [Refresh Token Rotation](#refresh-token-rotation)).

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

# Service Discovery

The API Gateway no longer has hard-coded downstream hosts. Each
Inventories.API instance registers itself with **Consul**, and Ocelot
looks the service up by name on every request.

``` mermaid
flowchart LR
Client[Client Application]
Gateway[API Gateway - Ocelot]
Consul[Consul Registry]
API1[Inventories API instance 1]
API2[Inventories API instance N]

API1 -- register + /health --> Consul
API2 -- register + /health --> Consul
Consul -- health check --> API1
Consul -- health check --> API2
Client --> Gateway
Gateway -- lookup inventories-api --> Consul
Gateway -- round robin --> API1
Gateway -- round robin --> API2
```

**How it works**

1.  On startup, Inventories.API registers with Consul as `inventories-api`,
    using a unique instance ID and an HTTP health check on `/health`
    (`Inventories.API/ServiceDiscovery`). Registration is retried in the
    background until Consul is reachable, and is restored within 30 seconds
    if Consul loses it (dev-mode Consul keeps its catalog in memory).
2.  Consul checks each instance every 10 seconds and removes instances whose
    check has failed for more than a minute. Instances also deregister
    themselves on graceful shutdown.
3.  Ocelot routes use `"ServiceName": "inventories-api"` instead of
    `DownstreamHostAndPorts`, with `RoundRobin` load balancing.
    `GlobalConfiguration.ServiceDiscoveryProvider` in `ocelot.json` points at
    Consul. Only instances with a passing check are used. If there are none,
    the gateway returns `404`.
4.  Token validation is unchanged: the gateway still validates the JWT
    before forwarding, and the API validates it again.

The gateway uses a small custom service builder
(`ApiGateway/ServiceAddressConsulServiceBuilder.cs`). Ocelot's default
builder routes to the Consul *node name*, which with a single Consul agent
is the Consul container itself. The custom builder routes to the address
each instance registered with.

| Run mode        | Consul address          | API registers as        | Consul health check URL                        |
| --------------- | ----------------------- | ----------------------- | ---------------------------------------------- |
| `dotnet run`    | `http://localhost:8500` | `localhost:5017`        | `https://host.docker.internal:5017/health`     |
| docker-compose  | `http://consul:8500`    | `inventories-api:8080`  | `http://inventories-api:8080/health`           |
| Kubernetes      | `http://consul:8500`    | `<pod IP>:8080`         | `http://<pod IP>:8080/health`                  |

These values come from the `ServiceDiscovery` section of
`Inventories.API/appsettings*.json`. In Kubernetes, the pod IP is injected
through the `ServiceDiscovery__ServiceAddress` environment variable.

IdentityServer and the client application are **not** resolved through
Consul. Their URLs are part of token validation (issuer) and browser
redirects, so they stay fixed public URLs.

The Consul UI is at http://localhost:8500 in every run mode.

------------------------------------------------------------------------

# Distributed Caching

Redis is a cache shared by every instance of every service, so a value
cached by one instance can be read by all of them. It holds three things:

``` mermaid
flowchart LR
Client[Inventories.Client]
Identity[IdentityServer]
API[Inventories API instances]
Redis[(Redis)]
SQL[(SQL Server)]
DB[(Inventories DB)]

API -- 1. inventory reads --> Redis
API -- on miss --> DB
Identity -- 2. clients, resources, scopes --> Redis
Identity -- on miss --> SQL
Identity -- 3. Data Protection keys --> Redis
Client -- 3. Data Protection keys --> Redis
```

| What                            | Service         | Redis key prefix                          | Expires / invalidated                               |
| ------------------------------- | --------------- | ----------------------------------------- | --------------------------------------------------- |
| Inventory list and single items | Inventories.API | `inventories-api:inventories:`            | After 60 s, or immediately on `POST`/`PUT`/`DELETE` |
| Clients, API scopes, resources  | IdentityServer  | `identityserver:<Type>:` (e.g. `Client:`) | After 15 min (Duende's default cache expiration)    |
| Data Protection key ring        | IdentityServer  | `identityserver:data-protection-keys`     | Never (keys rotate every 90 days)                   |
| Data Protection key ring        | Client          | `inventories-client:data-protection-keys` | Never (keys rotate every 90 days)                   |

**How it works**

1.  **Inventory reads (cache-aside).** `GET /api/inventories` and
    `GET /api/inventories/{id}` read from Redis first and fall back to the
    database on a miss, then store the result
    (`Inventories.API/Caching/InventoryCache.cs`). Every write removes the
    cached list and the changed item, so the next read reloads them. The TTL
    is `Redis:InventoryTtlSeconds`. Not-found results are not cached.
2.  **IdentityServer configuration store.** `AddConfigurationStoreCache()`
    stops IdentityServer from querying SQL Server for the client and its
    scopes on every token request. By default Duende keeps that cache in
    each process's memory. `IdentityServer/Caching/DistributedCache.cs`
    replaces it with Redis for clients, API scopes, API resources and
    identity resources. Cached entries are not removed when the database
    changes, so configuration edits take effect after the entry expires.
3.  **Data Protection keys.** ASP.NET Core encrypts the login cookie, the
    OIDC correlation/nonce cookies and anti-forgery tokens with Data
    Protection keys. By default these are stored in the container's file
    system, so a restarted container can no longer read cookies it issued
    and users are signed out. Both IdentityServer and the client now keep
    their key ring in Redis, so users stay signed in across restarts and
    any instance can read a cookie issued by another. Redis runs with
    append-only persistence on a volume, so the keys also survive Redis
    restarts.

**If Redis is down**

-   Inventories.API and IdentityServer's configuration cache keep working.
    Cache errors are logged as warnings and the value is read from the
    database. The Redis client fails fast while disconnected (no timeout
    wait) and reconnects in the background.
-   Redis is intentionally **not** part of the API's `/health` check, so a
    Redis outage does not make Consul remove every API instance.
-   Data Protection needs Redis. New keys cannot be loaded or created while
    it is down, so sign-in may fail until Redis is back.

| Run mode        | Redis address    |
| --------------- | ---------------- |
| `dotnet run`    | `localhost:6379` |
| docker-compose  | `redis:6379`     |
| Kubernetes      | `redis:6379`     |

These come from the `Redis` section of each service's `appsettings*.json`.

Inspect the cache with `redis-cli`:

    # docker-compose / dotnet run
    redis-cli --scan
    # Kubernetes
    kubectl --context kind-auth-services -n auth-services exec statefulset/redis -- redis-cli --scan

**Security note:** Redis runs without a password or TLS here, which is
fine for local development only. It holds the Data Protection keys (anyone
who can read them can decrypt or forge cookies) and hashed client secrets,
so in production enable Redis `AUTH`/ACLs and TLS, keep it on a private
network, and consider encrypting the key ring at rest
(`ProtectKeysWith...`).

**Caveat:** the Inventories API still uses an in-memory database, so each
replica has its own data. With more than one replica the shared cache can
return data loaded from a different replica's database. A shared database
is needed before running more than one API replica.

------------------------------------------------------------------------

# Refresh Token Rotation

The MVC client (`inventories_mvc_client`) gets a refresh token at login
(`offline_access` scope) and uses it to renew its access token without
sending the user back to the login page. Every refresh token can be used
**once**: redeeming it returns a new access token and a new refresh token,
and IdentityServer deletes the old one.

``` mermaid
sequenceDiagram
participant Browser
participant Client as Inventories.Client
participant IDP as IdentityServer
participant API as API Gateway / Inventories.API

Browser->>Client: Request (login cookie holds AT1 + RT1)
Note over Client: AT1 expires in < 60 s
Client->>IDP: POST /connect/token (grant_type=refresh_token, RT1)
IDP-->>Client: AT2 + RT2 (RT1 is now invalid)
Client-->>Browser: Re-issued cookie with AT2 + RT2
Client->>API: Bearer AT2
Note over Client,IDP: A later attempt to use RT1 gets invalid_grant
```

| Setting (IdentityServer/Config.cs) | Value                  |
| ---------------------------------- | ---------------------- |
| `AccessTokenLifetime`              | 5 minutes              |
| `RefreshTokenUsage`                | `OneTimeOnly` (rotate) |
| `RefreshTokenExpiration`           | `Sliding`              |
| `SlidingRefreshTokenLifetime`      | 15 days                |
| `AbsoluteRefreshTokenLifetime`     | 30 days after login    |

**How it works**

1.  **IdentityServer.** The client is allowed offline access and its
    refresh tokens are one-time-only. Each refresh extends the refresh
    token's lifetime by 15 days, but never beyond 30 days after login;
    after that the user signs in again. Claims such as roles are re-read on
    every refresh (`UpdateAccessTokenClaimsOnRefresh`).
2.  **Client.** `Authentication/RefreshTokenCookieEvents.cs` runs on every
    authenticated request. When the access token expires within 60
    seconds, it calls `RefreshTokenService`, which redeems the refresh
    token at IdentityServer's token endpoint, and writes the new tokens
    back into the login cookie. API calls and the userinfo call then use
    the fresh access token.
3.  **Parallel requests.** Two requests from the same user can find an
    expired token at the same time. Because a refresh token only works
    once, the second call would be rejected. `RefreshTokenService`
    therefore lets only one request per refresh token call IdentityServer
    and hands the result to the others for one minute. This lock is per
    process, so it assumes one client instance (the current setup).
4.  **When refresh fails.** If IdentityServer rejects the refresh token
    (`invalid_grant`: already used, revoked or expired), the user is signed
    out of the client and `[Authorize]` pages redirect to the login page.
    If IdentityServer can't be reached, the session is kept and the refresh
    is retried on the next request. Sessions created before this feature
    have no refresh token and are signed out once.
5.  **Config sync.** `SeedData` now re-creates clients from `Config.cs` on
    every IdentityServer start (other configuration is still only seeded
    into empty tables) and removes the client's cached copy from Redis, so
    changes to client settings take effect on restart, including in
    existing databases.

Refresh tokens are stored in IdentityServer's operational store
(`PersistedGrants` table) and removed by the token cleanup job once they
expire or are used.

------------------------------------------------------------------------

# Rate Limiting

Requests are limited with ASP.NET Core's built-in rate limiter
(`Microsoft.AspNetCore.RateLimiting`). A caller that goes over its limit
gets **429 Too Many Requests** with a `Retry-After` header (seconds). On
the gateway this is the soonest a request can be allowed again (one
10-second segment of the sliding window), so a caller may need to retry
more than once.

| Service        | What is limited                   | Counted per                              | Default limit       | Window         |
| -------------- | --------------------------------- | ---------------------------------------- | ------------------- | -------------- |
| ApiGateway     | All API calls with a valid token  | User (`sub`), or `client_id` if no user  | 100 requests        | 60 s, sliding  |
| ApiGateway     | Calls without a valid token       | IP address                               | 20 requests         | 60 s, sliding  |
| IdentityServer | Login form submissions            | IP address                               | 5 attempts          | 60 s, fixed    |
| IdentityServer | Token endpoint (`/connect/token`) | `client_id`                              | 300 requests        | 60 s, fixed    |

Limits are set in the `RateLimiting` section of each service's
`appsettings.json`.

**How it works**

1.  **API Gateway.** `UseAuthentication()` validates the access token
    before Ocelot runs, so the limiter knows who is calling. Each user gets
    their own limit even though all calls arrive from the Inventories.Client
    server. Ocelot still authenticates each route itself.
2.  **IdentityServer login.** Only `POST /Account/Login` is limited, which
    slows down password guessing. Showing the login page is not limited.
3.  **IdentityServer token endpoint.** Token refreshes for every user of a
    client come from that client's server, so this is limited per
    `client_id` rather than per IP. The id is read from the Basic
    Authorization header or from the form body.
4.  **Client.** When the gateway returns 429, Inventories.Client shows a
    "Too Many Requests" page instead of the generic error page. A 429 from
    the token endpoint during an access token refresh keeps the session and
    retries on the next request.

**Caveats**

-   Counters are kept in each process's memory. Every service runs a
    single instance today; with several gateway or IdentityServer
    instances each would count separately, and a shared store (for example
    Redis) would be needed.
-   In Docker and kind, requests reach the services through port
    forwarding, so many callers can share one IP address. Per-IP limits
    (anonymous API calls, logins) then apply to all of them together.
    Per-user and per-client limits are not affected.
-   Inventories.API is also published directly (port 5017) for local
    development. Calls made there bypass the gateway and are not limited.

------------------------------------------------------------------------

# Project Structure

    AuthServices
    │
    ├── IdentityServer
    │   ├── Caching            (Redis config store cache + Data Protection)
    │   ├── RateLimiting       (login and token endpoint limits)
    │   ├── Data/Migrations
    │   ├── Pages
    │   ├── Config.cs
    │   ├── SeedData.cs
    │   └── Program.cs
    │
    ├── Inventories.API
    │   ├── Controllers
    │   ├── Services
    │   ├── Caching            (Redis cache-aside for inventory reads)
    │   ├── ServiceDiscovery   (Consul registration)
    │   └── Models
    │
    ├── ApiGateway
    │   ├── RateLimiting       (per-user limits for API calls)
    │   ├── ocelot.json        (routes resolved via Consul)
    │   └── ServiceAddressConsulServiceBuilder.cs
    │
    ├── Inventories.Client
    │   ├── Authentication     (access token refresh with rotated refresh tokens)
    │   └── Filters            ("Too Many Requests" page for 429 responses)
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
| Consul UI          | http://localhost:8500   |
| Redis              | localhost:6379          |

Each service has a multi-stage `Dockerfile` and an `appsettings.Docker.json`
used when `ASPNETCORE_ENVIRONMENT=Docker`. Inside Docker, services use plain
HTTP and reach each other by service name (e.g. `http://identityserver:8080`),
while the browser uses `http://localhost:<port>`. IdentityServer's
`IssuerUri` is fixed to `http://localhost:5203` so tokens validate on both
paths, and the client rewrites login/logout redirects to the public URL.

Stop the stack with `docker compose down` (add `-v` to also delete the
database volume).

## Option B -- Run the services locally

Start only the IdentityServer database (SQL Server), Consul and Redis in Docker

    docker compose up -d identity-db consul redis

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

Inventories.API registers itself with Consul as `localhost:5017`. Consul
runs in Docker, so it health-checks the API through
`https://host.docker.internal:5017/health` and skips TLS verification for
the self-signed development certificate.

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
| `consul` Deployment + NodePort | `consul` container                       |
| `redis` StatefulSet+PVC       | `redis` container + named volume          |

Useful commands

    kubectl --context kind-auth-services -n auth-services get pods
    kubectl --context kind-auth-services -n auth-services logs deploy/identityserver
    helm uninstall auth-services -n auth-services --kube-context kind-auth-services

Delete the cluster, including the database volume, with `k8s/teardown.sh`.

The Consul UI port (8500) is a kind port mapping, and kind can only set those
when it creates a cluster. If your cluster was created before service
discovery was added, `deploy.sh` prints a warning. Run `k8s/teardown.sh`
and deploy again to get the Consul UI on http://localhost:8500. Service
discovery inside the cluster works either way.

All services run as a single replica. IdentityServer uses a developer
signing key stored on disk and the Inventories API uses an in-memory
database, so running more replicas would need shared signing key storage
and a real database first. Data Protection keys and the caches are
already shared through Redis. The gateway is ready for more Inventories API
replicas: each pod registers its own IP with Consul and Ocelot
round-robins across the healthy ones. However, each replica would have
its own separate in-memory data.

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
-   Service discovery (Consul) with health checks
-   Shared Data Protection keys for cookies across instances (Redis)
-   Refresh token rotation with short-lived access tokens
-   Rate limiting per user, client and IP (API Gateway, IdentityServer)

------------------------------------------------------------------------

# Possible Improvements

-   [x] Docker containerization
-   [x] Kubernetes deployment
-   [x] Service discovery
-   [x] Distributed caching
-   [x] Refresh token rotation
-   [x] Rate limiting
-   Observability with OpenTelemetry
