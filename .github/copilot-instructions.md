# Copilot Coding Agent Instructions

## Project Overview

You are building a **patient management demo (患者管理演示系统)** — a simple CRUD application using .NET 10 AOT + Vue 3 + TypeScript + PostgreSQL. This project does **not** use Docker, Entity Framework, or any ORM framework. Database access uses **Npgsql** directly with raw SQL and parameterized queries.

## Tech Stack

| Layer       | Technology                                       |
|-------------|--------------------------------------------------|
| Frontend    | Vue 3 + TypeScript + Vite + Pinia + Vue Router   |
| Backend     | .NET 10 with Native AOT                          |
| Database    | PostgreSQL 16                                    |
| DB Access   | Npgsql (raw SQL, parameterized queries, NO ORM)  |
| API Style   | RESTful JSON Minimal API                         |
| Testing     | xUnit (backend), Vitest (frontend)               |

## Repository Structure

```
autoagents2/
├── .github/
│   ├── copilot-instructions.md      ← This file
│   ├── workflows/                   ← CI/CD pipelines
│   └── ISSUE_TEMPLATE/              ← Issue templates
├── src/
│   ├── backend/
│   │   ├── PatientApi/              ← .NET 10 AOT Minimal API project
│   │   └── PatientApi.Tests/        ← xUnit tests
│   └── frontend/
│       ├── src/
│       │   ├── components/          ← Vue components
│       │   ├── views/               ← Page-level views
│       │   ├── stores/              ← Pinia stores
│       │   ├── services/            ← API client (axios)
│       │   ├── types/               ← TypeScript type definitions
│       │   └── router/              ← Vue Router config
│       └── tests/                   ← Vitest tests
├── database/
│   └── init.sql                     ← DDL for patients table
└── README.md
```

## Core Domain

### Patient Entity

```csharp
public class Patient
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Address { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

### Database Table

```sql
CREATE TABLE IF NOT EXISTS patients (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(100) NOT NULL,
    gender VARCHAR(10) NOT NULL,
    date_of_birth DATE NOT NULL,
    phone VARCHAR(20) NOT NULL,
    address VARCHAR(200),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

## Backend Requirements (.NET 10 AOT)

### AOT Compatibility Rules
- Use `System.Text.Json` with source generation (`[JsonSerializable]`)
- No reflection-based serialization
- Use Minimal API (`app.MapGet`, `app.MapPost`, etc.)
- Use `Npgsql` for all database access — **no EF Core, no Dapper, no ORM**
- All SQL must use parameterized queries to prevent SQL injection
- Read connection string from environment variable `CONNECTION_STRING`

### API Endpoints

```
GET    /api/patients            List all patients
POST   /api/patients            Create a patient
GET    /api/patients/{id}       Get patient by ID
PUT    /api/patients/{id}       Update patient
DELETE /api/patients/{id}       Delete patient
GET    /health                  Health check
```

## Frontend Requirements (Vue 3)

### Technology Choices
- **State**: Pinia
- **Routing**: Vue Router 4
- **HTTP**: axios
- **UI**: Element Plus
- **Testing**: Vitest + Vue Test Utils

### Pages
- **PatientList.vue** — table with search, add/edit/delete buttons
- **PatientForm.vue** — form for create/edit patient (name, gender, DOB, phone, address)

## When Implementing an Issue

1. Read the issue title and description carefully
2. Create feature branch from `main` named `feature/task-{N}-{short-description}`
3. Implement **all** requirements listed
4. Write unit tests alongside implementation
5. Submit PR with a clear description referencing the issue number (`Closes #N`)

## Code Quality Standards

- Follow C# Microsoft conventions
- Follow Vue 3 Composition API style guide
- All API calls must have proper error handling
- All SQL must use parameterized queries
- Never commit secrets or connection strings
