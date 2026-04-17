# 无人值守自动化流水线 — 需求与进展跟踪

> **创建日期**: 2026-04-17  
> **最后更新**: 2026-04-17  
> **状态**: 🔴 未达成无人值守  

---

## 1. 用户需求

实现完全无人值守的自动化流水线:

1. 创建 Issue（带 `agent-task` 标签）后，自动分配给 Copilot Coding Agent
2. Copilot 自动创建 PR
3. PR 自动标记标签、自动取消 Draft 状态
4. CI 测试自动运行（无需人工审批）
5. 测试通过后自动合并 PR
6. 合并后自动关闭 Issue 并触发下一个任务
7. **全程不需要任何人工干预**

---

## 2. 流水线设计

```
Issue opened (agent-task)
  → [01] Issue Agent: 分配 Copilot
    → Copilot 创建 PR (draft)
      → [04] Label PR: 添加 auto-merge 标签 + 取消 Draft
      → [02] PR Tests: 运行后端/前端测试
        → 设置 commit status (ci/pr-tests)
        → 触发 [03] workflow_dispatch
      → [03] Auto Merge: 检查状态 → squash merge
        → 关闭 Issue + 删除分支
        → 触发 [01] 处理下一个 Issue
```

### 工作流文件

| 编号 | 文件 | 触发方式 | 用途 |
|------|------|----------|------|
| 01 | `01-issue-agent.yml` | `issues`, `workflow_dispatch` | 队列管理 + 分配 Copilot |
| 02 | `02-pr-tests.yml` | `pull_request_target` | 运行 CI 测试 |
| 03 | `03-auto-merge.yml` | `check_suite`, `pull_request_target`, `pull_request_review`, `workflow_dispatch` | 自动合并 |
| 04 | `04-label-pr.yml` | `pull_request_target` | 标记标签 + 取消 Draft |
| 05 | `05-auto-approve.yml` | `workflow_run` | 审批安全网（仅 fork PR 有效） |

---

## 3. 当前阻塞点

### 🔴 问题: 所有工作流卡在 `action_required`（需人工审批）

**关联 PR**: [#29](https://github.com/gjyhj1234/autoagents2/pull/29) — `feat(db): add patients table DDL and seed data`

**现象**: PR #29 由 Copilot (`copilot-swe-agent[bot]`) 创建后，以下工作流全部被挂起为 `action_required`，从未实际执行:

| 工作流 | Run ID | 状态 | 结论 | 时间 |
|--------|--------|------|------|------|
| 🏷️ 04 — Label Copilot PRs | 24565424164 | completed | ❌ action_required | 2026-04-17T12:37:36Z |
| 🧪 02 — PR Tests | 24565423917 | completed | ❌ action_required | 2026-04-17T12:37:35Z |
| 🔓 05 — Auto Approve | 24565425597 | completed | ❌ action_required | 2026-04-17T12:37:38Z |

**后果**:
- PR #29 仍为 Draft 状态（04 未执行，无法取消 Draft）
- CI 测试从未运行（02 未执行）
- 无法触发自动合并（02 → 03 的 dispatch 未发生）
- 整条流水线完全停滞

### 根因分析

GitHub Actions 的安全策略要求：当 PR 由"首次贡献者"或"外部贡献者"触发时，工作流运行前需要仓库维护者手动审批。

`copilot-swe-agent[bot]` 被 GitHub 视为外部贡献者，因此：

1. **即使使用 `pull_request_target` 触发器**（在 base branch 上下文中运行），仍然需要首次审批
2. **`pull_request_target` 绕过的是工作流代码来源的信任问题**（确保运行 main 分支的代码），但**不绕过** Actions 仓库级别的 "Require approval" 设置
3. **REST API `approveWorkflowRun` 只对 fork PR 有效**，对同仓库的 Copilot PR 返回 "This run is not from a fork pull request"

---

## 4. 已尝试的方案

| 方案 | 结果 | 说明 |
|------|------|------|
| 使用 `pull_request_target` 替代 `pull_request` | ❌ 不足够 | 绕过了代码来源信任，但未绕过仓库级审批设置 |
| 工作流 05 调用 `approveWorkflowRun` API | ❌ 无效 | API 仅适用于 fork PR，对同仓库 PR 报错 |
| 使用 `COPILOT_PAT` 提权 | ⚠️ 部分有效 | 用于分配 Copilot 和触发 dispatch，但无法替代 Actions 审批 |

---

## 5. 推荐解决方案

### 方案 A: 修改仓库 Actions 权限设置（推荐 ⭐）

在仓库 **Settings → Actions → General** 页面:

1. 找到 **"Fork pull request workflows from outside collaborators"** 区域
2. 将策略改为 **"Approve and run"** (自动批准，无需手动) 或至少 **"Require approval for first-time contributors only"**（仅首次需要批准）
3. 如果仍不生效，尝试选择 **"Require approval for first-time contributors who are new to GitHub"** (最宽松选项)

> ⚠️ 安全提示: 此设置适用于私有仓库或可信协作者的仓库。公开仓库应谨慎选择。

### 方案 B: 手动批准一次后自动化

1. 进入 [Actions 页面](https://github.com/gjyhj1234/autoagents2/actions)
2. 找到 `action_required` 状态的工作流运行
3. 手动点击 "Approve and run"
4. GitHub 可能在首次批准后，后续运行不再要求审批（取决于仓库设置）

### 方案 C: 将 Copilot bot 添加为仓库协作者

将 `copilot-swe-agent[bot]` 或相关 GitHub App 添加为仓库协作者(Collaborator), 使其不再被视为"外部贡献者"。

### 方案 D: 使用 GitHub App 触发工作流

创建一个自定义 GitHub App 或使用 `repository_dispatch` 事件来绕过审批门槛。workflow_dispatch 已在使用中（02 → 03 的触发），但 02 本身的触发仍依赖 `pull_request_target`。

---

## 6. 任务完成情况

| Issue | 任务 | 状态 | PR | 说明 |
|-------|------|------|----|------|
| [#24](https://github.com/gjyhj1234/autoagents2/issues/24) | Task-01: Database Schema | 🟡 PR 已创建 | [#29](https://github.com/gjyhj1234/autoagents2/pull/29) | Copilot 已完成代码，但 CI/合并 被阻塞 |
| [#25](https://github.com/gjyhj1234/autoagents2/issues/25) | Task-02: Backend API | ⏳ 排队中 | — | 等待 Task-01 完成 |
| [#26](https://github.com/gjyhj1234/autoagents2/issues/26) | Task-03: Backend Tests | ⏳ 排队中 | — | 依赖 Task-02 |
| [#27](https://github.com/gjyhj1234/autoagents2/issues/27) | Task-04: Frontend | ⏳ 排队中 | — | 依赖 Task-02 |
| [#28](https://github.com/gjyhj1234/autoagents2/issues/28) | Task-05: Frontend Tests | ⏳ 排队中 | — | 依赖 Task-04 |

---

## 7. 变更历史

| 日期 | 变更内容 |
|------|----------|
| 2026-04-17 | 初始版本：记录流水线设计、PR #29 阻塞现象及根因分析 |

---

## 8. 备注

- 本文件用于持续跟踪无人值守自动化的实现进展
- 每次遇到新阻塞或解决问题后，应更新此文件
- 工作流源码位于 `.github/workflows/` 目录下
- `COPILOT_PAT` secret 用于提权操作（分配 Agent、触发 dispatch、取消 Draft、合并 PR）
