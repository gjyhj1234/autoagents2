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

### 🔴 问题: 工作流对 Bot PR 的审批不稳定

**关联 PR**: [#36](https://github.com/gjyhj1234/autoagents2/pull/36) — `feat(db): add patients table DDL and seed data`

**最新发现** (2026-04-17):

根据 [GitHub 官方文档](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/enabling-features-for-your-repository/managing-github-actions-settings-for-a-repository#controlling-changes-from-forks-to-workflows-in-public-repositories)，`pull_request_target` 触发的工作流应该 **"always run, regardless of approval settings"**，但实际对 Bot 账户不一致：

| 工作流 | 触发事件 | 运行 1 结果 | 运行 2 结果 | 说明 |
|--------|----------|------------|------------|------|
| 🧪 02 — PR Tests | `pull_request_target` | ✅ success | ❌ action_required | 不一致 |
| 🏷️ 04 — Label Copilot PRs | `pull_request_target` | ❌ action_required | ❌ action_required | 始终被阻止 |
| 🔓 05 — Auto Approve | `workflow_run` | ❌ action_required | ❌ action_required | 始终被阻止 |
| 🔀 03 — Auto Merge | `workflow_dispatch` | ✅ success | — | 不受影响 |

**关键结论**: `pull_request_target` 对 `copilot-swe-agent[bot]` 的审批绕过**不可靠**。需要通过添加 Bot 为协作者或其他方案解决。

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

> 📖 **详细操作指南**：请参阅 [`docs/fix-workflow-approval-guide.md`](fix-workflow-approval-guide.md)，其中包含每个方案的完整步骤和截图说明。

### 方案 A: 修改仓库 Actions 权限设置 ⭐

在仓库 **Settings → Actions → General** 页面:

1. 找到 **"Approval for running fork pull request workflows from contributors"** 区域
2. 选择 **"Require approval for first-time contributors who are new to GitHub"** (最宽松选项)
3. 点击 **Save**
4. ⚠️ **重要**: 修改设置后，旧的 `action_required` 运行**不会自动生效**，需要重新触发

**状态**: ❌ 已尝试，对 Bot 账户不稳定生效

### 方案 B: 手动批准一次后自动化

1. 进入 [Actions 页面](https://github.com/gjyhj1234/autoagents2/actions)
2. 找到 `action_required` 状态的工作流运行
3. 手动点击 "Approve and run"
4. GitHub 可能在首次批准后，后续运行不再要求审批（取决于仓库设置）

**状态**: ❌ 已尝试，Bot 的"首次贡献"状态不会像普通用户那样更新

### 方案 C: 将 Copilot bot 添加为仓库协作者 ⭐⭐ (当前推荐)

将 `copilot-swe-agent[bot]` 添加为仓库协作者(Collaborator)，使其不再被视为"外部贡献者"。

**操作方法**: UI 无法搜索到 Bot 账户，需通过 GitHub REST API 添加:

```bash
# 1. 确认 Bot 用户存在
curl -s https://api.github.com/users/copilot-swe-agent%5Bbot%5D

# 2. 添加为协作者 (write 权限)
curl -X PUT \
  -H "Authorization: Bearer YOUR_PAT" \
  https://api.github.com/repos/gjyhj1234/autoagents2/collaborators/copilot-swe-agent%5Bbot%5D \
  -d '{"permission":"write"}'
```

> 📖 完整步骤见 [`docs/fix-workflow-approval-guide.md` 第 5 节](fix-workflow-approval-guide.md#5-补充方案通过-github-api-将-bot-添加为仓库协作者)

**状态**: ⏳ 待尝试

### 方案 D: 使用 `workflow_dispatch` 重新设计触发机制 (兜底方案)

如果方案 A/B/C 均不生效，将所有工作流改为 `workflow_dispatch` 触发：

- 创建一个"调度器"工作流，通过 `COPILOT_PAT` 触发其他工作流
- 不再依赖 `pull_request` 或 `pull_request_target` 触发器
- `workflow_dispatch` 不受外部贡献者审批策略影响

**状态**: ⏳ 备选方案

---

## 6. 任务完成情况

| Issue | 任务 | 状态 | PR | 说明 |
|-------|------|------|----|------|
| [#24](https://github.com/gjyhj1234/autoagents2/issues/24) | Task-01: Database Schema | 🟡 PR 已创建 | [#36](https://github.com/gjyhj1234/autoagents2/pull/36) | 替代 PR #29（已关闭），CI 仍被部分阻塞 |
| [#25](https://github.com/gjyhj1234/autoagents2/issues/25) | Task-02: Backend API | ⏳ 排队中 | — | 等待 Task-01 完成 |
| [#26](https://github.com/gjyhj1234/autoagents2/issues/26) | Task-03: Backend Tests | ⏳ 排队中 | — | 依赖 Task-02 |
| [#27](https://github.com/gjyhj1234/autoagents2/issues/27) | Task-04: Frontend | ⏳ 排队中 | — | 依赖 Task-02 |
| [#28](https://github.com/gjyhj1234/autoagents2/issues/28) | Task-05: Frontend Tests | ⏳ 排队中 | — | 依赖 Task-04 |

---

## 7. 变更历史

| 日期 | 变更内容 |
|------|----------|
| 2026-04-17 | 初始版本：记录流水线设计、PR #29 阻塞现象及根因分析 |
| 2026-04-17 | 更新方案状态，新增方案 C (API 添加 Bot 协作者) 详细步骤，新增方案 D (workflow_dispatch 兜底方案)，添加 `fix-workflow-approval-guide.md` 详细指南链接 |

---

## 8. 备注

- 本文件用于持续跟踪无人值守自动化的实现进展
- 每次遇到新阻塞或解决问题后，应更新此文件
- 工作流源码位于 `.github/workflows/` 目录下
- `COPILOT_PAT` secret 用于提权操作（分配 Agent、触发 dispatch、取消 Draft、合并 PR）
