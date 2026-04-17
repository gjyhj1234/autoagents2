#!/usr/bin/env bash
# =============================================================================
# setup-github.sh — One-time repository setup for the automated pipeline
#
# USAGE:
#   bash scripts/setup-github.sh
#
# REQUIRES:
#   - GitHub CLI: https://cli.github.com  (gh auth login first)
#   - Must run from the root of the autoagents repository
# =============================================================================

set -euo pipefail

REPO="gjyhj1234/autoagents"
echo "🦷 Setting up GitHub repository: $REPO"
echo ""

# ─── 1. Create Labels ────────────────────────────────────────────────────────
echo "📌 Creating labels..."

create_label() {
  local name="$1" color="$2" description="$3"
  gh label create "$name" --color "$color" --description "$description" \
     --repo "$REPO" 2>/dev/null \
    || gh label edit "$name" --color "$color" --description "$description" \
       --repo "$REPO" 2>/dev/null \
    || echo "  ⚠️  Skipping label: $name"
  echo "  ✅ $name"
}

create_label "agent-task"        "0075ca" "Task ready for Copilot Coding Agent — triggers automation pipeline"
create_label "agent-in-progress" "e4e669" "Copilot Agent is currently working on this issue"
create_label "agent-queued"      "fbca04" "Task is queued and waiting for Copilot to pick it up"
create_label "agent-completed"   "0e8a16" "Agent task completed and merged"
create_label "auto-merge"        "6f42c1" "PR will be auto-merged after all checks pass"
create_label "infrastructure"    "d4c5f9" "Docker, CI/CD, DevOps"
create_label "database"          "c5def5" "Database schema, migrations"
create_label "backend"           "bfd4f2" ".NET 10 backend API"
create_label "frontend"          "d2f5b0" "Vue 3 frontend"
create_label "testing"           "f9d0c4" "Tests and QA"

echo ""

# ─── 2. Enable Auto-Merge on Repository ──────────────────────────────────────
echo "🔀 Enabling auto-merge..."
gh api repos/"$REPO" \
  --method PATCH \
  -f allow_auto_merge=true \
  -f delete_branch_on_merge=true \
  --silent && echo "  ✅ Auto-merge enabled; branches will be deleted after merge"

echo ""

# ─── 3. Create Milestones ────────────────────────────────────────────────────
echo "🎯 Creating milestones..."

create_milestone() {
  local title="$1" description="$2"
  gh api repos/"$REPO"/milestones \
    --method POST \
    -f title="$title" \
    -f description="$description" \
    --silent 2>/dev/null && echo "  ✅ $title" || echo "  ⏭️  $title (may already exist)"
}

create_milestone "Sprint 1 — Infrastructure & Backend"  "Tasks 01–06: project setup, database, all backend APIs"
create_milestone "Sprint 2 — Frontend"                  "Tasks 07–11: Vue frontend, dental chart, perio, treatment"
create_milestone "Sprint 3 — Polish & Testing"          "Task 12: reporting, PDF, integration tests"

echo ""

# ─── 4. Create GitHub Issues (Tasks 01–12) ───────────────────────────────────
echo "📋 Creating task issues..."

create_issue() {
  local number="$1" title="$2" labels="$3" milestone="$4" body_file="$5"
  
  if [ -f "$body_file" ]; then
    gh issue create \
      --repo "$REPO" \
      --title "[Task-$(printf '%02d' $number)] $title" \
      --label "$labels" \
      --milestone "$milestone" \
      --body-file "$body_file" \
    && echo "  ✅ Issue #$number: $title" \
    || echo "  ⚠️  Failed to create issue: $title"
  else
    echo "  ⚠️  Body file not found: $body_file — creating minimal issue"
    gh issue create \
      --repo "$REPO" \
      --title "[Task-$(printf '%02d' $number)] $title" \
      --label "$labels" \
      --milestone "$milestone" \
      --body "See \`docs/tasks/$(printf '%02d' $number)-*.md\` for full specification." \
    && echo "  ✅ Issue #$number: $title"
  fi
}

create_issue 1  "Project Infrastructure Setup"                   "agent-task,infrastructure"      "Sprint 1 — Infrastructure & Backend"  "docs/tasks/01-infrastructure.md"
create_issue 2  "Database Schema and Migrations"                  "agent-task,database"             "Sprint 1 — Infrastructure & Backend"  "docs/tasks/02-database-schema.md"
create_issue 3  "Backend: JWT Authentication and Patient CRUD API" "agent-task,backend"              "Sprint 1 — Infrastructure & Backend"  "docs/tasks/03-backend-auth-patients.md"
create_issue 4  "Backend: Dental Chart and Tooth Conditions API"  "agent-task,backend"              "Sprint 1 — Infrastructure & Backend"  "docs/tasks/04-backend-dental-chart-api.md"
create_issue 5  "Backend: Treatment Planning and Periodontal Chart API" "agent-task,backend"         "Sprint 1 — Infrastructure & Backend"  "docs/tasks/05-backend-treatment-perio.md"
create_issue 6  "Backend: Appointment Scheduling and Procedure Codes API" "agent-task,backend"       "Sprint 1 — Infrastructure & Backend"  "docs/tasks/06-backend-appointments-codes.md"
create_issue 7  "Frontend: Vue 3 Project Setup, Auth, and App Shell" "agent-task,frontend"           "Sprint 2 — Frontend"                   "docs/tasks/07-frontend-setup.md"
create_issue 8  "Frontend: Interactive SVG Dental Chart Component"  "agent-task,frontend"            "Sprint 2 — Frontend"                   "docs/tasks/08-frontend-dental-chart.md"
create_issue 9  "Frontend: Treatment Planning Editor"               "agent-task,frontend"            "Sprint 2 — Frontend"                   "docs/tasks/09-frontend-treatment-plan.md"
create_issue 10 "Frontend: Periodontal Chart Grid and Trend Visualization" "agent-task,frontend"     "Sprint 2 — Frontend"                   "docs/tasks/10-frontend-perio-chart.md"
create_issue 11 "Frontend: Patient Detail, Medical History, and Appointment Scheduling" "agent-task,frontend" "Sprint 2 — Frontend" "docs/tasks/11-frontend-patients-appointments.md"
create_issue 12 "Reporting, PDF Export, and End-to-End Integration Tests" "agent-task,testing"       "Sprint 3 — Polish & Testing"           "docs/tasks/12-reporting-testing.md"

echo ""
echo "🚀 Starting the issue queue workflow..."
workflow_output="$(gh workflow run "01-issue-agent.yml" --repo "$REPO" 2>&1)" \
  && echo "  ✅ Workflow 01 queued successfully" \
  || {
    echo "  ⚠️  Could not start Workflow 01 automatically; run it once from the Actions page"
    echo "     $workflow_output"
  }

# ─── 5. Check for COPILOT_PAT Secret ─────────────────────────────────────────
echo ""
echo "🔑 Checking for COPILOT_PAT secret..."
pat_check=$(gh secret list --repo "$REPO" 2>/dev/null | grep -c "COPILOT_PAT" || true)
if [ "$pat_check" -eq 0 ]; then
  echo "  ⚠️  COPILOT_PAT secret NOT found!"
  echo ""
  echo "  ╔══════════════════════════════════════════════════════════════╗"
  echo "  ║  IMPORTANT: Copilot auto-assignment requires a Personal    ║"
  echo "  ║  Access Token (PAT) stored as the COPILOT_PAT secret.      ║"
  echo "  ║  Without it, you must manually assign Copilot to issues.   ║"
  echo "  ║                                                             ║"
  echo "  ║  See: docs/setup-copilot-pat.md for instructions.          ║"
  echo "  ╚══════════════════════════════════════════════════════════════╝"
  echo ""
else
  echo "  ✅ COPILOT_PAT secret found"
fi

echo ""
echo "============================================================"
echo "✅ Repository setup complete!"
echo ""
echo "Next steps:"
echo "  1. REQUIRED: Create COPILOT_PAT secret if not done yet"
echo "     See docs/setup-copilot-pat.md for step-by-step instructions"
echo "  2. Go to: https://github.com/$REPO/issues"
echo "  3. All 12 issues were created with the 'agent-task' label already applied"
echo "  4. Workflow 01 will use COPILOT_PAT to assign Copilot to the first issue"
echo "  5. Monitor Actions tab: https://github.com/$REPO/actions"
echo "  6. After each PR is merged, the next issue is automatically picked up"
echo "============================================================"
