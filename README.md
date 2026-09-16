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
    │   ├── Configuration
    │   ├── Controllers
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
    └── AuthServices.sln

------------------------------------------------------------------------

# Running the Project

Clone the repository

    git clone https://github.com/yourusername/AuthServices.git
    cd AuthServices

Run each service

    dotnet run --project IdentityServer
    dotnet run --project Inventories.API
    dotnet run --project ApiGateway
    dotnet run --project Inventories.Client

------------------------------------------------------------------------

# Security Concepts Demonstrated

-   OAuth2 Authorization Code Flow
-   OpenID Connect authentication
-   JWT access tokens
-   API Gateway security
-   Microservice authentication
-   Zero‑Trust architecture

------------------------------------------------------------------------

# TODOS:
- Develop CRUD Operations from Inventories.API from Inventories.Client app
- Create Access Denied page
- EF Core Integration Real DB - Reference: (Using EF Core for Configuration and Operational data)

------------------------------------------------------------------------

# Future Improvements

-   Docker containerization
-   Kubernetes deployment
-   Service discovery
-   Distributed caching
-   Refresh token rotation
-   Rate limiting
-   Observability with OpenTelemetry
