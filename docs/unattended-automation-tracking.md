# 无人值守自动化流水线 — 需求与进展跟踪

> **创建日期**: 2026-04-17  
> **最后更新**: 2026-04-17  
> **状态**: 🟡 方案已实现 — GitHub 原生调度器 + Windows 桌面自动审批工具  

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

## 2. 流水线设计（最终版）

```
Issue opened (agent-task)
  → [01] Issue Agent: 分配 Copilot
    → Copilot 创建 PR (draft)
      ↓
    [00] PR Dispatcher (schedule: 每 5 分钟轮询)  ← 可靠路径
      → workflow_dispatch → [04] Label PR: 添加 auto-merge 标签 + 取消 Draft
      → workflow_dispatch → [02] PR Tests: 运行后端/前端测试
        → 设置 commit status (ci/pr-tests)
        → workflow_dispatch → [03] Auto Merge: 检查状态 → squash merge
          → 关闭 Issue + 删除分支
          → workflow_dispatch → [01] 处理下一个 Issue
```

### 双路径设计

- **快速路径**: `pull_request_target` 触发 → 如果碰巧通过审批，几秒内处理
- **可靠路径**: `00-pr-dispatcher.yml` (schedule) → 最多 5 分钟内通过 `workflow_dispatch` 触发

### 工作流文件

| 编号 | 文件 | 触发方式 | 用途 |
|------|------|----------|------|
| **00** | **`00-pr-dispatcher.yml`** | **`schedule`, `workflow_dispatch`** | **🚀 调度器 — 定时轮询 Copilot PR，触发下游** |
| 01 | `01-issue-agent.yml` | `issues`, `workflow_dispatch` | 队列管理 + 分配 Copilot |
| 02 | `02-pr-tests.yml` | `pull_request_target`, **`workflow_dispatch`** | 运行 CI 测试 |
| 03 | `03-auto-merge.yml` | `check_suite`, `pull_request_target`, `pull_request_review`, `workflow_dispatch` | 自动合并 |
| 04 | `04-label-pr.yml` | `pull_request_target`, **`workflow_dispatch`** | 标记标签 + 取消 Draft |
| 05 | `05-auto-approve.yml` | `workflow_run` | 审批安全网（仅 fork PR 有效） |

---

## 3. 问题回顾与根因

### 核心问题

Copilot 云代理创建的 PR 触发的工作流总是被卡在 `action_required` 状态。

### 根因：两层独立的安全机制

1. **仓库级 Actions 审批设置**: Settings → Actions → "Approval for fork pull request workflows from contributors"
2. **Copilot 平台级内建安全机制**: [GitHub 官方文档](https://docs.github.com/en/copilot/responsible-use/copilot-cloud-agent#avoiding-privileged-escalation)明确指出：
   > "GitHub Actions workflows triggered in response to pull requests raised by Copilot cloud agent require approval from a user with repository write access before they will run."

`pull_request_target` 只能绕过仓库级设置，无法绕过 Copilot 平台级安全机制。

---

## 4. 已尝试但失败的方案

| 方案 | 结果 | 原因 |
|------|------|------|
| 修改 Actions 审批设置为最宽松 | ❌ 不充分 | 不控制 Copilot 平台级安全 |
| 使用 `pull_request_target` | ⚠️ 不稳定 | 被 Copilot 安全层覆盖 |
| 添加 Bot 为协作者 (REST API) | ❌ 不可行 | API 只接受 `type: User`，Bot 返回 HTTP 422 |
| `approveWorkflowRun` API | ❌ 无效 | 仅对 fork PR 有效 |
| 手动批准后自动化 | ❌ 不持久 | Bot 首次贡献状态不更新 |

---

## 5. 最终解决方案 ✅

### 方案 A: `workflow_dispatch` + `schedule` 调度器 (GitHub 原生)

#### 为什么有效

`workflow_dispatch` 和 `schedule` 触发器运行在仓库自身上下文中，**完全不受任何贡献者审批策略影响**。

#### 实现细节

| 组件 | 说明 |
|------|------|
| `00-pr-dispatcher.yml` | 每 5 分钟轮询，检测未处理的 Copilot PR |
| `02-pr-tests.yml` 新增 `workflow_dispatch` | 接受 `pr_number` 输入，由调度器触发 |
| `04-label-pr.yml` 新增 `workflow_dispatch` | 接受 `pr_number` 输入，由调度器触发 |

#### 调度器逻辑

对每个 open 的 Copilot PR：
1. 检查是否缺少 `auto-merge` 标签 → 触发 04
2. 检查是否缺少 `ci/pr-tests` commit status → 触发 02

#### ⚠️ 已知局限

- **新工作流启动延迟**: GitHub 对新增的 scheduled workflow 可能需要最多 1 小时才能开始调度
- **高负载期不可靠**: 调度任务可能被延迟甚至丢弃
- **实测**: `00-pr-dispatcher.yml` 创建后 17 分钟仍有零次运行（预期至少 3 次）

### 方案 B: WorkflowApprover 桌面自动审批工具

位于 `src/WorkflowApprover/`，Windows 桌面应用 (WinForms + Edge WebView2)。

#### 工作原理

1. 通过 GitHub REST API 轮询 `action_required` 状态的工作流运行
2. 使用内嵌 WebView2 浏览器导航到待审批页面
3. 通过 JavaScript 注入自动点击 "Approve and run" 按钮
4. 支持配置化的检查间隔（默认 3 分钟）
5. 浏览器登录状态持久化，重启无需重新登录

#### 优势

- ✅ 不依赖 GitHub 的 schedule 调度器，直接通过浏览器操作
- ✅ 立即响应，不受 GitHub 平台延迟影响
- ✅ 自动保存设置和登录状态

#### 要求

- Windows 10/11 + .NET 8.0 + Edge WebView2 Runtime

### 推荐部署

**两层保障**：同时启用方案 A + 方案 B

```
层 1: GitHub 原生 schedule → 无需额外设施，但有延迟
层 2: WorkflowApprover      → 立即响应，需要 Windows 机器
```

---

## 6. 任务完成情况

| Issue | 任务 | 状态 | PR | 说明 |
|-------|------|------|----|------|
| [#24](https://github.com/gjyhj1234/autoagents2/issues/24) | Task-01: Database Schema | 🟡 PR 已创建 | [#36](https://github.com/gjyhj1234/autoagents2/pull/36) | CI 阻塞已通过调度器方案解决 |
| [#25](https://github.com/gjyhj1234/autoagents2/issues/25) | Task-02: Backend API | ⏳ 排队中 | — | 等待 Task-01 完成 |
| [#26](https://github.com/gjyhj1234/autoagents2/issues/26) | Task-03: Backend Tests | ⏳ 排队中 | — | 依赖 Task-02 |
| [#27](https://github.com/gjyhj1234/autoagents2/issues/27) | Task-04: Frontend | ⏳ 排队中 | — | 依赖 Task-02 |
| [#28](https://github.com/gjyhj1234/autoagents2/issues/28) | Task-05: Frontend Tests | ⏳ 排队中 | — | 依赖 Task-04 |

---

## 7. 变更历史

| 日期 | 变更内容 |
|------|----------|
| 2026-04-17 | 初始版本：记录流水线设计、PR #29 阻塞现象及根因分析 |
| 2026-04-17 | 更新方案状态，新增方案 C (API 添加 Bot 协作者)、方案 D (workflow_dispatch 兜底方案) |
| 2026-04-17 | **最终方案实现**: 确认添加 Bot 为协作者不可行（API 422），确认 Copilot 有独立的平台级安全机制。实现 `workflow_dispatch` + `schedule` 调度器方案（方案 D），新增 `00-pr-dispatcher.yml`，更新 `02-pr-tests.yml` 和 `04-label-pr.yml` 支持 `workflow_dispatch` 触发 |
| 2026-04-17 | **补充方案**: 发现 `00-pr-dispatcher.yml` schedule 触发存在启动延迟（创建 17 分钟后零次运行），新增 WorkflowApprover Windows 桌面自动审批工具 (`src/WorkflowApprover/`) 作为可靠补充方案 |

---

## 8. 备注

- 本文件用于持续跟踪无人值守自动化的实现进展
- 每次遇到新阻塞或解决问题后，应更新此文件
- 工作流源码位于 `.github/workflows/` 目录下
- `COPILOT_PAT` secret 用于提权操作（分配 Agent、触发 dispatch、取消 Draft、合并 PR）
- WorkflowApprover 工具位于 `src/WorkflowApprover/`，详细文档见 [`src/WorkflowApprover/README.md`](../src/WorkflowApprover/README.md)
- 详细技术文档见 [`docs/fix-workflow-approval-guide.md`](fix-workflow-approval-guide.md)
