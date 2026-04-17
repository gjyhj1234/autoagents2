# Copilot Coding Agent Instructions

## Project Overview

You are building a **comprehensive dental chart (口腔牙位图) system** — a professional-grade dental practice management platform inspired by Open Dental, but with richer functionality. This is **not a demo**; every feature must be production-ready.

## Tech Stack

| Layer       | Technology                                      |
|-------------|------------------------------------------------|
| Frontend    | Vue 3 + TypeScript + Vite + Pinia + Vue Router |
| Backend     | .NET 10 with Native AOT (Ahead-of-Time compilation) |
| Database    | PostgreSQL 16                                  |
| ORM         | Entity Framework Core 9 (AOT-compatible mode)  |
| API Style   | RESTful JSON API with OpenAPI/Swagger          |
| Auth        | JWT Bearer tokens with refresh tokens          |
| Containers  | Docker + Docker Compose                        |

## Repository Structure

```
autoagents/
├── .github/
│   ├── copilot-instructions.md      ← This file
│   ├── workflows/                   ← CI/CD pipelines
│   └── ISSUE_TEMPLATE/              ← Issue templates
├── docs/
│   ├── dental-chart-requirements.md ← Full PRD
│   └── tasks/                       ← Individual sprint tasks
├── src/
│   ├── backend/                     ← .NET 10 AOT backend
│   │   ├── DentalChart.Api/         ← Web API project
│   │   ├── DentalChart.Core/        ← Domain models & interfaces
│   │   ├── DentalChart.Infrastructure/ ← EF Core, PostgreSQL
│   │   └── DentalChart.Tests/       ← Unit + integration tests
│   └── frontend/                    ← Vue 3 frontend
│       ├── src/
│       │   ├── components/
│       │   │   ├── dental-chart/    ← Core tooth chart SVG components
│       │   │   ├── periodontal/     ← Perio chart components
│       │   │   ├── treatment/       ← Treatment planning components
│       │   │   └── shared/          ← Shared UI components
│       │   ├── views/               ← Page-level components
│       │   ├── stores/              ← Pinia stores
│       │   ├── services/            ← API client services
│       │   ├── types/               ← TypeScript type definitions
│       │   └── utils/               ← Shared utilities
│       └── tests/                   ← Frontend tests (Vitest)
├── database/
│   ├── migrations/                  ← SQL migration scripts
│   └── seeds/                       ← Seed data
├── docker/
│   ├── docker-compose.yml
│   ├── docker-compose.prod.yml
│   └── nginx.conf
└── README.md
```

## Core Domain Model

### Teeth Numbering
- **Universal (ADA) numbering**: 1–32 for permanent teeth, A–T for primary teeth
- **FDI (ISO 3950) notation**: Two-digit system (11–48), also support Palmer notation
- The chart must display: upper-right → upper-left → lower-left → lower-right (clockwise from patient's perspective)
- Support toggling between adult (permanent) and mixed/primary dentition

### Key Entities

```csharp
// Core domain entities (use these exact names)
Patient, DentalChart, Tooth, ToothCondition, ToothSurface,
TreatmentPlan, TreatmentPlanItem, ProcedureCode, CompletedProcedure,
PeriodontalChart, PeriodontalMeasurement, Appointment, AppointmentType,
InsurancePlan, InsuranceClaim, User, Provider, Note
```

### Tooth Surfaces (MODFL)
- **M** = Mesial  
- **O** = Occlusal (anterior: Incisal)  
- **D** = Distal  
- **F** = Facial/Buccal (anterior: Labial)  
- **L** = Lingual (anterior: Lingual/Palatal)  
- **R** = Root (for root conditions)

## Dental Chart Features Required

### Visual Chart (SVG-based)
1. Full arch display — all 32 permanent teeth + 20 primary teeth
2. Toggle between: Permanent mode / Primary mode / Mixed mode
3. Tooth anatomy SVG with 5-surface segments (click per surface)
4. Per-tooth status icons: missing, extracted, unerupted, impacted
5. Condition color overlays per surface:
   - Red = decay/caries
   - Gold/yellow = crown
   - Blue = treatment planned
   - Gray = missing
   - Silver = amalgam
   - White = composite
   - Purple = pontic (bridge)
6. Bridge connectors rendered between abutment teeth
7. Implant indicator (screw icon below tooth)
8. Root canal indicator (red lines through roots)
9. Sealant, veneer, partial denture, full denture indicators
10. Tooltip on hover showing tooth details
11. Click tooth → select → show detail panel on right

### Condition/Procedure Panel
- ADA procedure codes (CDT codes) lookup with autocomplete
- Apply procedure to: whole tooth, individual surface, or range of teeth
- Status: Treatment Planned, In Progress, Completed, Existing (done elsewhere), Referred
- Notes field per condition
- Date completed
- Provider assignment
- Fee (with insurance adjustment)

### Periodontal Chart
- 6-point probing per tooth (3 buccal + 3 lingual)
- Recession and attachment loss auto-calculated
- Bleeding on probing (BOP) toggles
- Furcation involvement (Class I, II, III) for multi-rooted teeth
- Bone loss percentage
- Color-coded severity (healthy ≥3mm = green, 4-5mm = yellow, ≥6mm = red)
- Trend comparison (current vs previous measurement)

### Treatment Planning
- Multiple treatment plans per patient
- Drag-and-drop to reorder procedures
- Grouping by visit/appointment
- Total fee + insurance estimate + patient portion
- Status tracking: Planned → Scheduled → In Progress → Completed
- Print/export to PDF

### Patient Record Integration
- Chief complaint
- Medical history flags (diabetes, anticoagulants, allergies, etc.) shown as alerts on chart
- Medication list
- Insurance info (primary + secondary)
- Photo attachments (intraoral photos linked to teeth)
- X-ray/radiograph attachments linked to teeth

### Appointment Scheduling (from chart)
- "Schedule" button on treatment plan item
- Opens appointment creation modal
- Provider availability check
- Operatory/chair assignment

## Backend Requirements (.NET 10 AOT)

### AOT Compatibility Rules
- Use `System.Text.Json` with source generation (`[JsonSerializable]`)
- No reflection-based serialization (no `Newtonsoft.Json`)
- Use `[JsonSourceGenerationOptions]` context classes
- EF Core with AOT: use `UseModel()` with compiled models
- Controllers must be minimal API-style or use AOT-compatible Mvc
- Prefer `IResult`-based minimal APIs over MVC controllers for better AOT support

### API Endpoints (minimum)
```
GET    /api/patients                    List patients (paginated, searchable)
POST   /api/patients                    Create patient
GET    /api/patients/{id}               Get patient detail
PUT    /api/patients/{id}               Update patient
DELETE /api/patients/{id}               Soft-delete patient

GET    /api/patients/{id}/chart         Get full dental chart
PUT    /api/patients/{id}/chart/teeth/{number}/conditions  Update tooth condition
DELETE /api/patients/{id}/chart/teeth/{number}/conditions/{condId}

GET    /api/patients/{id}/treatment-plans         List treatment plans
POST   /api/patients/{id}/treatment-plans         Create treatment plan
PUT    /api/patients/{id}/treatment-plans/{planId} Update plan
POST   /api/patients/{id}/treatment-plans/{planId}/items  Add item
PUT    /api/patients/{id}/treatment-plans/{planId}/items/{itemId}
DELETE /api/patients/{id}/treatment-plans/{planId}/items/{itemId}

GET    /api/patients/{id}/perio-charts            List perio charts
POST   /api/patients/{id}/perio-charts            Create perio chart
GET    /api/patients/{id}/perio-charts/{chartId}  Get specific perio chart

GET    /api/appointments                List appointments (date range, provider)
POST   /api/appointments                Create appointment
PUT    /api/appointments/{id}           Update appointment
DELETE /api/appointments/{id}           Cancel appointment

GET    /api/procedure-codes             Search CDT codes
GET    /api/providers                   List providers
POST   /api/auth/login
POST   /api/auth/refresh
POST   /api/auth/logout
```

### Database Schema Requirements
- All tables: `created_at`, `updated_at`, `deleted_at` (soft delete)
- Use UUIDs for primary keys
- Optimistic concurrency with `row_version`/`xmin` via PostgreSQL
- Full audit log table
- Database migrations using EF Core Migrations

## Frontend Requirements (Vue 3)

### Technology Choices
- **State**: Pinia (no Vuex)
- **Routing**: Vue Router 4
- **HTTP**: `axios` with interceptors for JWT
- **UI Library**: Element Plus or Ant Design Vue (pick one and be consistent)
- **SVG**: inline SVG components for the tooth chart (NOT canvas)
- **Charts**: ECharts (for perio trend charts)
- **PDF**: `jsPDF` + `html2canvas`
- **Drag-and-drop**: `vue-draggable-next` for treatment plan reordering
- **Testing**: Vitest + Vue Test Utils

### Component Architecture
```
DentalChartPage.vue              ← Main page, orchestrates everything
  ├── PatientHeader.vue          ← Patient name, DOB, insurance alerts
  ├── ChartToolbar.vue           ← Mode selector, zoom, print button
  ├── ToothChart.vue             ← Main SVG chart container
  │   ├── UpperArch.vue          ← Teeth 1–16 (or 1–10 primary)
  │   ├── LowerArch.vue          ← Teeth 17–32 (or A–T primary)
  │   └── ToothSVG.vue           ← Individual tooth component
  ├── ToothDetailPanel.vue       ← Side panel: selected tooth detail
  │   ├── ConditionList.vue      ← Existing conditions on tooth
  │   └── AddConditionForm.vue   ← Add new condition
  ├── TreatmentPlanPanel.vue     ← Treatment plans tab
  │   ├── TreatmentPlanList.vue  ← List of plans
  │   └── TreatmentPlanEditor.vue← Edit plan items
  └── PerioChartPanel.vue        ← Periodontal chart tab
      ├── PerioGrid.vue          ← The 6-per-tooth grid
      └── PerioChart.vue         ← Visual trend chart
```

### Color Legend (use these exact hex values)
```typescript
export const CONDITION_COLORS = {
  decay: '#E53E3E',        // red
  crown: '#D69E2E',        // gold
  bridge_pontic: '#805AD5', // purple
  missing: '#718096',       // gray
  implant: '#38A169',       // green
  planned: '#3182CE',       // blue
  amalgam: '#4A5568',       // dark gray
  composite: '#F6E05E',     // light yellow
  root_canal: '#E53E3E',    // red (lines)
  sealant: '#81E6D9',       // teal
  veneer: '#FED7E2',        // pink
  extraction_planned: '#FC8181', // light red
  watch: '#F6AD55',          // orange
};
```

## Testing Requirements

### Backend Tests (xUnit)
- Unit tests for domain logic (tooth numbering conversion, surface parsing, etc.)
- Integration tests using Testcontainers for PostgreSQL
- API endpoint tests using `WebApplicationFactory`
- Minimum 80% code coverage

### Frontend Tests (Vitest)
- Unit tests for utility functions
- Component tests for `ToothSVG.vue`, `PerioGrid.vue`, `TreatmentPlanEditor.vue`
- Store tests for Pinia stores

## Code Quality Standards

- **No TODO comments** left in committed code
- **Error handling**: all API calls have proper error handling and user feedback
- **Loading states**: all async operations show loading indicators
- **Accessibility**: ARIA labels on interactive tooth SVG elements
- **i18n-ready**: wrap all user-facing strings (even if only one language now)
- **No console.log** in production code
- Follow C# Microsoft conventions; follow Vue 3 Composition API style guide

## When Implementing an Issue

1. Read the issue title and description carefully
2. Check `docs/tasks/` for the matching detailed specification
3. Create feature branch from `main` named `feature/task-{N}-{short-description}`
4. Implement **all** requirements listed — partial implementation is not acceptable
5. Write unit tests alongside implementation
6. Ensure Docker Compose `up` still works end-to-end
7. Submit PR with:
   - Clear description referencing the issue number (`Closes #N`)
   - Test results screenshot or summary
   - Any architectural decisions made

## Security Requirements

- Never commit secrets, connection strings, or API keys
- Use environment variables (`.env` for local, GitHub Secrets for CI)
- Sanitize all user input
- SQL injection protection via parameterized queries (EF Core handles this)
- XSS protection via Vue's default template escaping
- CORS policy: only allow configured frontend origin
- Rate limiting on auth endpoints
- JWT tokens expire in 1 hour; refresh tokens in 7 days
