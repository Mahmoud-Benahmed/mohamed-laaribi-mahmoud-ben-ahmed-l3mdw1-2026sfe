---

# 🏢 ERP Microservices System

### Final Year Project – Distributed ERP Architecture

---

## 📌 Project Overview

This project is a modular **Enterprise Resource Planning (ERP)** system designed using a **Microservices Architecture** and **Event-Driven Communication**.

The system combines:

* ✅ .NET 10 Web APIs
* ✅ MongoDB & SQL Server (Database per Service)
* ✅ Apache Kafka (KRaft mode)
* ✅ Docker Compose
* ✅ API Gateway (YARP)
* ✅ JWT Authentication
* ✅ Angular Frontend (planned)

The goal is to design a **scalable, resilient, and decoupled ERP platform** aligned with modern distributed systems practices.

---

# 🏗️ Architecture Overview

## 🔹 Architectural Style

* Microservices Architecture
* Event-Driven Architecture
* Domain-Driven Design (DDD)
* Database per Service
* API Gateway Pattern

## 🔹 Communication Model

| Type  | Usage                                   |
| ----- | --------------------------------------- |
| HTTP  | Client ↔ API Gateway ↔ Services         |
| Kafka | Service ↔ Service (asynchronous events) |

---

# 📦 Services

| Service             | Database   | Responsibility                      |
| ------------------- | ---------- | ----------------------------------- |
| Auth Service        | MongoDB    | Authentication, JWT, Refresh Tokens |
| Clients Service     | SQL Server | Customer management                 |
| Articles Service    | SQL Server | Product management                  |
| Facturation Service | SQL Server | Invoice management                  |
| Paiement Service    | SQL Server | Payment processing                  |
| Stock Service       | SQL Server | Inventory management                |
| Reporting Service   | SQL Server | Analytical projections              |
| API Gateway         | —          | Central entry point                 |

---

# 🔐 Auth Service

## Responsibilities

* User authentication
* JWT generation
* Refresh token management
* Role management
* Account activation / deactivation
* Password hashing (ASP.NET Identity PasswordHasher)

## Technologies

* .NET 10
* MongoDB
* JWT Bearer Authentication
* Clean Architecture layers

---

# 🧱 Project Structure (Auth Service Example)

```
ERP.AuthService
│
├── Application
│   ├── DTOs
│   ├── Interfaces
│   └── Services
│
├── Domain
│   ├── Entities
│   └── Enums
│
├── Infrastructure
│   ├── Configuration
│   ├── Persistence
│   └── Security
│
├── Controllers
├── Program.cs
└── appsettings.json
```

---

# 🔑 Authentication Flow

1. Admin registers a new user
2. Password is hashed using ASP.NET PasswordHasher
3. User logs in
4. Access Token (15 min) is generated
5. Refresh Token (7 days) is stored in MongoDB
6. Access token used to call protected endpoints

---

# 📨 Event-Driven Communication

Kafka is used to propagate domain events.

### Example Flow:

1. Facturation Service publishes:

   * `InvoiceCreated`
2. Stock Service consumes event
3. Stock quantities are updated
4. Reporting Service builds projections

---

# 🗄️ Database Strategy

The system follows:

> **Strict Database per Service Rule**

* No shared databases
* No cross-service SQL queries
* Communication only via API or Kafka events

---

# 🐳 Docker Setup

## Services in Docker Compose

* Kafka (KRaft mode)
* Kafka UI
* MongoDB
* SQL Server
* Auth Service
* Other services (planned)

To run:

```bash
docker-compose up -d
```

---

# 🚀 Running the Auth Service

## 1️⃣ Restore dependencies

```bash
dotnet restore
```

## 2️⃣ Run project

```bash
dotnet run
```

## 3️⃣ Open Swagger

```
https://localhost:xxxx/swagger
```

---

# 🧪 Testing API

Using Swagger:

### Register (Admin only)

```json
{
  "email": "admin@erp.com",
  "password": "Admin123!",
  "role": 0
}
```

### Login

```json
{
  "email": "admin@erp.com",
  "password": "Admin123!"
}
```

Response:

```json
{
  "accessToken": "...",
  "refreshToken": "...",
  "expiresAt": "..."
}
```

---

# 🔒 Security Decisions

* Passwords are never stored in plain text
* Refresh tokens are stored in DB
* Access tokens are stateless JWT
* Role-based authorization
* Users can be deactivated
* MustChangePassword flag supported

---

# ⚠️ Current Implementation Status

| Feature              | Status             |
| -------------------- | ------------------ |
| Auth Service         | ✅ Implemented      |
| MongoDB Repositories | ✅ Implemented      |
| JWT Authentication   | ✅ Implemented      |
| Kafka Setup          | ⚙️ Docker prepared |
| Other Services       | 🛠 Planned         |
| Angular Frontend     | 🛠 Planned         |

---

# 📈 Scalability & Future Improvements

* Kubernetes deployment
* Outbox pattern
* Dead Letter Topics
* Saga Pattern
* Schema Registry
* Centralized logging (ELK)
* Prometheus + Grafana
* Distributed tracing (OpenTelemetry)

---

# 🎓 Academic Objectives

This project demonstrates:

* Distributed systems design
* Microservices architecture
* Event-driven communication
* Secure authentication mechanisms
* Clean Architecture principles
* DevOps practices with Docker

---

# 👥 Team

* Backend Architecture & Auth: *Your Name*
* Frontend Development: *Colleague Name*

---

# 📜 License

Academic project – Educational use only.

---
