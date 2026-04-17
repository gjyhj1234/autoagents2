# 解决 GitHub Actions 工作流审批问题 — 详细操作指南

> **创建日期**: 2026-04-17  
> **最后更新**: 2026-04-17  
> **目标**: 让 `copilot-swe-agent[bot]` 创建的 PR 不再需要人工审批即可运行 CI 工作流

---

## 目录

1. [问题背景](#1-问题背景)
2. [根因分析](#2-根因分析)
3. [已尝试但不充分的方案](#3-已尝试但不充分的方案)
4. [推荐解决方案：修改 Actions 审批设置（最宽松选项）](#4-推荐解决方案修改-actions-审批设置最宽松选项)
5. [补充方案：通过 GitHub API 将 Bot 添加为仓库协作者](#5-补充方案通过-github-api-将-bot-添加为仓库协作者)
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

### 相关 PR 和工作流运行记录

| 工作流 | 触发事件 | 结论 | 说明 |
|--------|----------|------|------|
| 🧪 02 — PR Tests | `pull_request_target` | ❌ `action_required` | 有时成功，有时被阻止 |
| 🏷️ 04 — Label Copilot PRs | `pull_request_target` | ❌ `action_required` | 始终被阻止 |
| 🔓 05 — Auto Approve | `workflow_run` | ❌ `action_required` | 始终被阻止 |
| 🔀 03 — Auto Merge | `workflow_dispatch` | ✅ `success` | 由 COPILOT_PAT 触发，不受影响 |

---

## 2. 根因分析

### GitHub 的安全策略

GitHub Actions 对**公开仓库**有一个安全策略：

> **来自外部贡献者的 PR** 触发的工作流需要仓库维护者审批后才能运行。

这个策略的设置位于：**Settings → Actions → General → "Approval for running fork pull request workflows from contributors"**

### 为什么 `copilot-swe-agent[bot]` 被视为"外部贡献者"

`copilot-swe-agent[bot]` 是 GitHub 平台管理的 App Bot 账户，它：

- **不是**仓库的 Owner 或 Collaborator
- **不是**组织的 Member
- 因此被 GitHub 归类为"external contributor"（外部贡献者）

### 关于 `pull_request_target` 的矛盾

根据 [GitHub 最新官方文档](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/enabling-features-for-your-repository/managing-github-actions-settings-for-a-repository#controlling-changes-from-forks-to-workflows-in-public-repositories)：

> Workflows triggered by `pull_request_target` events are run in the context of the base branch. Since the base branch is considered trusted, **workflows triggered by these events will always run, regardless of approval settings**.

理论上，使用 `pull_request_target` 触发器应该绕过所有审批要求。**但实际测试中，这个绕过并不稳定**——有时有效，有时仍被阻止。这可能与 GitHub 平台对 Bot 账户的特殊处理有关。

---

## 3. 已尝试但不充分的方案

| 方案 | 结果 | 原因 |
|------|------|------|
| 方案 A: 选择 "Require approval for first-time contributors who are new to GitHub" | ❌ 不生效 | Bot 账户可能不适用此分类 |
| 方案 B: 手动批准一次后自动化 | ❌ 不持久 | 每次新 PR 仍然需要审批 |
| 使用 `pull_request_target` | ⚠️ 不稳定 | 文档称应绕过，但实际对 Bot 不一致 |
| API `approveWorkflowRun` | ❌ 无效 | 仅对 fork PR 有效 |

---

## 4. 推荐解决方案：修改 Actions 审批设置（最宽松选项）

> ⚠️ **重要提示**: 修改设置后，需要**重新触发**工作流。旧的 `action_required` 状态的工作流运行**不会**自动变为已批准。

### 步骤 1: 打开仓库 Settings 页面

1. 在浏览器中打开你的仓库主页：
   ```
   https://github.com/gjyhj1234/autoagents2
   ```

2. 点击页面顶部的 **Settings**（设置）标签页
   - 如果看不到 "Settings" 标签，点击 `...` 按钮展开更多选项，然后点击 **Settings**
   - ⚠️ 你必须是仓库 Owner 或有 Admin 权限才能看到 Settings

### 步骤 2: 进入 Actions 设置

1. 在左侧边栏中，找到并点击 **Actions**
2. 在展开的子菜单中，点击 **General**

### 步骤 3: 修改审批策略

1. 向下滚动页面，找到标题为 **"Approval for running fork pull request workflows from contributors"** 的区域

2. 选择 **最宽松的选项**：
   > **"Require approval for first-time contributors who are new to GitHub"**
   >
   > 含义: 仅在贡献者**同时满足以下两个条件**时才需审批：
   > - 是 GitHub 新用户
   > - 从未在此仓库有过合并的 commit 或 PR

3. 点击页面底部的 **Save** 按钮保存设置

### 步骤 4: 重新触发工作流

**重要**：设置变更后，之前已经卡在 `action_required` 的工作流运行**不会自动生效**。你需要：

#### 方法 A: 手动批准现有运行（快速见效）

1. 打开仓库的 Actions 页面：
   ```
   https://github.com/gjyhj1234/autoagents2/actions
   ```

2. 找到状态为 **"Waiting for approval"** 或 **"Action required"** 的工作流运行

3. 点击进入该运行的详情页

4. 在页面右上角或顶部，找到并点击 **"Approve and run"** 按钮

5. 工作流将开始执行

#### 方法 B: 关闭并重新创建 PR（确保新设置生效）

如果批准按钮不起作用或不存在：

1. 进入 PR 页面（例如 PR #36）
2. 滚动到底部，点击 **"Close pull request"**
3. 让 Copilot 重新创建 PR（或手动 reopen）
4. 新创建的 PR 将使用新的审批设置

---

## 5. 补充方案：通过 GitHub API 将 Bot 添加为仓库协作者

如果**方案 4 仍然不生效**，可以尝试将 `copilot-swe-agent[bot]` 添加为仓库协作者。这样 Bot 将不再被归类为"外部贡献者"。

### 5.1 为什么不能通过 UI 添加

GitHub 仓库的 **Settings → Collaborators** 页面的搜索框**无法搜索到 Bot 账户**（如 `copilot-swe-agent[bot]`）。必须通过 GitHub REST API 或 `gh` CLI 工具来添加。

### 5.2 前置准备

你需要一个拥有 `admin` 权限的 Personal Access Token (PAT)。

#### 创建 PAT 的步骤：

如果你已经有 `COPILOT_PAT`（在仓库 Secrets 中配置过的），可以直接使用它。否则，创建一个新的：

1. 打开 GitHub 个人设置：
   ```
   https://github.com/settings/tokens
   ```

2. 点击 **"Generate new token"** → **"Generate new token (classic)"**

3. 填写：
   - **Note**: `Add bot collaborator`
   - **Expiration**: 选择一个合适的过期时间
   - **Scopes**: 勾选 `repo`（包含全部仓库权限）

4. 点击底部的 **"Generate token"**

5. **立即复制生成的 token**（离开页面后无法再次查看）

### 5.3 查找 Bot 的 GitHub 用户 ID

首先需要确认 `copilot-swe-agent[bot]` 的 GitHub 用户信息。

在终端（Terminal / 命令提示符 / PowerShell）中运行：

```bash
curl -s https://api.github.com/users/copilot-swe-agent%5Bbot%5D | python3 -m json.tool
```

或者使用 `gh` CLI：

```bash
gh api /users/copilot-swe-agent%5Bbot%5D
```

> **说明**: `%5B` 和 `%5D` 是 `[` 和 `]` 的 URL 编码。

如果命令返回了用户信息（包含 `id`、`login` 等字段），说明该 Bot 用户存在，可以继续下一步。

### 5.4 通过 API 添加 Bot 为协作者

使用以下命令将 Bot 添加为仓库协作者（`write` 权限）：

#### 使用 curl：

```bash
curl -X PUT \
  -H "Accept: application/vnd.github+json" \
  -H "Authorization: Bearer YOUR_PERSONAL_ACCESS_TOKEN" \
  https://api.github.com/repos/gjyhj1234/autoagents2/collaborators/copilot-swe-agent%5Bbot%5D \
  -d '{"permission":"write"}'
```

> **⚠️ 将 `YOUR_PERSONAL_ACCESS_TOKEN` 替换为你的实际 PAT！**

#### 使用 gh CLI：

```bash
gh api \
  --method PUT \
  -H "Accept: application/vnd.github+json" \
  /repos/gjyhj1234/autoagents2/collaborators/copilot-swe-agent%5Bbot%5D \
  -f permission=write
```

### 5.5 预期结果

- **如果成功** (HTTP 201 或 204): Bot 已被添加为协作者
- **如果返回 404**: Bot 用户名可能不对，尝试其他名称（见下方 5.6）
- **如果返回 422**: GitHub 可能不允许将此类 Bot 添加为协作者

### 5.6 如果 `copilot-swe-agent[bot]` 不可用

Copilot Cloud Agent 的底层 GitHub App 名称可能不同。尝试以下变体：

```bash
# 变体 1: copilot-swe-agent
curl -s https://api.github.com/users/copilot-swe-agent%5Bbot%5D

# 变体 2: copilot
curl -s https://api.github.com/users/copilot%5Bbot%5D

# 变体 3: github-copilot
curl -s https://api.github.com/users/github-copilot%5Bbot%5D

# 变体 4: copilot-coding-agent
curl -s https://api.github.com/users/copilot-coding-agent%5Bbot%5D
```

找到返回有效用户信息的那个名称，然后在步骤 5.4 中使用对应的名称。

### 5.7 验证协作者已添加

```bash
gh api /repos/gjyhj1234/autoagents2/collaborators --jq '.[].login'
```

或在浏览器中查看：

```
https://github.com/gjyhj1234/autoagents2/settings/access
```

---

## 6. 方案生效验证步骤

完成上述设置后，按以下步骤验证是否生效：

### 步骤 1: 确认当前设置

1. 打开 `https://github.com/gjyhj1234/autoagents2/settings/actions`
2. 确认 "Approval for running fork pull request workflows from contributors" 已设为最宽松选项
3. 确认页面已保存

### 步骤 2: 触发新的工作流运行

1. 如果有现存的 Copilot PR（如 PR #36），在 PR 上推送一个空 commit 或让 Copilot 做一个小改动来触发新的工作流运行
2. 或者创建一个新的 Issue（带 `agent-task` 标签）让 Copilot 创建新 PR

### 步骤 3: 检查工作流状态

1. 打开 Actions 页面：`https://github.com/gjyhj1234/autoagents2/actions`
2. 查看最新的工作流运行：
   - ✅ 如果状态为 `queued` → `in_progress` → `success/failure`：**方案生效！**
   - ❌ 如果状态仍为 `action_required`：需要尝试补充方案（见第 5 节）

### 步骤 4: 验证完整流水线

如果单个工作流运行正常，验证完整的自动化链路：

```
Issue (agent-task) → 01 分配 Copilot → Copilot 创建 PR
  → 04 添加标签 + 取消 Draft ✅
  → 02 运行测试 ✅
  → 02 触发 03 (workflow_dispatch) ✅
  → 03 自动合并 ✅
  → 01 处理下一个 Issue ✅
```

---

## 7. FAQ / 常见问题

### Q1: 我在 Settings 里找不到 "Approval for running fork pull request workflows" 选项？

**A**: 这个选项只在**公开仓库 (public repository)** 中显示。如果你的仓库是私有的：
- 转到 **Settings → Actions → General**
- 在 **"Fork pull request workflows"** 区域
- 确保 **"Require approval for fork pull request workflows"** 未勾选
- 也可以考虑将仓库改为 Public（如果是演示项目的话）

### Q2: 为什么 `pull_request_target` 没有按文档所说绕过审批？

**A**: GitHub 官方文档明确指出 `pull_request_target` 触发的工作流应该始终运行，不受审批设置影响。但在实际测试中，对于 Bot 账户创建的 PR，这个绕过**不总是有效**。这可能是：
- GitHub 平台对 Bot/App 账户有额外的安全检查
- 平台行为与文档之间存在差异
- 不同时间点的平台行为可能不一致

### Q3: 手动批准后，后续运行是否自动？

**A**: **不一定**。即使你选择了 "Require approval for first-time contributors" 并且手动批准过一次：
- 对于 **普通用户**: 一旦他们有过合并的 PR，后续不再需要审批
- 对于 **Bot 账户**: 每次可能仍需审批，因为 Bot 的"首次贡献"状态可能不会像普通用户那样更新

### Q4: 添加 Bot 为协作者是否有安全风险？

**A**: 
- `copilot-swe-agent[bot]` 是 GitHub 官方管理的 App，本身是可信的
- 给予 `write` 权限意味着它可以推送代码和管理 PR，这正是 Copilot Cloud Agent 需要的权限
- **对于私有仓库或包含敏感代码的仓库**，请评估风险后再添加

### Q5: 如果所有方案都不行怎么办？

**A**: 最终的兜底方案是使用 `workflow_dispatch` + `repository_dispatch` 完全重新设计工作流触发机制：
- 不依赖 `pull_request` 或 `pull_request_target` 触发器
- 所有工作流都通过 `workflow_dispatch`（由 `COPILOT_PAT` 触发）启动
- 在一个"调度器"工作流中监听 PR 事件，然后通过 API 触发其他工作流

这种方案更复杂但可以完全绕过审批机制，因为 `workflow_dispatch` 和 `repository_dispatch` 不受外部贡献者审批策略的影响。

---

## 参考链接

- [GitHub Docs: Managing GitHub Actions settings for a repository](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/enabling-features-for-your-repository/managing-github-actions-settings-for-a-repository)
- [GitHub Docs: Approving workflow runs from public forks](https://docs.github.com/en/actions/managing-workflow-runs-and-deployments/managing-workflow-runs/approving-workflow-runs-from-public-forks)
- [GitHub Docs: Managing teams and people with access to your repository](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/managing-repository-settings/managing-teams-and-people-with-access-to-your-repository)
- [GitHub Docs: About Copilot Cloud Agent](https://docs.github.com/en/copilot/concepts/agents/cloud-agent/about-cloud-agent)
- [GitHub REST API: Add a repository collaborator](https://docs.github.com/en/rest/collaborators/collaborators#add-a-repository-collaborator)
