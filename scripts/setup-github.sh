#!/usr/bin/env bash
set -euo pipefail

REPO="gjyhj1234/autoagents2"

create_label() {
  local name="$1" color="$2" description="$3"
  gh label create "$name" --color "$color" --description "$description" \
     --repo "$REPO" 2>/dev/null \
    || gh label edit "$name" --color "$color" --description "$description" \
       --repo "$REPO" 2>/dev/null \
    || true
}

create_label "agent-task"        "0075ca" "Task ready for Copilot Coding Agent"
create_label "agent-in-progress" "e4e669" "Copilot Agent is currently working on this issue"
create_label "agent-queued"      "fbca04" "Task is queued and waiting for Copilot"
create_label "agent-completed"   "0e8a16" "Agent task completed and merged"
create_label "auto-merge"        "6f42c1" "PR will be auto-merged after all checks pass"
create_label "backend"           "bfd4f2" ".NET 10 backend API"
create_label "frontend"          "d2f5b0" "Vue 3 frontend"
create_label "database"          "c5def5" "Database schema"

gh api repos/"$REPO" \
  --method PATCH \
  -f allow_auto_merge=true \
  -f delete_branch_on_merge=true \
  --silent 2>/dev/null || true

# Issue 1
gh issue create --repo "$REPO" \
  --title "[Task-01] Database Schema and Init SQL" \
  --label "agent-task,database" \
  --body '## Objective
Create the PostgreSQL database init script.

## Requirements
- [ ] Create `database/init.sql` with the `patients` table DDL
- [ ] Table columns: `id` (UUID PK, default gen_random_uuid()), `name` (VARCHAR 100 NOT NULL), `gender` (VARCHAR 10 NOT NULL), `date_of_birth` (DATE NOT NULL), `phone` (VARCHAR 20 NOT NULL), `address` (VARCHAR 200 nullable), `created_at` (TIMESTAMPTZ NOT NULL DEFAULT now()), `updated_at` (TIMESTAMPTZ NOT NULL DEFAULT now())
- [ ] Add a few INSERT statements as seed data (3-5 sample patients)

## Acceptance Criteria
- [ ] `init.sql` is valid PostgreSQL syntax
- [ ] Table uses UUID primary key'

# Issue 2
gh issue create --repo "$REPO" \
  --title "[Task-02] Backend .NET 10 AOT Patient API" \
  --label "agent-task,backend" \
  --body '## Objective
Create the .NET 10 AOT Minimal API backend for patient CRUD.

## Requirements
- [ ] Create `src/backend/PatientApi/` project using `dotnet new web`
- [ ] Target .NET 10, enable PublishAot in csproj
- [ ] Use `Npgsql` for database access (NO EF Core, NO Dapper, NO ORM)
- [ ] Use `System.Text.Json` source generation for AOT compatibility
- [ ] Read connection string from environment variable `CONNECTION_STRING`
- [ ] Implement Minimal API endpoints:
  - `GET /api/patients` — list all patients
  - `POST /api/patients` — create patient
  - `GET /api/patients/{id}` — get patient by ID
  - `PUT /api/patients/{id}` — update patient
  - `DELETE /api/patients/{id}` — delete patient
  - `GET /health` — health check
- [ ] All SQL queries must use parameterized queries
- [ ] Enable CORS for all origins (demo purpose)
- [ ] Create a `PatientApi.sln` solution file in `src/backend/`

## Acceptance Criteria
- [ ] `dotnet build` succeeds
- [ ] API endpoints work correctly
- [ ] No EF Core or ORM dependencies

## Depends On
#ISSUE1'

# Issue 3
gh issue create --repo "$REPO" \
  --title "[Task-03] Backend Unit Tests" \
  --label "agent-task,backend" \
  --body '## Objective
Add xUnit tests for the backend API.

## Requirements
- [ ] Create `src/backend/PatientApi.Tests/` xUnit test project
- [ ] Add project to `PatientApi.sln`
- [ ] Test patient model serialization/deserialization
- [ ] Test API endpoint routing using `WebApplicationFactory` (integration tests, may use in-memory or mock)
- [ ] At least 5 test cases

## Acceptance Criteria
- [ ] `dotnet test` passes all tests
- [ ] Tests cover basic CRUD logic

## Depends On
#ISSUE2'

# Issue 4
gh issue create --repo "$REPO" \
  --title "[Task-04] Frontend Vue 3 Project Setup and Patient List Page" \
  --label "agent-task,frontend" \
  --body '## Objective
Create the Vue 3 + TypeScript frontend with patient list page.

## Requirements
- [ ] Create `src/frontend/` using Vite + Vue 3 + TypeScript scaffold
- [ ] Install dependencies: `vue-router`, `pinia`, `axios`, `element-plus`
- [ ] Create TypeScript type `Patient` in `src/types/patient.ts`
- [ ] Create API service `src/services/patientApi.ts` using axios
- [ ] Create Pinia store `src/stores/patientStore.ts`
- [ ] Create `PatientList.vue` page with Element Plus table, search input, add/edit/delete buttons
- [ ] Create `PatientForm.vue` component (dialog/modal for create/edit patient with fields: name, gender, DOB, phone, address)
- [ ] Configure Vue Router with routes: `/` → PatientList
- [ ] Configure axios base URL from environment variable `VITE_API_BASE_URL` (default `http://localhost:5000`)

## Acceptance Criteria
- [ ] `npm run build` succeeds
- [ ] Patient list page renders correctly
- [ ] CRUD operations work when backend is running

## Depends On
#ISSUE2'

# Issue 5
gh issue create --repo "$REPO" \
  --title "[Task-05] Frontend Tests and README" \
  --label "agent-task,frontend" \
  --body '## Objective
Add frontend tests and update the project README.

## Requirements
- [ ] Install Vitest and Vue Test Utils as dev dependencies
- [ ] Add `test` script to `package.json`
- [ ] Write tests for `patientStore.ts` (at least 2 test cases)
- [ ] Write a component test for `PatientList.vue` (at least 1 test case)
- [ ] Update `README.md` in the repository root with:
  - Project description (Patient Management Demo)
  - Tech stack (.NET 10 AOT + Vue 3 + TypeScript + PostgreSQL)
  - How to run the backend
  - How to run the frontend
  - API endpoint list

## Acceptance Criteria
- [ ] `npm run test` passes
- [ ] README is complete and accurate

## Depends On
#ISSUE4'
