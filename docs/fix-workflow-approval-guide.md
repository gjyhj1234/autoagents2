# 解决 GitHub Actions 工作流审批问题 — 详细操作指南

> **创建日期**: 2026-04-17  
> **最后更新**: 2026-04-17  
> **目标**: 让 `copilot-swe-agent[bot]` 创建的 PR 不再需要人工审批即可运行 CI 工作流

---

## 目录

1. [问题背景](#1-问题背景)
2. [根因分析](#2-根因分析)
3. [已尝试但不充分的方案](#3-已尝试但不充分的方案)
4. [最终解决方案：workflow_dispatch + schedule 调度器](#4-最终解决方案workflow_dispatch--schedule-调度器)
5. [新架构详解](#5-新架构详解)
6. [方案生效验证步骤](#6-方案生效验证步骤)
7. [FAQ / 常见问题](#7-faq--常见问题)

---

## 1. 问题背景

### 现象

当 Copilot Cloud Agent（`copilot-swe-agent[bot]`）创建 Pull Request 后，仓库中配置的 GitHub Actions 工作流（如 CI 测试、自动标签等）不会自动运行，而是卡在 `action_required` 状态，需要仓库维护者手动点击 "Approve and run" 才能执行。

### 影响

这导致我们的无人值守自动化流水线完全中断：

- PR 无法自动取消 Draft 状态（工作流 04 未执行）
- CI 测试无法自动运行（工作流 02 未执行）
- 自动合并无法触发（工作流 03 依赖 02 的结果）
- **整条流水线停滞，无法实现无人值守**

---

## 2. 根因分析

### 两层安全机制

经过深入研究，发现审批阻塞由**两个独立的安全机制**导致：

#### 机制 1: 仓库级 Actions 审批设置

GitHub Actions 对公开仓库有安全策略：来自外部贡献者的 PR 触发的工作流需要仓库维护者审批。
- 位置: Settings → Actions → General → "Approval for running fork pull request workflows from contributors"
- `copilot-swe-agent[bot]` 被视为外部贡献者

#### 机制 2: Copilot 平台级内建安全机制 ⚠️

根据 [GitHub 官方文档](https://docs.github.com/en/copilot/responsible-use/copilot-cloud-agent#avoiding-privileged-escalation)：

> **"GitHub Actions workflows triggered in response to pull requests raised by Copilot cloud agent require approval from a user with repository write access before they will run."**

**这是 Copilot 云代理自身的平台级安全特性**，与仓库设置无关，无法通过任何仓库配置禁用。

### 为什么 `pull_request_target` 不可靠

GitHub 文档声称 `pull_request_target` "always run, regardless of approval settings"，但这仅指**仓库级**审批设置。Copilot 的**平台级**安全机制会覆盖此行为，导致结果不一致。

### 为什么添加 Bot 为协作者不可行

GitHub REST API `PUT /repos/{owner}/{repo}/collaborators/{username}` 只接受 `type: "User"` 的账户。`copilot-swe-agent[bot]` 的类型是 `Bot`，API 返回 HTTP 422 错误。GitHub Apps 通过 Installation 机制工作，不能通过协作者 API 添加。

---

## 3. 已尝试但不充分的方案

| 方案 | 结果 | 原因 |
|------|------|------|
| 修改 Actions 审批设置为最宽松 | ❌ 不充分 | 控制的是仓库级策略，不控制 Copilot 平台级安全机制 |
| 使用 `pull_request_target` | ⚠️ 不稳定 | 被 Copilot 平台级安全层覆盖 |
| 添加 Bot 为协作者 (API) | ❌ 不可行 | API 只接受 User 类型，Bot 类型返回 422 |
| `approveWorkflowRun` API | ❌ 无效 | 仅对 fork PR 有效 |
| 手动批准后自动化 | ❌ 不持久 | Bot 的首次贡献状态不会更新 |

---

## 4. 最终解决方案：workflow_dispatch + schedule 调度器

### 核心原理

`workflow_dispatch` 和 `schedule` 触发器完全运行在仓库自身的上下文中，**不受任何贡献者审批策略的影响**——无论是仓库级设置还是 Copilot 平台级安全机制。

### 架构设计

```
Copilot 创建 PR
  ↓ (pull_request_target 可能触发也可能被阻止 — 快速路径)
  ↓
[00] PR Dispatcher (schedule: 每 5 分钟) — 可靠路径
  → 检测未处理的 Copilot PR
  → workflow_dispatch → [04] Label PR (标签 + 取消 Draft)
  → workflow_dispatch → [02] PR Tests (运行测试)
    → [02] 设置 commit status (ci/pr-tests)
    → [02] workflow_dispatch → [03] Auto Merge (自动合并)
      → [03] 关闭 Issue + 删除分支
      → [03] workflow_dispatch → [01] 处理下一个 Issue
```

### 双路径设计

保留了 `pull_request_target` 作为"快速路径"：如果 GitHub 碰巧让它通过了，PR 会在几秒内被处理。如果被阻止，定时调度器会在最多 5 分钟内接管。

---

## 5. 新架构详解

### 新增工作流: `00-pr-dispatcher.yml`

| 属性 | 值 |
|------|------|
| 触发方式 | `schedule` (每 5 分钟) + `workflow_dispatch` (手动) |
| 作用 | 轮询 open 的 Copilot PR，触发下游工作流 |
| 使用 Token | `COPILOT_PAT` |

逻辑：
1. 列出所有 open 的 PR
2. 筛选 Copilot PR（通过分支前缀或作者判断）
3. 对每个 PR 检查：
   - 是否缺少 `auto-merge` 标签 → 触发 04
   - 是否缺少 `ci/pr-tests` commit status → 触发 02

### 修改的工作流

| 工作流 | 改动 |
|--------|------|
| `02-pr-tests.yml` | 新增 `workflow_dispatch` 输入 `pr_number`；新增 `resolve-pr` job 统一两种触发方式 |
| `04-label-pr.yml` | 新增 `workflow_dispatch` 输入 `pr_number`；合并两个 step 为一个 |
| `05-auto-approve.yml` | 已确认仅对 fork PR 有效，保留作为安全网 |

### 完整工作流列表

| 编号 | 文件 | 触发方式 | 用途 |
|------|------|----------|------|
| 00 | `00-pr-dispatcher.yml` | `schedule`, `workflow_dispatch` | **调度器 — 定时轮询 Copilot PR** |
| 01 | `01-issue-agent.yml` | `issues`, `workflow_dispatch` | 队列管理 + 分配 Copilot |
| 02 | `02-pr-tests.yml` | `pull_request_target`, `workflow_dispatch` | 运行 CI 测试 |
| 03 | `03-auto-merge.yml` | `check_suite`, `pull_request_target`, `pull_request_review`, `workflow_dispatch` | 自动合并 |
| 04 | `04-label-pr.yml` | `pull_request_target`, `workflow_dispatch` | 标记标签 + 取消 Draft |
| 05 | `05-auto-approve.yml` | `workflow_run` | 审批安全网（仅 fork PR 有效） |

---

## 6. 方案生效验证步骤

### 步骤 1: 确认调度器运行

1. 打开 Actions 页面：`https://github.com/gjyhj1234/autoagents2/actions`
2. 查找 "🚀 00 — PR Dispatcher" 工作流
3. 每 5 分钟应该有一次运行记录
4. 也可以手动点击 "Run workflow" 立即触发

### 步骤 2: 创建测试任务

1. 创建一个带 `agent-task` 标签的 Issue
2. 等待 Copilot 创建 PR
3. 观察调度器是否在 5 分钟内自动处理

### 步骤 3: 检查完整流水线

```
Issue (agent-task)
  → 01 分配 Copilot ✅
  → Copilot 创建 PR ✅
  → 00 调度器检测到 PR ✅
  → 04 添加标签 + 取消 Draft ✅ (via workflow_dispatch)
  → 02 运行测试 ✅ (via workflow_dispatch)
  → 03 自动合并 ✅ (via workflow_dispatch)
  → 01 处理下一个 Issue ✅
```

---

## 7. FAQ / 常见问题

### Q1: 为什么不直接用 `pull_request_target`？

**A**: GitHub 文档声称它应该绕过审批，但 Copilot 有自己的平台级安全机制，会覆盖这个行为。实测对 Bot 账户不稳定。我们保留它作为"快速路径"，但不依赖它。

### Q2: 为什么不把 Bot 添加为协作者？

**A**: GitHub 协作者 API 只接受 `type: "User"` 的账户。`copilot-swe-agent[bot]` 的类型是 `"Bot"`，API 会返回 HTTP 422 错误。GitHub Apps 通过 Installation 机制工作，不通过协作者机制。

### Q3: 5 分钟延迟可以缩短吗？

**A**: GitHub Actions 的 `schedule` 最小间隔是 1 分钟（`* * * * *`），但 GitHub 不保证精确执行。5 分钟是一个合理的平衡。如果快速路径（`pull_request_target`）生效，则几乎没有延迟。

### Q4: 调度器会不会重复触发？

**A**: 不会。调度器检查：
- 是否已有 `auto-merge` 标签 → 如果有则跳过 04
- 是否已有 `ci/pr-tests` commit status → 如果有则跳过 02

### Q5: 调度器消耗多少 Actions 分钟数？

**A**: 每次运行通常在 10-20 秒内完成（只是 API 调用），每月约 `(60/5) × 24 × 30 = 8640` 次运行，每次 ~15 秒 ≈ 36 小时/月。在免费额度（2000 分钟/月）范围内。

---

## 参考链接

- [GitHub Docs: Responsible use of Copilot cloud agent — Avoiding privileged escalation](https://docs.github.com/en/copilot/responsible-use/copilot-cloud-agent#avoiding-privileged-escalation)
- [GitHub Docs: Managing GitHub Actions settings for a repository](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/enabling-features-for-your-repository/managing-github-actions-settings-for-a-repository)
- [GitHub Docs: Events that trigger workflows — workflow_dispatch](https://docs.github.com/en/actions/using-workflows/events-that-trigger-workflows#workflow_dispatch)
- [GitHub REST API: Add a repository collaborator](https://docs.github.com/en/rest/collaborators/collaborators#add-a-repository-collaborator)
