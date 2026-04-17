using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace WorkflowApprover;

/// <summary>
/// Main form with embedded WebView2 browser that auto-approves
/// GitHub Actions workflow runs requiring approval.
/// </summary>
public partial class MainForm : Form
{
    // ── Controls ──
    private WebView2 _webView = null!;
    private TextBox _txtOwner = null!;
    private TextBox _txtRepo = null!;
    private NumericUpDown _nudInterval = null!;
    private TextBox _txtToken = null!;
    private Button _btnStart = null!;
    private Button _btnStop = null!;
    private Button _btnManual = null!;
    private Button _btnLogin = null!;
    private RichTextBox _txtLog = null!;
    private Label _lblStatus = null!;
    private ProgressBar _progressBar = null!;

    // ── State ──
    private System.Windows.Forms.Timer? _timer;
    private GitHubApiClient? _apiClient;
    private AppSettings _settings = null!;
    private CancellationTokenSource? _cts;
    private bool _isProcessing;
    private int _totalApproved;

    // JavaScript to find and click the "Approve and run" button on the workflow run page.
    // GitHub's UI may have different button texts and structures; this covers common patterns.
    private const string ApproveScript = @"
(function() {
    // Strategy 1: Look for the main 'Approve and run' button
    const buttons = document.querySelectorAll('button');
    for (const btn of buttons) {
        const text = btn.textContent.trim().toLowerCase();
        if ((text.includes('approve') && text.includes('run')) ||
            text === 'approve and run' ||
            text === 'approve') {
            if (!btn.disabled && btn.offsetParent !== null) {
                btn.click();
                return JSON.stringify({ result: 'clicked', target: btn.textContent.trim() });
            }
        }
    }

    // Strategy 2: Look for link/anchor with approval text
    const links = document.querySelectorAll('a, [role=""button""]');
    for (const el of links) {
        const text = el.textContent.trim().toLowerCase();
        if (text.includes('approve') && (text.includes('run') || text.includes('workflow'))) {
            el.click();
            return JSON.stringify({ result: 'clicked', target: el.textContent.trim() });
        }
    }

    // Strategy 3: Check if there's a pending review/approval banner
    const banners = document.querySelectorAll('[class*=""flash""], [class*=""banner""], [class*=""alert""]');
    for (const banner of banners) {
        const text = banner.textContent.toLowerCase();
        if (text.includes('approval') || text.includes('approve')) {
            const btn = banner.querySelector('button, a');
            if (btn) {
                btn.click();
                return JSON.stringify({ result: 'clicked_banner', target: btn.textContent.trim() });
            }
        }
    }

    // Strategy 4: Look in dialog/modal
    const dialogs = document.querySelectorAll('dialog, [role=""dialog""], [class*=""modal""]');
    for (const dialog of dialogs) {
        const btns = dialog.querySelectorAll('button');
        for (const btn of btns) {
            const text = btn.textContent.trim().toLowerCase();
            if (text.includes('approve')) {
                btn.click();
                return JSON.stringify({ result: 'clicked_dialog', target: btn.textContent.trim() });
            }
        }
    }

    // Check page state
    const pageText = document.body?.innerText || '';
    if (pageText.includes('Waiting for approval') || pageText.includes('action_required')) {
        return JSON.stringify({ result: 'pending_no_button', page: 'has_pending_indicator' });
    }

    return JSON.stringify({ result: 'not_found' });
})()";

    public MainForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        // ── Form settings ──
        Text = "GitHub Actions Workflow Auto-Approver";
        Size = new Size(1200, 800);
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;

        // ── Top panel: Settings ──
        var panelTop = new Panel
        {
            Dock = DockStyle.Top,
            Height = 110,
            Padding = new Padding(10),
        };

        // Row 1: Owner / Repo / Interval
        var lblOwner = new Label { Text = "Owner:", Location = new Point(10, 12), AutoSize = true };
        _txtOwner = new TextBox { Location = new Point(60, 10), Width = 150 };

        var lblRepo = new Label { Text = "Repo:", Location = new Point(220, 12), AutoSize = true };
        _txtRepo = new TextBox { Location = new Point(265, 10), Width = 180 };

        var lblInterval = new Label { Text = "Interval (sec):", Location = new Point(460, 12), AutoSize = true };
        _nudInterval = new NumericUpDown
        {
            Location = new Point(555, 10), Width = 70,
            Minimum = 30, Maximum = 3600, Value = 180, Increment = 30
        };

        // Row 2: Token + Buttons
        var lblToken = new Label { Text = "Token:", Location = new Point(10, 47), AutoSize = true };
        _txtToken = new TextBox
        {
            Location = new Point(60, 45), Width = 385,
            PasswordChar = '•',
            PlaceholderText = "ghp_... (optional, improves API rate limit)"
        };

        _btnLogin = new Button
        {
            Text = "🔑 Login to GitHub",
            Location = new Point(460, 43), Width = 130, Height = 28
        };
        _btnLogin.Click += OnLoginClick;

        _btnStart = new Button
        {
            Text = "▶ Start Auto-Approve",
            Location = new Point(600, 43), Width = 140, Height = 28,
            BackColor = Color.FromArgb(46, 160, 67),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        _btnStart.Click += OnStartClick;

        _btnStop = new Button
        {
            Text = "⏹ Stop",
            Location = new Point(750, 43), Width = 80, Height = 28,
            Enabled = false,
        };
        _btnStop.Click += OnStopClick;

        _btnManual = new Button
        {
            Text = "🔄 Check Now",
            Location = new Point(840, 43), Width = 100, Height = 28,
        };
        _btnManual.Click += OnManualCheckClick;

        // Row 3: Status bar
        _lblStatus = new Label
        {
            Text = "Status: Idle",
            Location = new Point(10, 80),
            AutoSize = true,
            ForeColor = Color.Gray,
        };

        _progressBar = new ProgressBar
        {
            Location = new Point(460, 78),
            Width = 480, Height = 20,
            Style = ProgressBarStyle.Marquee,
            Visible = false,
        };

        panelTop.Controls.AddRange(new Control[]
        {
            lblOwner, _txtOwner, lblRepo, _txtRepo, lblInterval, _nudInterval,
            lblToken, _txtToken, _btnLogin, _btnStart, _btnStop, _btnManual,
            _lblStatus, _progressBar,
        });

        // ── Split: WebView (left) + Log (right) ──
        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 750,
        };

        _webView = new WebView2
        {
            Dock = DockStyle.Fill,
        };

        _txtLog = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(200, 200, 200),
            Font = new Font("Consolas", 9f),
            WordWrap = true,
        };

        splitContainer.Panel1.Controls.Add(_webView);
        splitContainer.Panel2.Controls.Add(_txtLog);

        Controls.Add(splitContainer);
        Controls.Add(panelTop);

        ResumeLayout(true);
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Load settings
        _settings = AppSettings.Load();
        _txtOwner.Text = _settings.Owner;
        _txtRepo.Text = _settings.Repo;
        _nudInterval.Value = Math.Clamp(_settings.IntervalSeconds, 30, 3600);
        _txtToken.Text = _settings.GitHubToken;

        // Initialize WebView2 with persistent user data folder
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WorkflowApprover", "WebView2Data");
        Directory.CreateDirectory(userDataFolder);

        try
        {
            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await _webView.EnsureCoreWebView2Async(env);
            _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;

            Log("✅ WebView2 initialized successfully", Color.LightGreen);
            Log($"   User data folder: {userDataFolder}", Color.Gray);

            // Navigate to GitHub
            _webView.CoreWebView2.Navigate("https://github.com");

            if (_settings.AutoStartEnabled && !string.IsNullOrEmpty(_settings.Owner))
            {
                Log("Auto-start enabled, beginning in 5 seconds...", Color.Yellow);
                await Task.Delay(5000);
                StartAutoApprove();
            }
        }
        catch (Exception ex)
        {
            Log($"❌ WebView2 initialization failed: {ex.Message}", Color.Red);
            Log("   Please ensure Microsoft Edge WebView2 Runtime is installed.", Color.Red);
            Log("   Download: https://developer.microsoft.com/en-us/microsoft-edge/webview2/", Color.Orange);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopAutoApprove();
        SaveSettings();
        _apiClient?.Dispose();
        base.OnFormClosing(e);
    }

    // ── Event Handlers ──

    private void OnLoginClick(object? sender, EventArgs e)
    {
        _webView.CoreWebView2?.Navigate("https://github.com/login");
        Log("🔑 Navigating to GitHub login page...", Color.Cyan);
    }

    private void OnStartClick(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtOwner.Text) || string.IsNullOrWhiteSpace(_txtRepo.Text))
        {
            MessageBox.Show("Please enter Owner and Repo.", "Missing Settings",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SaveSettings();
        StartAutoApprove();
    }

    private void OnStopClick(object? sender, EventArgs e)
    {
        StopAutoApprove();
    }

    private async void OnManualCheckClick(object? sender, EventArgs e)
    {
        if (_isProcessing)
        {
            Log("⚠ Already processing, please wait...", Color.Yellow);
            return;
        }

        SaveSettings();
        await RunApprovalCycleAsync();
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        var url = _webView.CoreWebView2.Source;
        if (e.IsSuccess)
        {
            Log($"📄 Page loaded: {url}", Color.Gray);
        }
        else
        {
            Log($"❌ Navigation failed ({e.WebErrorStatus}): {url}", Color.Red);
        }
    }

    // ── Auto-Approve Logic ──

    private void StartAutoApprove()
    {
        StopAutoApprove();

        _apiClient?.Dispose();
        _apiClient = new GitHubApiClient(
            string.IsNullOrWhiteSpace(_txtToken.Text) ? null : _txtToken.Text);

        var interval = (int)_nudInterval.Value * 1000;
        _timer = new System.Windows.Forms.Timer { Interval = interval };
        _timer.Tick += async (_, _) => await RunApprovalCycleAsync();
        _timer.Start();

        _btnStart.Enabled = false;
        _btnStop.Enabled = true;
        _progressBar.Visible = true;
        _lblStatus.Text = $"Status: Running (every {_nudInterval.Value}s)";
        _lblStatus.ForeColor = Color.Green;

        Log($"▶ Auto-approve started: checking {_txtOwner.Text}/{_txtRepo.Text} every {_nudInterval.Value}s",
            Color.LightGreen);

        // Run first check immediately
        _ = RunApprovalCycleAsync();
    }

    private void StopAutoApprove()
    {
        _cts?.Cancel();
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;

        _btnStart.Enabled = true;
        _btnStop.Enabled = false;
        _progressBar.Visible = false;
        _lblStatus.Text = "Status: Stopped";
        _lblStatus.ForeColor = Color.Gray;

        Log("⏹ Auto-approve stopped", Color.Orange);
    }

    private async Task RunApprovalCycleAsync()
    {
        if (_isProcessing) return;
        _isProcessing = true;

        _cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var ct = _cts.Token;

        try
        {
            var owner = _txtOwner.Text.Trim();
            var repo = _txtRepo.Text.Trim();

            if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repo))
            {
                Log("⚠ Owner/Repo not set", Color.Yellow);
                return;
            }

            Log($"\n{'─'.ToString().PadLeft(60, '─')}", Color.DarkGray);
            Log($"🔍 [{DateTime.Now:HH:mm:ss}] Checking for pending approvals: {owner}/{repo}",
                Color.Cyan);

            // Step 1: Use API to find runs needing approval
            _apiClient ??= new GitHubApiClient(
                string.IsNullOrWhiteSpace(_txtToken.Text) ? null : _txtToken.Text);

            var pendingRuns = await _apiClient.GetPendingApprovalRunsAsync(owner, repo, ct);

            if (pendingRuns.Count == 0)
            {
                Log("   ✅ No workflow runs need approval", Color.LightGreen);
                _lblStatus.Text = $"Status: Running — last check {DateTime.Now:HH:mm:ss} — none pending (total approved: {_totalApproved})";
                return;
            }

            Log($"   Found {pendingRuns.Count} run(s) requiring approval:", Color.Yellow);
            foreach (var run in pendingRuns)
            {
                Log($"   • {run}", Color.White);
            }

            // Step 2: For each pending run, navigate and approve
            foreach (var run in pendingRuns)
            {
                if (ct.IsCancellationRequested) break;

                Log($"\n   🌐 Navigating to: {run.HtmlUrl}", Color.Cyan);
                _webView.CoreWebView2.Navigate(run.HtmlUrl);

                // Wait for page to load
                await Task.Delay(3000, ct);

                // Try to click the approve button with retries
                var approved = false;
                for (int attempt = 1; attempt <= 5; attempt++)
                {
                    if (ct.IsCancellationRequested) break;

                    try
                    {
                        var result = await _webView.CoreWebView2.ExecuteScriptAsync(ApproveScript);
                        var cleanResult = result?.Trim('"').Replace("\\\"", "\"") ?? "";

                        Log($"   [Attempt {attempt}] Script result: {cleanResult}", Color.Gray);

                        if (cleanResult.Contains("\"result\":\"clicked\"") ||
                            cleanResult.Contains("\"result\":\"clicked_banner\"") ||
                            cleanResult.Contains("\"result\":\"clicked_dialog\""))
                        {
                            Log($"   ✅ Approved run #{run.Id}: {run.Name}", Color.LightGreen);
                            _totalApproved++;
                            approved = true;

                            // Wait for the approval to process
                            await Task.Delay(2000, ct);
                            break;
                        }

                        if (cleanResult.Contains("\"result\":\"not_found\""))
                        {
                            if (attempt < 5)
                            {
                                Log($"   ⏳ Button not found yet, waiting 3s (attempt {attempt}/5)...",
                                    Color.Yellow);
                                await Task.Delay(3000, ct);
                            }
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Log($"   ⚠ Attempt {attempt} error: {ex.Message}", Color.Orange);
                        if (attempt < 5)
                            await Task.Delay(2000, ct);
                    }
                }

                if (!approved)
                {
                    Log($"   ⚠ Could not auto-approve run #{run.Id} — may need manual intervention",
                        Color.Orange);
                }
            }

            _lblStatus.Text = $"Status: Running — last check {DateTime.Now:HH:mm:ss} — total approved: {_totalApproved}";
        }
        catch (OperationCanceledException)
        {
            Log("   ⏹ Check cycle cancelled", Color.Gray);
        }
        catch (Exception ex)
        {
            Log($"   ❌ Error: {ex.Message}", Color.Red);
        }
        finally
        {
            _isProcessing = false;
        }
    }

    // ── Helpers ──

    private void SaveSettings()
    {
        _settings.Owner = _txtOwner.Text.Trim();
        _settings.Repo = _txtRepo.Text.Trim();
        _settings.IntervalSeconds = (int)_nudInterval.Value;
        _settings.GitHubToken = _txtToken.Text.Trim();
        _settings.Save();
    }

    private void Log(string message, Color color)
    {
        if (InvokeRequired)
        {
            Invoke(() => Log(message, color));
            return;
        }

        _txtLog.SelectionStart = _txtLog.TextLength;
        _txtLog.SelectionLength = 0;
        _txtLog.SelectionColor = color;
        _txtLog.AppendText(message + "\n");
        _txtLog.ScrollToCaret();
    }
}
