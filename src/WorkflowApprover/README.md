# GitHub Actions Workflow Auto-Approver (工作流自动审批工具)

> Windows 桌面应用，使用内嵌浏览器 (WebView2) 自动审批 Copilot 创建的 PR 所触发的 GitHub Actions 工作流。

## 背景

GitHub Copilot 云代理创建的 PR 会触发 Actions 工作流，但由于 Copilot 的**平台级安全机制**，这些工作流需要仓库维护者手动点击 "Approve and run" 才能执行。这个安全限制**无法通过任何 GitHub 配置禁用**。

本工具通过内嵌浏览器自动导航到需要审批的工作流页面，模拟点击 "Approve and run" 按钮，实现自动化审批。

## 工作原理

```
┌─────────────────────────────────────────────────┐
│  WorkflowApprover (Windows 桌面应用)             │
│                                                  │
│  1. 定时器 (每 N 秒)                              │
│     ↓                                            │
│  2. GitHub REST API 查询 action_required 的运行    │
│     GET /repos/{owner}/{repo}/actions/runs        │
│     ?status=action_required                       │
│     ↓                                            │
│  3. 对每个待审批的运行:                             │
│     a. WebView2 导航到运行页面                      │
│     b. JavaScript 注入，查找 "Approve and run" 按钮 │
│     c. 自动点击审批                                │
│     ↓                                            │
│  4. 记录日志，等待下一次检查                        │
└─────────────────────────────────────────────────┘
```

## 系统要求

- **操作系统**: Windows 10 1809+ 或 Windows 11
- **运行时**: .NET 8.0 Desktop Runtime
- **浏览器**: Microsoft Edge WebView2 Runtime（通常已预装在 Windows 10/11 上）

## 安装与运行

### 1. 安装 .NET 8.0 Desktop Runtime

如果尚未安装，下载并安装：
https://dotnet.microsoft.com/download/dotnet/8.0

### 2. 安装 WebView2 Runtime

通常 Windows 10/11 已自带。如果没有，下载安装：
https://developer.microsoft.com/en-us/microsoft-edge/webview2/

### 3. 构建并运行

```bash
cd src/WorkflowApprover/WorkflowApprover
dotnet build --configuration Release
dotnet run --configuration Release
```

或者直接发布为独立可执行文件：

```bash
dotnet publish -c Release -r win-x64 --self-contained true -o ./publish
./publish/WorkflowApprover.exe
```

## 使用方法

### 首次使用

1. **启动应用** — 打开 `WorkflowApprover.exe`
2. **登录 GitHub** — 点击 "🔑 Login to GitHub"，在内嵌浏览器中登录你的 GitHub 账号
3. **配置设置**:
   - **Owner**: 仓库所有者（如 `gjyhj1234`）
   - **Repo**: 仓库名称（如 `autoagents2`）
   - **Interval**: 检查间隔秒数（默认 180 秒 = 3 分钟）
   - **Token**: （可选）GitHub Personal Access Token，提高 API 速率限制
4. **启动自动审批** — 点击 "▶ Start Auto-Approve"

### 日常使用

- 应用会自动保存设置到 `%LOCALAPPDATA%/WorkflowApprover/settings.json`
- 浏览器登录状态会持久化到 `%LOCALAPPDATA%/WorkflowApprover/WebView2Data/`
- 重启应用后无需重新登录

### 手动触发

点击 "🔄 Check Now" 立即执行一次检查，无需等待定时器。

## 界面说明

```
┌───────────────────────────────────────────────────────┐
│ Owner: [gjyhj1234] Repo: [autoagents2] Interval: [180]│
│ Token: [•••••]  [Login] [▶ Start] [⏹ Stop] [🔄 Check] │
│ Status: Running — last check 14:30:00 — approved: 3   │
├─────────────────────────────┬─────────────────────────┤
│                             │ ✅ WebView2 initialized  │
│    WebView2 浏览器            │ 🔍 Checking for runs...  │
│    (显示 GitHub 页面)         │ Found 2 runs pending    │
│                             │ ✅ Approved run #12345   │
│                             │ ✅ Approved run #12346   │
└─────────────────────────────┴─────────────────────────┘
  左侧: 内嵌浏览器              右侧: 操作日志
```

## Token 说明

GitHub Token 是**可选的**，但建议设置：

- **不设置 Token**: 使用匿名 API，速率限制为 60 次/小时
- **设置 Token**: 速率限制提升至 5000 次/小时

创建 Token: https://github.com/settings/tokens  
只需要 `repo` 和 `actions` 权限。

## 与 GitHub Actions 调度器配合使用

本工具可以与仓库中的 `00-pr-dispatcher.yml`（schedule 调度器）配合使用：

| 方案 | 说明 | 可靠性 |
|------|------|--------|
| `00-pr-dispatcher.yml` (schedule) | GitHub 原生调度器，每 5 分钟轮询 | ⚠️ 有延迟，新工作流可能需要等待 |
| `pull_request_target` | 快速路径 | ❌ 被 Copilot 安全机制阻止 |
| **WorkflowApprover** (本工具) | Windows 桌面自动审批 | ✅ 立即响应，最可靠 |

**推荐配置**: 同时使用 `00-pr-dispatcher.yml` + `WorkflowApprover`，实现多层保障。

## 故障排除

### WebView2 初始化失败

```
❌ WebView2 initialization failed
```

**解决**: 安装 Microsoft Edge WebView2 Runtime:
https://developer.microsoft.com/en-us/microsoft-edge/webview2/

### 审批按钮找不到

如果日志显示 `not_found`，可能原因：
1. 页面尚未完全加载 — 增加检查间隔
2. GitHub UI 结构变更 — 需要更新 JavaScript 注入脚本
3. 用户未登录 — 点击 "Login to GitHub" 重新登录

### API 速率限制

```
GitHub API returned 403
```

**解决**: 设置 GitHub Token 提高速率限制。

## 技术实现

| 组件 | 技术 |
|------|------|
| 框架 | .NET 8.0 Windows Forms |
| 内嵌浏览器 | Microsoft Edge WebView2 |
| API 客户端 | HttpClient + System.Text.Json |
| 自动化方式 | JavaScript 注入 (ExecuteScriptAsync) |

## 许可证

与主项目相同。
