using AutoAgents5.Core.Models;
using AutoAgents5.Core.Services;

namespace AutoAgents5.App;

public partial class MainForm : Form
{
    // ── Fields ─────────────────────────────────────────────────────────────
    private AppLogger? _logger;
    private WebView2BrowserBridge? _bridge;
    private TaskOrchestrator? _orchestrator;
    private CancellationTokenSource? _runCts;

    private string _selectedTaskFile = string.Empty;
    private LogLevel _minLogLevel = LogLevel.Trace;
    private bool _isRunning = false;

    public MainForm()
    {
        InitializeComponent();
        Load += MainForm_Load;
        FormClosing += MainForm_FormClosing;
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private async void MainForm_Load(object sender, EventArgs e)
    {
        // Init logger
        var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
        _logger = new AppLogger(logDir);
        _logger.OnLog += Logger_OnLog;

        // Init WebView2
        _bridge = new WebView2BrowserBridge(webView);
        await _bridge.InitializeAsync();

        // Navigate to GitHub
        await _bridge.NavigateAsync("https://github.com", CancellationToken.None);
        _logger.Info(OrchestratorState.Idle, "WebView2 已初始化，请在浏览器中登录 GitHub");
        SetStatus("请在浏览器中登录 GitHub，然后点击「启动」");
    }

    private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
    {
        _runCts?.Cancel();
    }

    // ── Start / Stop ───────────────────────────────────────────────────────

    private async void BtnStart_Click(object sender, EventArgs e)
    {
        if (_isRunning) return;

        var owner = txtOwner.Text.Trim();
        var repo = txtRepo.Text.Trim();
        var role = cboRole.SelectedItem?.ToString() ?? "pm";

        if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repo))
        {
            MessageBox.Show("请填写用户名和仓库名", "参数缺失", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _isRunning = true;
        btnStart.Enabled = false;
        btnStop.Enabled = true;
        SetStatus("运行中…");

        var settings = new AppSettings { Owner = owner, Repo = repo, Role = role };

        _runCts = new CancellationTokenSource();
        _ = RunLoopAsync(settings, _runCts.Token);
    }

    private void BtnStop_Click(object sender, EventArgs e)
    {
        _runCts?.Cancel();
        SetRunningState(false);
        SetStatus("已停止");
        _logger?.Info(OrchestratorState.Idle, "用户手动停止");
    }

    // ── Main automation loop ───────────────────────────────────────────────

    private async Task RunLoopAsync(AppSettings settings, CancellationToken ct)
    {
        if (_bridge == null || _logger == null) return;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Refresh task list
                await RefreshTaskListAsync(settings, ct);

                if (lstTasks.Items.Count == 0)
                {
                    _logger.Info(OrchestratorState.Idle,
                        $"角色 {settings.Role} 无待处理任务，停止循环");
                    SetStatus($"角色 {settings.Role} 无待处理任务");
                    break;
                }

                // Pick task: use selected or first
                var taskFile = string.IsNullOrEmpty(_selectedTaskFile)
                    ? lstTasks.Items[0].Text
                    : _selectedTaskFile;

                SetStatus($"执行任务: {taskFile}");

                _orchestrator = new TaskOrchestrator(_bridge, _logger, settings);
                _orchestrator.StateChanged += state =>
                    BeginInvoke(() => SetStatus($"状态: {state}"));
                _orchestrator.HaltRequested += () =>
                    BeginInvoke(() =>
                    {
                        MessageBox.Show(
                            "任务进入 Halt 状态，需要人工干预。请查看日志了解详情。",
                            "需要人工干预",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        SetRunningState(false);
                        SetStatus("已停止（等待人工干预）");
                    });

                await _orchestrator.StartTaskAsync(taskFile, ct);

                // If Halt, stop loop
                if (_orchestrator.CurrentState == OrchestratorState.Halt) break;

                // Done: clear selection and loop again
                _selectedTaskFile = string.Empty;
                await Task.Delay(2000, ct);
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.Info(OrchestratorState.Idle, "主循环已取消");
        }
        catch (Exception ex)
        {
            _logger?.Error(OrchestratorState.Idle, $"主循环异常: {ex.Message}", ex);
        }
        finally
        {
            BeginInvoke(() =>
            {
                SetRunningState(false);
                SetStatus("已停止");
            });
        }
    }

    // ── Task List ──────────────────────────────────────────────────────────

    private async void BtnRefreshTasks_Click(object sender, EventArgs e)
    {
        var owner = txtOwner.Text.Trim();
        var repo = txtRepo.Text.Trim();
        var role = cboRole.SelectedItem?.ToString() ?? "pm";
        if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repo)) return;
        await RefreshTaskListAsync(new AppSettings { Owner = owner, Repo = repo, Role = role },
            CancellationToken.None);
    }

    private async Task RefreshTaskListAsync(AppSettings settings, CancellationToken ct)
    {
        var files = await GitHubContentsApi.GetReadyTasksAsync(settings.Owner, settings.Repo, settings.Role);
        BeginInvoke(() =>
        {
            lstTasks.Items.Clear();
            foreach (var f in files)
                lstTasks.Items.Add(new ListViewItem(f));
            _logger?.Info(OrchestratorState.Idle,
                $"任务列表已刷新: {files.Count} 个待执行任务（role={settings.Role}）");
        });
    }

    private void LstTasks_DoubleClick(object sender, EventArgs e)
    {
        if (lstTasks.SelectedItems.Count > 0)
        {
            _selectedTaskFile = lstTasks.SelectedItems[0].Text;
            SetStatus($"已选中任务: {_selectedTaskFile}");
            _logger?.Info(OrchestratorState.Idle, $"用户选中任务: {_selectedTaskFile}");
        }
    }

    // ── Log Panel ──────────────────────────────────────────────────────────

    private void CboLogLevel_SelectedIndexChanged(object sender, EventArgs e)
    {
        _minLogLevel = cboLogLevel.SelectedIndex switch
        {
            1 => LogLevel.Info,
            2 => LogLevel.Warn,
            3 => LogLevel.Error,
            _ => LogLevel.Trace
        };
    }

    private void Logger_OnLog(string line, LogLevel level)
    {
        if (level < _minLogLevel) return;

        var color = level switch
        {
            LogLevel.Trace => Color.Gray,
            LogLevel.Debug => Color.DarkGray,
            LogLevel.Warn => Color.DarkOrange,
            LogLevel.Error => Color.Red,
            _ => Color.White
        };

        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLog(line, color));
        }
        else
        {
            AppendLog(line, color);
        }
    }

    private void AppendLog(string line, Color color)
    {
        rtbLog.SuspendLayout();
        rtbLog.SelectionStart = rtbLog.TextLength;
        rtbLog.SelectionLength = 0;
        rtbLog.SelectionColor = color;
        rtbLog.AppendText(line + Environment.NewLine);
        rtbLog.SelectionColor = rtbLog.ForeColor;
        rtbLog.ScrollToCaret();
        rtbLog.ResumeLayout();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void SetStatus(string text) => lblStatus.Text = text;

    private void SetRunningState(bool running)
    {
        _isRunning = running;
        btnStart.Enabled = !running;
        btnStop.Enabled = running;
    }
}
