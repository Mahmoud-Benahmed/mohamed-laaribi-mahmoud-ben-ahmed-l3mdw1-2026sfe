# ERP Microservices System

A comprehensive Enterprise Resource Planning (ERP) system built on a distributed microservices architecture with event-driven communication.

## Overview

This project is a modular ERP system designed using modern distributed systems principles. It provides a scalable, decoupled platform for managing core business operations including authentication, user management, client relations, inventory, and financial transactions. The system leverages event-driven architecture to ensure loose coupling between services while maintaining data consistency across domain boundaries.

### Key Features

- **Microservices Architecture**: Independent, deployable services for each business domain
- **Event-Driven Communication**: Apache K# Enterprise ERP Microservices System

This backend ecosystem is a production-grade, event‑driven ERP platform built on .NET 10, designed for multi‑tenant SaaS deployment. It decouples core business domains (authentication, clients, articles, stock, invoicing, payments, suppliers, tenants) into independently scalable microservices that communicate asynchronously via Apache Kafka. The system features a unified API Gateway with JWT authentication, fine‑grained RBAC, and fully containerized deployment (Docker Compose / Kubernetes) to ensure reliability and operational agility.

## 🏗️ System Architecture & Services Breakdown

| Service Name | Primary Responsibility | Database / Storage | Key Technologies |
|--------------|------------------------|--------------------|------------------|
| **TenantService** | Multi‑tenant lifecycle management, subscription plans, tenant provisioning | SQL Server | .NET 10, EF Core, Kafka (publisher) |
| **AuthService** | User authentication, JWT/refresh tokens, role & privilege management (RBAC) | MongoDB | .NET 10, JWT, Identity PasswordHasher, Kafka (publisher/consumer) |
| **Gateway** (YARP) | Single entry point: JWT validation, rate limiting, tenant resolution, reverse proxy | Redis (tenant cache) | YARP, JWT Bearer, StackExchange.Redis, Kafka (consumer) |
| **ArticleService** | Product catalog, article & category management, unique article code generation | SQL Server | .NET 10, EF Core, Kafka (publisher) |
| **ClientService** | Customer registry, client categories, credit limits, return windows | SQL Server | .NET 10, EF Core, Kafka (publisher) |
| **FournisseurService** | Supplier management (CRUD, block/unblock) | SQL Server | .NET 10, EF Core, Kafka (publisher) |
| **StockService** | Inventory movements (receipts, issues, returns), stock journal, real‑time stock levels | SQL Server | .NET 10, EF Core, Kafka (consumer for Article/Client/Fournisseur/Invoice events) |
| **InvoiceService** | Invoice lifecycle (draft → unpaid → paid/cancelled), line‑item management, PDF generation | SQL Server | .NET 10, EF Core, QuestPDF, Kafka (publisher & consumer) |
| **PaymentService** | Payment registration, allocation to invoices, partial payments, refund processing | SQL Server | .NET 10, EF Core, Kafka (publisher & consumer) |

## 🔄 Inter-Service Communication & Event-Driven Flows

All asynchronous communication is built on **Apache Kafka (KRaft mode)**. Services publish domain events to specific topics, and consumers react independently – ensuring loose coupling and eventual consistency.

### 1. Tenant Provisioning Flow

When a new organization is created, the system automatically initialises its isolated security environment.

1. Admin calls `POST /tenants` → **TenantService** persists tenant & subscription.
2. **TenantService** publishes `tenant.created` event (contains `TenantId`, `Slug`, branding, etc.) to Kafka.
3. **AuthService** (`TenantLifecycleConsumer`) consumes the event and:
   - Provisions default roles (`SYSTEM_ADMIN`, `SALES_MANAGER`, `STOCK_MANAGER`, `ACCOUNTANT`).
   - Seeds all required fine‑grained privileges (see `PrivilegeRegistry`).
   - Creates default tenant users (`admin_<slug>`, `sales_<slug>`, etc.).
   - Upserts tenant cache into its MongoDB collection.
4. **Gateway** (`TenantLifecycleConsumer`) also consumes `tenant.created` and updates its Redis cache for tenant resolution.
5. Other services (Article, Client, Stock, Invoice, Payment) listen to `tenant.deleted` to cascade cleanup, but do **not** pre‑provision – they rely on cache‑sync from AuthService or lazy creation.

### 2. Invoice Creation → Stock Reservation

When a sales invoice is finalized, stock levels must be reduced accordingly.

1. **User** creates an invoice via `POST /invoices` → **InvoiceService** creates a `DRAFT` invoice (no stock movement yet).
2. **User** calls `PUT /invoices/{id}/finalize` → **InvoiceService**:
   - Validates stock availability by calling **StockService** synchronously (HTTP).
   - Changes invoice status to `UNPAID`.
   - Publishes `invoice.created` event to Kafka with the full invoice details.
3. **StockService** (`InvoiceEventConsumer`) consumes `invoice.created` and:
   - Automatically creates a `BonSortie` (issue document) that deducts the invoice quantities from stock.
   - Records the movement in `JournalStock` (before/after quantities).
   - Stores an `InvoiceBonSortieMapping` for future cancellation.
4. If the invoice is later cancelled, **InvoiceService** publishes `invoice.cancelled` → **StockService** consumes it and creates a `BonRetour` to reverse the stock deduction.

## 🔐 Cross-Cutting Concerns

### Multi‑Tenancy

- **Tenant Resolution Pipeline:**
  1. **Gateway** (`TenantResolutionMiddleware`) extracts tenant from subdomain (e.g. `acme.erp.local`) or JWT claim `tenantId`.
  2. Gateway caches tenant metadata in **Redis** (keys `tenant:id:{id}` and `tenant:slug:{slug}`) and injects headers `X-Tenant-Id` and `X-Tenant-Slug` into proxied requests.
  3. Each microservice implements `ITenantContext` which reads these headers (or falls back to JWT claim) and exposes the current `TenantId`.
  4. **Entity Framework Core** uses global query filters automatically appended by `DbContext` to scope all queries to the resolved `TenantId` (e.g. `HasQueryFilter(e => !e.IsDeleted && (_tenantId == null || e.TenantId == _tenantId))`).
  5. **MongoDB** repositories in AuthService use a `BaseRepository` that injects `{ TenantId }` filters into every operation.

### Security & Authentication

- **AuthService** issues JWT access tokens (15 min lifetime) and opaque refresh tokens (7 days, stored hashed in MongoDB). Tokens contain claims: `sub` (userId), `login`, `role`, `privilege` (list of granular permissions), and `tenantId`.
- **Gateway** validates every incoming JWT (issuer, audience, signature, expiration) using `Microsoft.AspNetCore.Authentication.JwtBearer`.
- **Authorization Policies** in Gateway map to privilege codes (e.g. `CREATE_ARTICLE`, `VIEW_PAYMENTS`). Each YARP route declares an `AuthorizationPolicy` – the Gateway enforces it before forwarding the request.
- **Downstream services** optionally double‑check privileges via `User.HasClaim("privilege", ...)` inside controllers, but the primary enforcement layer is the Gateway.
- **Audit Logging** (`AuditLogger`) in AuthService records all security‑sensitive actions (login, role changes, user activation) into MongoDB.

### Resilience

- **GlobalExceptionMiddleware** in every service catches unhandled exceptions, logs them, and returns a uniform JSON error response with a `code` and `message` (e.g. `{ "code": "ART_001", "message": "Article not found", "statusCode": 404 }`).
- **Health Checks** (`/health`, `/health/live`, `/health/ready`) verify database connectivity, Kafka availability, and basic service liveness – used by container orchestrators.
- **Kafka Consumers** are configured with `EnableAutoCommit = false` and commit offsets only after successful processing, preventing message loss.
- **Idempotent Event Handling** – services check for existing cache records before applying updates (e.g. `ArticleCacheService.SyncCreatedAsync` ignores duplicate events).

## 🚀 Local Development & Containerization

The system is fully containerized and can be run in two modes:

### Docker Compose (Full Stack)

- `docker-compose.yaml` defines all services, plus:
  - **SQL Server** (one instance, but each service uses its own logical database)
  - **MongoDB** (AuthService)
  - **Redis** (Gateway tenant cache)
  - **Kafka** (KRaft mode, single node)
  - **Nginx** (reverse proxy for subdomain routing)
- Environment variables are loaded from `.env` (template provided).
- All services use `depends_on` with health checks to guarantee startup order.
- **Local development alternative:** `docker-compose.local.yaml` starts only infrastructure (Kafka, SQL Server, MongoDB, Redis) – services run from Visual Studio for debugging.

### Kubernetes (Minikube)

- Full set of manifests under `/k8s`:
  - **StatefulSets** for SQL Server, MongoDB, Kafka (with persistent volumes).
  - **Deployments** for each microservice, including `initContainers` that wait for dependencies.
  - **Secrets** generated via `create-secrets.ps1` (JWT secret, DB passwords, internal API keys).
  - **Ingress** (`nginx`) routes subdomains (`*.erp.local`) to the Gateway service.
  - **NodePort** exposes the Gateway inside the cluster.
- One‑command setup: `./k8s/setup.ps1` (Windows) builds all Docker images inside Minikube’s Docker daemon, applies secrets, and deploys everything.
- All services expose `/health` endpoints for readiness/liveness probes. with YARP (Yet Another Reverse Proxy)
- **JWT Authentication**: Secure token-based authentication with refresh token support
- **Domain-Driven Design**: Business logic encapsulated in domain models
- **Containerized Deployment**: Full Docker support with Docker Compose orchestration

## Architecture

### Architectural Style

The system follows a **Microservices Architecture** with **Event-Driven Communication** and **Domain-Driven Design** principles:

| Pattern | Implementation |
|---------|----------------|
| **Microservices** | Independent services for Auth, Users, Clients, Articles, Stock, Facturation, Paiement, Reporting |
| **Event-Driven** | Kafka for asynchronous event propagation (e.g., InvoiceCreated → Stock update) |
| **Database per Service** | Each service has its own database (SQL Server or MongoDB) |
| **API Gateway** | YARP-based gateway for routing, authentication, and rate limiting |
| **Clean Architecture** | Layered structure within each service (Controllers, Application, Domain, Infrastructure) |

### Communication Model

| Communication Type | Usage | Example |
|-------------------|-------|---------|
| **Synchronous (HTTP)** | Client ↔ Gateway ↔ Services | User login, client creation |
| **Asynchronous (Kafka)** | Service ↔ Service | Stock update after invoice creation |

### Data Flow

```
┌─────────┐    HTTP     ┌──────────────┐    HTTP     ┌─────────────┐
│ Client  │ ──────────► │ API Gateway  │ ──────────► │   Service   │
└─────────┘             └──────────────┘             └─────────────┘
                                                           │
                                                           │
                                                      ┌────▼────┐
                                                      │ Database│
                                                      └────┬────┘
                                                           │
                                                      ┌────▼────┐
                                                      │  Kafka  │
                                                      └────┬────┘
                                                           │
                                              ┌────────────▼────────────┐
                                              │ Other Service (Async)   │
                                              └─────────────────────────┘
```

## Project Structure

The system consists of the following microservices:

```
ERPSystem/
├── ERP.Gateway/                 # API Gateway (YARP)
├── ERP.AuthService/             # Authentication & Authorization
├── ERP.UserService/             # User profiles & business data
├── ERP.ClientService/           # Customer management
├── ERP.ArticlesService/         # Product management
├── ERP.StockService/            # Inventory management
├── ERP.FacturationService/      # Invoice management
├── ERP.PaiementService/         # Payment processing
├── ERP.ReportingService/        # Analytics & reporting
├── docker-compose.yaml          # Container orchestration
├── .env.example                 # Environment variables template
└── start-services.sh            # Service startup script
```

### Service Architecture (AuthService Example)

Each service follows a consistent layered architecture:

```
ERP.AuthService/
├── Controllers/                 # HTTP endpoints
├── Application/
│   ├── DTOs/                   # Request/Response objects
│   ├── Interfaces/              # Service contracts
│   └── Services/                # Business logic orchestration
├── Domain/
│   ├── Entities/                # Core business models
│   └── Enums/                   # Domain enumerations
├── Infrastructure/
│   ├── Configuration/           # Service configuration
│   ├── Persistence/             # Data access (MongoDB/EF Core)
│   └── Security/                # JWT, password hashing
└── Program.cs                   # Entry point
```

## Key Components

### Services

| Service | Database | Responsibility | Status |
|---------|----------|----------------|--------|
| **Auth Service** | MongoDB | Authentication, JWT, Refresh Tokens, Role Management | ✅ Implemented |
| **Clients Service** | SQL Server | Customer management, categories | ✅ Implemented |
| **Articles Service** | SQL Server | Product catalog, stock keeping units | 🛠 Planned |
| **Stock Service** | SQL Server | Inventory movements (receipts, issues, returns) | ✅ Implemented |
| **Facturation Service** | SQL Server | Invoice generation, credit notes | 🛠 Planned |
| **Paiement Service** | SQL Server | Payment processing, reconciliation | 🛠 Planned |
| **Reporting Service** | SQL Server | Analytical views, business intelligence | 🛠 Planned |
| **API Gateway** | — | Routing, authentication, rate limiting | 🚧 In Progress |

### Controllers (Based on Implemented Services)

#### Auth Service

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/auth/login` | POST | Authenticate user and return JWT |
| `/auth/register` | POST | Register new user (admin only) |
| `/auth/refresh` | POST | Refresh access token |
| `/auth/logout` | POST | Invalidate refresh token |

#### Client Service

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/clients` | GET | Get all clients (paginated) |
| `/clients/{id}` | GET | Get client by ID |
| `/clients` | POST | Create new client |
| `/clients/{id}` | PUT | Update client |
| `/clients/{id}` | DELETE | Soft delete client |
| `/clients/{id}/block` | PATCH | Block client |
| `/clients/{id}/categories` | POST | Assign category to client |
| `/clients/stats` | GET | Get client statistics |

#### Stock Service

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/stock/fournisseurs` | GET | Get all suppliers (paginated) |
| `/stock/fournisseurs` | POST | Create supplier |
| `/stock/bon-entres` | GET | Get stock receipts |
| `/stock/bon-entres` | POST | Create receipt document |
| `/stock/bon-sorties` | GET | Get stock issues |
| `/stock/bon-sorties` | POST | Create issue document |
| `/stock/bon-retours` | GET | Get return documents |
| `/stock/bon-retours` | POST | Create return document |

### Domain Models

#### Auth Service
- **User**: Email, password hash, role, refresh token, activation status
- **RefreshToken**: Token string, expiry, user association

#### Client Service
- **Client**: Name, email, address, credit limit, return window, block status
- **Category**: Name, code, discount rate, credit multiplier, active status
- **ClientCategory**: Many-to-many relationship with assignment audit

#### Stock Service
- **Fournisseur (Supplier)**: Name, address, tax number, RIB, block status
- **BonEntre (Receipt)**: Document number, supplier, line items, total
- **BonSortie (Issue)**: Document number, client, line items, total
- **BonRetour (Return)**: Document number, source document, reason, line items
- **LigneStock (Line Item)**: Article reference, quantity, price, total

### DTOs

DTOs serve as immutable contracts between layers:

- **Request DTOs**: `CreateClientRequestDto`, `CreateBonEntreRequestDto` with validation attributes
- **Response DTOs**: `ClientResponseDto`, `FournisseurResponseDto` with flattened data
- **Statistics DTOs**: `ClientStatsDto`, `BonStatsDto` with aggregated metrics
- **Pagination**: `PagedResultDto<T>` with total count and page metadata

### Repositories

Each service implements repository pattern for data access:

- **Active Queries**: Filter out soft-deleted records by default
- **Deleted Queries**: Access soft-deleted records via `IgnoreQueryFilters()`
- **Pagination**: Consistent pagination across all entity types
- **Statistics**: Aggregate counts and metrics

## Technologies Used

| Category | Technologies |
|----------|--------------|
| **Backend** | .NET 10, ASP.NET Core |
| **Databases** | SQL Server, MongoDB |
| **ORM/ODM** | Entity Framework Core, MongoDB.Driver |
| **Event Bus** | Apache Kafka (KRaft mode) |
| **API Gateway** | YARP (Yet Another Reverse Proxy) |
| **Authentication** | JWT Bearer, ASP.NET Identity PasswordHasher |
| **Containerization** | Docker, Docker Compose |
| **Frontend** | Angular 21 (planned) |
| **Monitoring** | ELK Stack, Prometheus, Grafana (planned) |

## Setup Instructions

### Prerequisites

| Requirement | Purpose |
|-------------|---------|
| **.NET 10 SDK** | Build and run backend services |
| **Docker Desktop** | Container orchestration (Kafka, databases) |
| **WSL2** | Linux kernel for Docker (Windows) |
| **SQL Server / SSMS** | Local database management |
| **MongoDB / Compass** | AuthService database |
| **Node.js & Angular 21** | Frontend development (planned) |

### Running the Project

#### Option 1: Visual Studio (Local Development)

1. **Start Kafka via Docker** (WSL terminal):
   ```bash
   cd /path/to/ERPSystem
   docker compose up -d
   ```

2. **Launch Services in Visual Studio**:
   - Select the `Launch Services` run profile
   - This starts all services simultaneously which are set in the Startup Projects such as: AuthService, UserService, and Gateway

> ⚠️ **Note**: On each startup, databases are wiped and re-seeded. To preserve data, disable auto-seeding in service configurations.

#### Option 2: Full Docker

1. **Open WSL terminal** and navigate to project root:
   ```bash
   cd /path/to/ERPSystem
   ```

2. **Run startup script**:
   ```bash
   ./scripts/start-services.sh
   ```

3. **Access Swagger Documentation**:
   Check ERP.Gateway/appsettings.Docker.json for the exposed port in order to access the API documentation for each service:
   ```
   # AuthService
   http://localhost:5001/swagger
   ```

### Environment Configuration

1. Copy `.env.example` to `.env`:
   ```bash
   cp .env.example .env
   ```

2. Update critical values:
   - `SQLSERVER__SA_PASSWORD`: Set strong SQL Server password
   - `JWT__SECRET`: Generate using `openssl rand -base64 32`
   - `MONGO__USERNAME` and `MONGO__PASSWORD`: Set secure MongoDB credentials

3. Configure environment-specific URLs:
   - Development: Use `_DEV` suffixed variables
   - Docker: Use `_DOCKER` suffixed variables

## Example Usage

### Authentication Flow

1. **Register User** (Admin only):
   ```bash
   POST /auth/register
   {
     "email": "admin@erp.com",
     "password": "Admin123!",
     "role": "SystemAdmin"
   }
   ```
   Keep in mind that the registration endpoint is protected and can only be accessed by users with the "SystemAdmin" role. To create the initial admin user, you may need to seed it directly into the database or temporarily allow registration without authentication during development.
   And the set "role" must match the predefined/persisted roles in the system, which are "SystemAdmin", "Accountant", "StockManager" etc.. The "SystemAdmin" role has the highest privileges. Make sure to assign roles appropriately based on the level of access required for each user.

2. **Login**:
   ```bash
   POST /auth/login
   {
     "email": "admin@erp.com",
     "password": "Admin123!"
   }
   ```

   Response:
   ```json
   {
     "accessToken": "eyJhbGciOiJIUzI1NiIs...",
     "refreshToken": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
     "expiresAt": "2026-04-02T12:00:00Z"
   }
   ```

### Create Client

```bash
POST /clients
Authorization: Bearer {accessToken}
{
  "name": "ABC Company",
  "email": "contact@abccompany.com",
  "address": "123 Business Avenue, Tunis",
  "phone": "+216 71 123 456",
  "taxNumber": "TN12345678",
  "creditLimit": 50000
}
```

### Create Stock Receipt

```bash
POST /stock/bon-entres
Authorization: Bearer {accessToken}
{
  "fournisseurId": "f4a3b2c1-d5e6-4789-abcd-ef1234567890",
  "observation": "Initial stock order",
  "lignes": [
    {
      "articleId": "a1b2c3d4-e5f6-4789-abcd-ef1234567890",
      "quantity": 100,
      "price": 15.50
    }
  ]
}
```

### Check Order Eligibility (Client Service)

```bash
GET /clients/{clientId}/can-place-order?orderAmount=1500&currentBalance=200
```

Response:
```json
{
  "canPlace": true
}
```

## Security Decisions

- **Password Storage**: Never stored in plain text; hashed using ASP.NET Identity PasswordHasher
- **Refresh Tokens**: Stored in database, can be revoked
- **Access Tokens**: Stateless JWT with short expiration (15 minutes)
- **Role-Based Authorization**: Access control based on user roles
- **Account Management**: Users can be deactivated; supports "must change password" flag
- **Environment Variables**: Sensitive data stored in `.env`, never committed to version control

## Current Implementation Status

| Feature | Status |
|---------|--------|
| MongoDB Repositories | ✅ Implemented |
| JWT Authentication | ✅ Implemented |
| Auth Service | ✅ Implemented |
| API Gateway | 🛠 In parallel with the implemented service |
| Kafka Setup | ⚙️ Docker prepared but development is halted until all serivces are implemented |
| Articles Service | ✅ Implemented |
| Client Service | ✅ Implemented |
| Stock Service | ✅ Implemented |
| Facturation Service | 🛠 Planned |
| Paiement Service | 🛠 Planned |
| Reporting Service | 🛠 Planned |
| Angular Frontend | 🛠 In parallel with the implemented service |

## Future Improvements

Based on the current codebase and architecture, planned improvements include:

1. **Kubernetes Deployment**
   - Migrate from Docker Compose to Kubernetes for production-ready orchestration
   - Implement horizontal pod autoscaling

2. **Event-Driven Patterns**
   - **Outbox Pattern**: Ensure reliable event publishing
   - **Dead Letter Topics**: Handle failed event processing
   - **Saga Pattern**: Manage distributed transactions across services

3. **Observability**
   - Centralized logging with ELK Stack (Elasticsearch, Logstash, Kibana)
   - Metrics collection with Prometheus and Grafana
   - Distributed tracing with OpenTelemetry

4. **API Gateway Enhancements**
   - Rate limiting
   - Request/response transformation
   - Circuit breaker patterns

5. **Data Consistency**
   - Implement event sourcing for critical aggregates
   - CQRS (Command Query Responsibility Segregation) for complex queries

6. **Schema Management**
   - Kafka Schema Registry for event contract evolution
   - Versioned API endpoints

7. **Testing**
   - Unit tests for domain logic
   - Integration tests for service interactions
   - Contract testing between services

8. **Frontend Development**
   - Angular-based admin dashboard
   - Real-time notifications via SignalR

9. **Security Hardening**
   - API key management for service-to-service authentication
   - OAuth2 / OpenID Connect integration
   - Audit logging for sensitive operations

## Academic Objectives

This project demonstrates mastery of:

- Distributed systems design and implementation
- Microservices architecture patterns
- Event-driven communication with Apache Kafka
- Domain-Driven Design principles
- Secure authentication and authorization mechanisms
- Clean Architecture and SOLID principles
- DevOps practices with Docker containerization
- Database per service pattern
- API Gateway pattern

## License

Academic project – Educational use only. Not intended for production deployment without additional security hardening and testing.
