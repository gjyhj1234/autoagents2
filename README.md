# Patient Management Demo

基于 .NET 10 AOT + Vue 3 + TypeScript + PostgreSQL 的患者增删改查演示项目。

## Tech Stack

- **Backend**: .NET 10 Native AOT Minimal API + Npgsql
- **Frontend**: Vue 3 + TypeScript + Vite + Element Plus + Pinia
- **Database**: PostgreSQL 16

## Automated Pipeline

Issues labeled `agent-task` are automatically assigned to Copilot Coding Agent. After PR tests pass, PRs are auto-merged and the next issue is picked up.

Pipeline: `Issue → Copilot Agent → PR → Tests → Auto-Merge → Next Issue`

## Setup

1. Set repository secret `COPILOT_PAT` (a PAT with repo scope from a user with Copilot access)
2. Run `bash scripts/setup-github.sh` to create labels and issues
3. The pipeline will start automatically