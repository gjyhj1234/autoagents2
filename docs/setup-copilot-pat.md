# 自动化流水线完整设置指南

> **本文档是仓库全自动化流水线（Issue → Copilot Agent → PR → Tests → Merge → 下一个 Issue）
> 的完整操作手册。**  
> 面向零基础用户，每一步都有截图位置说明和验证方法。  
> 最后更新：2026-04-17（第 4 版，结合 GitHub 最新行为修订）

---

## 📋 目录

1. [整体架构图](#1-整体架构图)
2. [前提条件总清单](#2-前提条件总清单)
3. [步骤 A：启用 Copilot Coding Agent](#3-步骤-a启用-copilot-coding-agent)
4. [步骤 B：创建 Personal Access Token (PAT)](#4-步骤-b创建-personal-access-token-pat)
5. [步骤 C：将 PAT 存为仓库 Secret](#5-步骤-c将-pat-存为仓库-secret)
6. [步骤 D：配置仓库 Actions 权限](#6-步骤-d配置仓库-actions-权限)
7. [步骤 E：解决 "Awaiting approval from a maintainer"](#7-步骤-e解决-awaiting-approval-from-a-maintainer)
8. [步骤 F：启用 Auto-Merge](#8-步骤-f启用-auto-merge)
9. [验证：端到端测试流水线](#9-验证端到端测试流水线)
10. [场景处理：卡住的 Issue / PR 如何修复](#10-场景处理卡住的-issue--pr-如何修复)
11. [常见问题 (FAQ)](#11-常见问题-faq)
12. [快速排查清单](#12-快速排查清单)

---

## 1. 整体架构图

```
┌─────────────────── 自动化流水线 ───────────────────┐
│                                                     │
│  ① Issue (agent-task 标签)                          │
│     │                                               │
│     ▼                                               │
│  ② Workflow 01: 自动 Assign Copilot (需要 PAT)      │
│     │                                               │
│     ▼                                               │
│  ③ Copilot Coding Agent: 编写代码 → 创建 Draft PR   │
│     │                                               │
│     ▼                                               │
│  ④ Workflow 04: 给 PR 添加 auto-merge 标签          │
│     │                                               │
│     ▼                                               │
│  ⑤ Workflow 02: 运行测试 ← ⚠️ 可能需要手动批准!     │
│     │                                               │
│     ▼                                               │
│  ⑥ Workflow 03: 自动 Merge → 关闭 Issue             │
│     │                                               │
│     ▼                                               │
│  ⑦ 触发 Workflow 01 → 分配下一个 Issue → 循环       │
│                                                     │
└─────────────────────────────────────────────────────┘
```

### ⚠️ 当前无法 100% 全自动化的环节

**第 ⑤ 步**是目前唯一可能需要人工介入的环节。

GitHub 出于安全考虑，对来自外部贡献者（包括 bot）的 PR 触发的工作流实行审批机制。
Copilot Coding Agent 以 `Copilot` 身份（bot 账户）创建 PR，GitHub 将其视为
"outside collaborator" 或 "first-time contributor"，因此 `on: pull_request`
触发的 Workflow（如本仓库的 **Workflow 02 — PR Auto Tests**）会显示：

> **"This workflow is awaiting approval from a maintainer"**

你需要在 PR 页面上手动点击 **Approve and run workflows** 一次来放行。

**以下设置可以减少（甚至消除）这个人工步骤，但需要理解安全风险：**

---

## 2. 前提条件总清单

在开始之前，请逐项确认（✅ 打勾 = 已完成）：

| #  | 前提条件                                           | 检查方法                                  |
|----|--------------------------------------------------|------------------------------------------|
| 1  | 你拥有仓库的 **Admin** 权限                         | 能访问 `Settings` 标签页                    |
| 2  | 你有 **GitHub Copilot** 订阅（Pro / Business / Enterprise） | 右上角头像 → Copilot 图标亮着              |
| 3  | **Copilot Coding Agent** 已启用                    | [步骤 A](#3-步骤-a启用-copilot-coding-agent) 检查 |
| 4  | 已创建 **Personal Access Token (PAT)**             | [步骤 B](#4-步骤-b创建-personal-access-token-pat) |
| 5  | PAT 已保存为仓库 Secret `COPILOT_PAT`              | [步骤 C](#5-步骤-c将-pat-存为仓库-secret)    |
| 6  | Actions 权限设为 **Read and write**                 | [步骤 D](#6-步骤-d配置仓库-actions-权限)     |
| 7  | 理解 "Awaiting approval" 及处理方案                 | [步骤 E](#7-步骤-e解决-awaiting-approval-from-a-maintainer) |
| 8  | 仓库已启用 **Allow auto-merge**                     | [步骤 F](#8-步骤-f启用-auto-merge)          |

---

## 3. 步骤 A：启用 Copilot Coding Agent

Copilot Coding Agent（又称 Copilot Cloud Agent）是 GitHub 提供的 AI 编码代理。
它能读取 Issue，编写代码，提交 PR。

### 个人仓库

1. 点击 GitHub 页面右上角你的 **头像** → **Settings**
2. 左侧菜单点击 **Copilot**
3. 找到 **Copilot coding agent**（或 **Cloud agent**）区域
4. 确认开关已打开（Enabled）
5. 在 **Repository access** 中，确认 `autoagents` 仓库已在允许列表中
   - 如果选择了 "All repositories"，则自动包含
   - 如果选择了 "Only select repositories"，需要手动添加

### 组织仓库

1. 打开组织页面 → **Settings**
2. 左侧菜单 → **Copilot** → **Policies** 或 **Coding agent**
3. 确认 Coding agent 已启用
4. 确认 Repository access 包含目标仓库

### 验证方法

打开任意 Issue → 看右侧边栏 **Assignees** 区域：
- 如果能看到 **Assign to Agent** 下拉选项中有 **Copilot**，说明已启用
- 如果看不到这个选项，说明 Copilot Coding Agent 未正确启用

> **注意**：只有仓库 Admin 或有 triage 权限的用户才能看到 Assign to Agent。

---

## 4. 步骤 B：创建 Personal Access Token (PAT)

### 为什么需要 PAT？

GitHub Actions 内置的 `GITHUB_TOKEN` 是一个 **安装令牌 (installation token)**，
**不是**用户令牌，有两个关键限制：

1. **不能分配 Copilot Agent** — GitHub 的 Copilot 分配 API 要求用户身份认证
2. **触发的事件不会启动其他 Workflow** — 防止无限循环

根据 [GitHub 官方文档](https://docs.github.com/en/copilot/how-tos/use-copilot-agents/cloud-agent/create-a-pr#assigning-an-issue-to-copilot-via-the-github-api)：

> Make sure you're authenticating with the API using a **user token**,
> for example a personal access token or a GitHub App user-to-server token.

### 方案 1（推荐）：Fine-grained Personal Access Token

Fine-grained PAT 权限更精确，遵循最小权限原则。

1. 打开浏览器，访问：  
   👉 <https://github.com/settings/personal-access-tokens/new>

2. 填写表单：

   | 字段              | 值                                     |
   |-------------------|----------------------------------------|
   | **Token name**    | `copilot-autoagents`（任意名称）          |
   | **Expiration**    | `90 days`（推荐）或 `Custom...` 最长 1 年 |
   | **Description**   | `用于自动化流水线 Copilot 分配`（可选）     |
   | **Resource owner**| 选择你自己的用户名（个人仓库情况下）          |

3. **Repository access** 区域：
   - 选择 **Only select repositories**
   - 点击下拉框，搜索并勾选 `autoagents`

4. **Permissions** 区域 — 展开 **Repository permissions**，设置以下 5 项：

   | 权限名称            | 访问级别            | 说明                         |
   |--------------------|--------------------|------------------------------|
   | **Actions**        | **Read and Write** | 触发 Workflow                 |
   | **Contents**       | **Read and Write** | 推送代码、合并 PR              |
   | **Issues**         | **Read and Write** | 分配 Copilot、管理标签          |
   | **Metadata**       | **Read-only**      | 自动选中，无法取消              |
   | **Pull requests**  | **Read and Write** | 管理 PR（Draft 转 Ready、合并） |

   > ⚠️ 不需要设置 **Account permissions**（全部留空即可）

5. 滚动到底部，点击绿色的 **Generate token** 按钮

6. **立即复制**生成的令牌（格式：`github_pat_xxxxxxxxxx...`）
   > ⚠️ **离开此页面后你将无法再看到这个令牌！** 请先复制。

### 方案 2（备选）：Classic Personal Access Token

如果 Fine-grained PAT 遇到问题（例如组织策略限制），可使用 Classic PAT。

1. 打开：👉 <https://github.com/settings/tokens/new>

2. 填写：
   - **Note**: `copilot-autoagents`
   - **Expiration**: `90 days`

3. 勾选以下 scope：
   - ✅ `repo`（完整仓库访问）
   - ✅ `workflow`（更新 Actions 工作流文件）

4. 点击 **Generate token**，复制令牌（格式：`ghp_xxxxxxxxxx...`）

### Fine-grained vs Classic 对比

| 特性               | Fine-grained PAT     | Classic PAT   |
|--------------------|--------------------|---------------|
| 权限粒度           | 精确到单个仓库和操作   | 粗粒度 scope   |
| 安全性             | ⭐⭐⭐ 高            | ⭐⭐ 中        |
| 组织仓库兼容性      | 需要组织管理员审批     | 直接可用       |
| 推荐场景           | 个人仓库             | 组织仓库或调试  |

---

## 5. 步骤 C：将 PAT 存为仓库 Secret

1. 打开仓库设置页面：  
   👉 <https://github.com/gjyhj1234/autoagents/settings/secrets/actions>
   
   > 路径：仓库首页 → **Settings**（标签栏最右侧）→ 左侧菜单 **Secrets and variables** → **Actions**

2. 点击绿色的 **New repository secret** 按钮

3. 填写：
   - **Name**：输入 `COPILOT_PAT`（必须完全一致，全大写）
   - **Secret**：粘贴上一步复制的令牌

4. 点击 **Add secret**

### 验证

添加成功后，你会在 **Repository secrets** 列表中看到 `COPILOT_PAT`，
旁边显示 `Updated just now`。

> **⚠️ 注意**：Secret 保存后无法查看其内容，只能更新或删除。

### 如果需要更新令牌

1. 在同一页面找到 `COPILOT_PAT`
2. 点击右侧的 **Update** 按钮（铅笔图标）
3. 粘贴新令牌
4. 点击 **Update secret**

---

## 6. 步骤 D：配置仓库 Actions 权限

### D-1：Workflow 权限

1. 打开：  
   👉 <https://github.com/gjyhj1234/autoagents/settings/actions>
   
   > 路径：仓库 → Settings → 左侧 **Actions** → **General**

2. 滚动到 **Workflow permissions** 区域

3. 选择 **Read and write permissions**  
   （如果当前是 "Read repository contents and packages permissions"，需要改）

4. ✅ 勾选 **Allow GitHub Actions to create and approve pull requests**

5. 点击 **Save**

### D-2：Actions 运行器权限

在同一页面（Actions → General）顶部：

1. **Actions permissions** 应选择 **Allow all actions and reusable workflows**
   （或至少允许 `actions/checkout`、`actions/github-script`、`actions/setup-dotnet`、`actions/setup-node` 等）

2. 点击 **Save**

---

## 7. 步骤 E：解决 "Awaiting approval from a maintainer"

### 这是什么？

当 Copilot（bot 身份）创建 PR 后，仓库中使用 `on: pull_request` 触发的 Workflow
（例如我们的 **Workflow 02 — PR Auto Tests**）不会自动运行，而是显示：

> ⚠️ **"This workflow is awaiting approval from a maintainer"**

这是 GitHub 的安全策略。GitHub 把 Copilot bot 视为 **"outside collaborator"** 或
**"first-time contributor"**，为了防止恶意代码通过 PR 触发 Workflow（可能泄露 Secret
或执行恶意操作），GitHub 默认要求仓库维护者手动审批。

### 方案 1（推荐 — 安全且省事）：每次手动批准

**适用场景**：个人仓库、安全要求高、不介意每次 PR 点一下

1. 当 Copilot 创建 PR 后，打开该 PR 页面
2. 滚动到底部的 **Checks** 区域（或点击 PR 页面上的 **Checks** 标签）
3. 你会看到黄色横幅：
   > "**1 workflow awaiting approval** — First-time contributors require approval to run workflows."
4. 点击 **Approve and run workflows** 按钮（或 **Approve and run**）
5. Workflow 02 开始运行测试

> **⚠️ 安全说明**：这是最安全的方式。你可以在点击之前检查 PR 的代码变更，
> 确认没有恶意内容（例如修改 Workflow 文件、尝试泄露 Secret 等）。
> 对于 Copilot 生成的代码，通常是安全的。

### 方案 2（减少手动操作）：调整 outside collaborator 审批策略

**适用场景**：个人仓库、信任 Copilot、希望减少人工步骤

1. 打开：  
   👉 <https://github.com/gjyhj1234/autoagents/settings/actions>

2. 滚动到 **Fork pull request workflows from outside collaborators** 区域

3. 你会看到三个选项：

   | 选项                                                              | 安全级别 | 说明                    |
   |------------------------------------------------------------------|---------|------------------------|
   | **Require approval for all outside collaborators**                | 🔒 最高  | 每次都需要批准（默认设置）   |
   | **Require approval for first-time contributors**                  | 🔓 中等  | 第一次需要批准，之后自动运行 |
   | **Require approval for first-time contributors who are new to GitHub** | 🔓 较低  | 仅 GitHub 新账户需要批准  |

4. **建议选择**："Require approval for first-time contributors"
   - 第一次 Copilot PR 仍需手动批准
   - 之后 Copilot 的 PR 可能不再需要批准（取决于 GitHub 是否记住 bot 身份）

5. 点击 **Save**

> **⚠️ 重要说明**：由于 Copilot 以 bot 身份创建 PR，GitHub 可能在每次新 PR 时
> 仍然将其视为 "first-time contributor"。如果修改设置后仍需手动批准，
> 这是 GitHub 的当前行为，不是配置错误。
>
> **截至 2026 年 4 月，GitHub 官方尚未提供完全跳过 bot PR 审批的设置。**

### 方案 3（完全自动化 — 有安全风险）：使用 GitHub API 自动批准

**适用场景**：完全不想手动操作、可以接受安全风险

你可以在 Workflow 04（Label PR）中添加一步，使用 PAT 自动批准等待中的 Workflow Run。
但这需要修改 Workflow 文件并理解风险：

> ⚠️ **安全风险**：自动批准所有 PR 的 Workflow 意味着任何贡献者（不只是 Copilot）
> 都可以触发你仓库的 CI/CD。如果你的 Workflow 中有 Secret 或部署步骤，这可能导致
> 安全问题。**仅在私有仓库或你完全信任所有贡献者时使用。**

本仓库当前不包含此自动化。如果你需要实现，请参考 GitHub API：
[Approve a workflow run](https://docs.github.com/en/rest/actions/workflow-runs#approve-a-workflow-run-for-a-fork-pull-request)。

### 方案 4：将 Workflow 改为 pull_request_target 触发（不推荐）

将 `02-pr-tests.yml` 中的 `on: pull_request` 改为 `on: pull_request_target` 可以
绕过审批，因为 `pull_request_target` 使用 **base 分支的代码** 而非 PR 分支的代码运行。

> ⚠️ **强烈不推荐**：`pull_request_target` 的 checkout 默认是 base 分支（main），
> 不是 PR 的代码。如果你 checkout PR 代码来运行测试，会暴露 Secret 给 PR 作者。
> 这是一个 [已知的安全反模式](https://securitylab.github.com/resources/github-actions-preventing-pwn-requests/)。

### 当前推荐策略

对于本仓库（`autoagents`），建议组合使用：

1. **步骤 D** 中的 Actions 权限设置
2. **方案 2** 中的 "Require approval for first-time contributors" 设置
3. Copilot 的第一个 PR **手动点击一次** "Approve and run workflows"
4. 后续 PR 观察是否自动运行，如果仍需手动，则接受每次点一下

**这是当前 GitHub 生态中最务实的做法。**

---

## 8. 步骤 F：启用 Auto-Merge

1. 打开仓库设置：  
   👉 <https://github.com/gjyhj1234/autoagents/settings>
   
   > 路径：仓库 → Settings → 左侧 **General**

2. 滚动到 **Pull Requests** 区域

3. ✅ 勾选 **Allow auto-merge**

4. ✅ 建议同时勾选 **Automatically delete head branches**
   （合并后自动删除 `copilot/*` 和 `feature/task-*` 分支）

5. 点击 **Save** 或 **Update**（如果有的话；有些仓库即时生效）

---

## 9. 验证：端到端测试流水线

完成上述所有步骤后，按以下流程验证流水线是否工作。

### 9-1：验证 Copilot 分配

1. 打开 Actions 页面：  
   👉 <https://github.com/gjyhj1234/autoagents/actions>

2. 在左侧工作流列表中，点击 **🤖 01 — Issue → Agent**

3. 点击右上方 **Run workflow** → 保持 Branch `main` → 点绿色 **Run workflow**

4. 等待几秒，刷新页面

5. 点击新出现的运行记录，进入 **Sync queue and assign Copilot** 作业

6. 检查日志：
   - **Step 2**（Queue tasks）应显示 ✅
   - **Step 3**（Assign Copilot）应显示 ✅ 且日志中有 `✅ Assigned issue #XX to Copilot.`
   - 如果 Step 3 显示 **skipped**，说明已有 Issue 在处理中（正常）

7. 打开对应的 Issue，检查：
   - **Assignees** 中有 `Copilot`
   - 标签包含 `agent-in-progress`
   - 时间线中出现 👀 反应表情

### 9-2：验证 PR 测试和合并

1. 等待 Copilot 创建 Draft PR（通常 5-30 分钟）

2. 打开 PR 页面

3. 如果看到 **"awaiting approval from a maintainer"**：
   - 点击 **Approve and run workflows**
   
4. 等待 Workflow 02 运行完成

5. 检查 Workflow 03 是否自动合并了 PR

6. 确认 Issue 被关闭，标签变为 `agent-completed`

7. 确认下一个排队的 Issue 自动变为 `agent-in-progress`

---

## 10. 场景处理：卡住的 Issue / PR 如何修复

### 场景 1：Issue 标记为 agent-in-progress，但 Copilot 没有分配

**症状**：Issue 有 `agent-in-progress` 标签，但右侧 Assignees 没有 `Copilot`

**原因**：之前的 Workflow 运行设置了标签但分配失败（通常是 PAT 问题）

**修复步骤**：

1. 打开 Issue 页面
2. 在右侧 **Labels** 区域，点击 `agent-in-progress` 旁的 ✕ 移除它
3. 如果没有 `agent-queued` 标签，点击 Labels 齿轮图标添加它
4. 确认 `COPILOT_PAT` Secret 已正确设置
5. 去 Actions → **🤖 01 — Issue → Agent** → **Run workflow** 手动触发

### 场景 2：PR 显示 "awaiting approval"，测试未运行

**症状**：PR 页面底部显示黄色横幅 "This workflow is awaiting approval from a maintainer"

**修复步骤**：

1. 打开 PR 页面（例如 PR #42）
2. 向下滚动或点击 **Checks** 标签
3. 找到黄色的审批横幅
4. 点击 **Approve and run workflows**
5. 等待 Workflow 02 开始运行

### 场景 3：PR 测试通过了但没有自动合并

**症状**：Workflow 02 显示全部通过（✅），但 PR 仍然 Open

**可能原因**：
- PR 是 Draft 状态（Workflow 03 会自动转为 Ready，但可能遇到权限问题）
- PR 没有 `auto-merge` 标签
- `COPILOT_PAT` 未设置或过期

**修复步骤**：

方式 A — 手动触发 Auto-Merge Workflow：
1. 打开 <https://github.com/gjyhj1234/autoagents/actions/workflows/03-auto-merge.yml>
2. 点击 **Run workflow**
3. 在 **PR number to auto-merge** 输入框中输入 PR 号（例如 `42`）
4. 点击 **Run workflow**

方式 B — 手动合并：
1. 打开 PR 页面
2. 如果是 Draft PR，点击 **Ready for review**
3. 点击 **Squash and merge**
4. 编辑 commit message（保留 `Closes #XX` 关键字以自动关闭 Issue）
5. 点击 **Confirm squash and merge**

合并后手动触发下一个 Issue 分配：
1. 去 Actions → **🤖 01 — Issue → Agent** → **Run workflow**

### 场景 4：Copilot 创建了 PR 但没有链接到 Issue

**症状**：PR 存在但对应 Issue 没有任何变化

**修复步骤**：

1. 打开 PR，点击 **Edit**（标题旁的编辑按钮）
2. 在 PR body 中添加一行：`Closes #XX`（XX 是 Issue 号）
3. 点击 **Update**
4. 这样合并时会自动关闭 Issue

### 场景 5：COPILOT_PAT 过期

**症状**：Workflow 01 的 Step 3 失败，错误信息包含 `401` 或 `Bad credentials`

**修复步骤**：

1. 重新创建 PAT（参考[步骤 B](#4-步骤-b创建-personal-access-token-pat)）
2. 更新仓库 Secret（参考[步骤 C](#5-步骤-c将-pat-存为仓库-secret)）
3. 手动触发 Workflow 01 验证

### 场景 6：想跳过一个卡住的 Issue，处理下一个

**修复步骤**：

1. 打开卡住的 Issue
2. 移除 `agent-in-progress` 和 `agent-queued` 标签
3. 移除 Copilot 的 Assignee（如果有的话）
4. 关闭该 Issue（可以之后重新打开）
5. 去 Actions → **🤖 01 — Issue → Agent** → **Run workflow**
6. Workflow 会自动找到下一个带 `agent-task` 标签的 Issue

---

## 11. 常见问题 (FAQ)

### Q1: 为什么必须使用 PAT？GITHUB_TOKEN 不行吗？

**A**: GitHub 出于安全考虑，限制了 `GITHUB_TOKEN` 的能力：

1. ❌ 不能分配 Copilot Agent — GitHub API 要求用户身份认证
2. ❌ 触发的事件不会启动其他 Workflow — 防止无限循环

使用 PAT 可以绕过这两个限制。这是 [GitHub 官方文档](https://docs.github.com/en/copilot/how-tos/use-copilot-agents/cloud-agent/create-a-pr#assigning-an-issue-to-copilot-via-the-github-api)明确要求的。

### Q2: PAT 过期了怎么办？

**A**: 重新创建 PAT，然后在仓库 Settings → Secrets → `COPILOT_PAT` 中更新。
在此期间，你仍然可以手动在 Issue 页面点击 **Assign to Agent → Copilot** 来触发。

### Q3: 可以用 GitHub App 代替 PAT 吗？

**A**: 可以。创建一个 GitHub App 并使用 user-to-server token。
但对于个人仓库，PAT 是最简单的方案。GitHub App 更适合组织级别的部署。

### Q4: 为什么 Copilot 创建的 PR 需要手动批准 Workflow？

**A**: GitHub 的安全策略。Copilot 以 bot 身份创建 PR，GitHub 将其视为 "outside
collaborator"。`on: pull_request` 触发的 Workflow 会暴露仓库的 Secrets（即使是
只读模式），所以 GitHub 要求人工确认。

这 **不是** 仓库配置错误，是 GitHub 平台当前的限制。

详见：[GitHub Docs — Approving workflow runs from public forks](https://docs.github.com/en/actions/managing-workflow-runs-and-deployments/managing-workflow-runs/approving-workflow-runs-from-public-forks)

### Q5: "Approve and run workflows" 按钮在哪里？

**A**: 打开 Copilot 创建的 PR 页面后：

1. 向下滚动到底部的 merge 按钮区域附近
2. 或者点击 PR 页面上的 **Checks** 标签
3. 你会看到黄色横幅和 **Approve and run workflows** 按钮
4. 如果你在 Actions 页面看到该 Workflow Run，也可以在那里找到 **Approve** 按钮

### Q6: 如何确认流水线是否正常工作？

**A**: 检查以下几个指标：

| 检查项                             | 期望状态                              |
|------------------------------------|------------------------------------- |
| Issue 右侧 Assignees              | 显示 `Copilot`                        |
| Issue 标签                         | `agent-in-progress`                   |
| Issue 时间线                       | 出现 👀 反应和/或 "View session" 链接  |
| Actions 页面                       | 有 "Running Copilot cloud agent" 运行 |
| PR 创建后                          | 标签有 `auto-merge`                   |
| PR Checks                          | Workflow 02 正在运行或已通过           |

### Q7: 一个 Issue 一直卡在 in-progress 怎么办？

**A**: 参考 [场景 1](#场景-1issue-标记为-agent-in-progress但-copilot-没有分配)
或 [场景 6](#场景-6想跳过一个卡住的-issue处理下一个)。

### Q8: 多个 Issue 同时执行会怎样？

**A**: 不会。Workflow 01 设计为 **单队列模式**：同一时间只有一个 Issue 处于
`agent-in-progress` 状态。其余 Issue 标记为 `agent-queued`，按 Issue 编号排序。

### Q9: 合并 PR 后下一个 Issue 没有自动开始怎么办？

**A**: 手动触发 Workflow 01：
1. 打开 <https://github.com/gjyhj1234/autoagents/actions/workflows/01-issue-agent.yml>
2. 点击 **Run workflow** → **Run workflow**

这可能发生在以下情况：
- PR 是手动合并的（不是通过 Workflow 03）
- `COPILOT_PAT` 未设置（Workflow 03 回退到 `GITHUB_TOKEN`，无法触发后续 Workflow）

---

## 12. 快速排查清单

遇到问题时，按顺序检查：

```
□ 1. COPILOT_PAT Secret 是否存在且未过期？
     → Settings → Secrets and variables → Actions → 检查 COPILOT_PAT

□ 2. Copilot Coding Agent 是否已启用？
     → 任意 Issue 右侧能否看到 "Assign to Agent → Copilot"

□ 3. Actions 权限是否为 Read and write？
     → Settings → Actions → General → Workflow permissions

□ 4. Allow auto-merge 是否勾选？
     → Settings → General → Pull Requests → Allow auto-merge

□ 5. 是否需要在 PR 上点击 "Approve and run workflows"？
     → 打开 Copilot 的 PR，检查底部是否有黄色横幅

□ 6. Workflow 01 最近一次运行是否成功？
     → Actions → 🤖 01 — Issue → Agent → 看最近一次运行状态

□ 7. 当前有没有 Issue 标签为 agent-in-progress？
     → Issues → Filter: label:agent-in-progress

□ 8. 如果以上都正常但仍不工作：
     → 手动在 Issue 右侧点击 Assign to Agent → Copilot
     → 这会绕过所有 Workflow 直接触发 Copilot
```

---

## 附录：各 Workflow 文件说明

| Workflow 文件                          | 触发条件                         | 使用的 Token                   | 作用              |
|---------------------------------------|--------------------------------|-------------------------------|-------------------|
| `01-issue-agent.yml`                  | Issue 事件 / 手动触发            | Step 1: `GITHUB_TOKEN`<br>Step 2: `COPILOT_PAT` | 队列管理 + 分配 Copilot |
| `02-pr-tests.yml`                     | `pull_request` 事件 / 手动触发   | `GITHUB_TOKEN`                 | 运行测试           |
| `03-auto-merge.yml`                   | `check_suite` / `pull_request_review` / 手动 | `COPILOT_PAT` (fallback: `GITHUB_TOKEN`) | 自动合并 + 触发下一个 |
| `04-label-pr.yml`                     | `pull_request: opened`          | `GITHUB_TOKEN`                 | 给 Copilot PR 加标签 |

---

## 附录：完整自动化流程（带决策点）

```
Issue created (with agent-task label)
  │
  ▼
Workflow 01 — Step 1: 检查队列
  ├── 已有 Issue in-progress → 不操作，等待
  └── 没有 → 选出最早的待办 Issue
          │
          ▼
Workflow 01 — Step 2: 分配 Copilot (需要 COPILOT_PAT)
  ├── 成功 → Issue 标记为 agent-in-progress
  └── 失败 → Issue 标记为 agent-queued + 发评论提示
          │
          ▼
Copilot Coding Agent 开始工作（5-30 分钟）
  │
  ▼
Copilot 创建 Draft PR
  │
  ▼
Workflow 04: 自动给 PR 添加 auto-merge 标签
  │
  ▼
Workflow 02: 运行测试
  ├── ⚠️ "Awaiting approval" → 人工点击批准（或配置自动批准）
  ├── 测试通过 ✅
  │     │
  │     ▼
  │   Workflow 03: 
  │     ├── Draft PR → Ready-for-Review（GraphQL）
  │     ├── Squash Merge
  │     ├── 关闭关联 Issue（agent-completed）
  │     ├── 删除分支
  │     └── 触发 Workflow 01 → 分配下一个 Issue
  │
  └── 测试失败 ❌ → PR 评论提示失败 → 等待修复后重试
```
