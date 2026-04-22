namespace AutoAgents5.App;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
            components.Dispose();
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        // ── Controls ──────────────────────────────────────────────────────────
        pnlTop = new Panel();
        lblOwner = new Label();
        txtOwner = new TextBox();
        lblRepo = new Label();
        txtRepo = new TextBox();
        lblRole = new Label();
        cboRole = new ComboBox();
        btnStart = new Button();
        btnStop = new Button();
        lblStatus = new Label();

        pnlLeft = new Panel();
        lblTasks = new Label();
        lstTasks = new ListView();
        colTask = new ColumnHeader();
        btnRefreshTasks = new Button();

        splitMain = new SplitContainer();
        webView = new Microsoft.Web.WebView2.WinForms.WebView2();

        pnlLog = new Panel();
        lblLog = new Label();
        cboLogLevel = new ComboBox();
        rtbLog = new RichTextBox();

        // ── Top Panel ─────────────────────────────────────────────────────────
        pnlTop.SuspendLayout();
        pnlTop.Dock = DockStyle.Top;
        pnlTop.Height = 44;
        pnlTop.Padding = new Padding(6, 6, 6, 4);

        lblOwner.AutoSize = true; lblOwner.Text = "用户名:"; lblOwner.Top = 14; lblOwner.Left = 6;
        txtOwner.Width = 110; txtOwner.Top = 10; txtOwner.Left = 56;

        lblRepo.AutoSize = true; lblRepo.Text = "仓库名:"; lblRepo.Top = 14; lblRepo.Left = 178;
        txtRepo.Width = 130; txtRepo.Top = 10; txtRepo.Left = 228;

        lblRole.AutoSize = true; lblRole.Text = "角色:"; lblRole.Top = 14; lblRole.Left = 370;
        cboRole.Items.AddRange(new object[] { "pm", "ui", "architect", "backend", "frontend", "qa" });
        cboRole.SelectedIndex = 0; cboRole.DropDownStyle = ComboBoxStyle.DropDownList;
        cboRole.Width = 90; cboRole.Top = 10; cboRole.Left = 408;

        btnStart.Text = "▶ 启动"; btnStart.Top = 9; btnStart.Left = 510; btnStart.Width = 72;
        btnStart.BackColor = Color.FromArgb(40, 167, 69); btnStart.ForeColor = Color.White;
        btnStart.FlatStyle = FlatStyle.Flat;
        btnStart.Click += BtnStart_Click;

        btnStop.Text = "■ 停止"; btnStop.Top = 9; btnStop.Left = 590; btnStop.Width = 72;
        btnStop.BackColor = Color.FromArgb(220, 53, 69); btnStop.ForeColor = Color.White;
        btnStop.FlatStyle = FlatStyle.Flat; btnStop.Enabled = false;
        btnStop.Click += BtnStop_Click;

        lblStatus.AutoSize = false; lblStatus.Text = "就绪"; lblStatus.Top = 14; lblStatus.Left = 676;
        lblStatus.Width = 300; lblStatus.ForeColor = Color.DimGray;

        pnlTop.Controls.AddRange(new Control[]
            { lblOwner, txtOwner, lblRepo, txtRepo, lblRole, cboRole, btnStart, btnStop, lblStatus });

        // ── Left Panel (task list) ─────────────────────────────────────────────
        pnlLeft.Dock = DockStyle.Left;
        pnlLeft.Width = 230;
        pnlLeft.BorderStyle = BorderStyle.FixedSingle;

        lblTasks.Dock = DockStyle.Top; lblTasks.Text = "  待执行任务"; lblTasks.Height = 24;
        lblTasks.Font = new Font(Font.FontFamily, 9f, FontStyle.Bold);
        lblTasks.BackColor = Color.FromArgb(240, 240, 240);

        colTask.Text = "文件名"; colTask.Width = 200;
        lstTasks.Columns.Add(colTask);
        lstTasks.View = View.Details; lstTasks.FullRowSelect = true; lstTasks.GridLines = true;
        lstTasks.Dock = DockStyle.Fill;
        lstTasks.DoubleClick += LstTasks_DoubleClick;

        btnRefreshTasks.Text = "刷新列表"; btnRefreshTasks.Dock = DockStyle.Bottom;
        btnRefreshTasks.Click += BtnRefreshTasks_Click;

        pnlLeft.Controls.Add(lstTasks);
        pnlLeft.Controls.Add(btnRefreshTasks);
        pnlLeft.Controls.Add(lblTasks);

        // ── Log Panel (right bottom) ───────────────────────────────────────────
        pnlLog.Dock = DockStyle.Bottom;
        pnlLog.Height = 180;
        pnlLog.BorderStyle = BorderStyle.FixedSingle;

        lblLog.Text = "  日志"; lblLog.Height = 22; lblLog.Dock = DockStyle.Top;
        lblLog.Font = new Font(Font.FontFamily, 9f, FontStyle.Bold);
        lblLog.BackColor = Color.FromArgb(240, 240, 240);

        cboLogLevel.Items.AddRange(new object[] { "全部", "INFO+", "WARN+", "ERROR" });
        cboLogLevel.SelectedIndex = 0; cboLogLevel.DropDownStyle = ComboBoxStyle.DropDownList;
        cboLogLevel.Dock = DockStyle.Top; cboLogLevel.Height = 22;
        cboLogLevel.SelectedIndexChanged += CboLogLevel_SelectedIndexChanged;

        rtbLog.Dock = DockStyle.Fill;
        rtbLog.ReadOnly = true; rtbLog.BackColor = Color.FromArgb(30, 30, 30);
        rtbLog.ForeColor = Color.White;
        rtbLog.Font = new Font("Consolas", 8.5f);
        rtbLog.ScrollBars = RichTextBoxScrollBars.Vertical;
        rtbLog.WordWrap = false;

        pnlLog.Controls.Add(rtbLog);
        pnlLog.Controls.Add(cboLogLevel);
        pnlLog.Controls.Add(lblLog);

        // ── WebView2 (main area) ──────────────────────────────────────────────
        ((System.ComponentModel.ISupportInitialize)webView).BeginInit();
        webView.Dock = DockStyle.Fill;

        // ── Form ──────────────────────────────────────────────────────────────
        SuspendLayout();
        ClientSize = new Size(1280, 800);
        Text = "AutoAgents5 – GitHub Copilot Agents 自动化";
        MinimumSize = new Size(900, 600);

        Controls.Add(webView);        // Fill (must be added before pnlLeft/pnlLog)
        Controls.Add(pnlLeft);        // Left
        Controls.Add(pnlLog);         // Bottom
        Controls.Add(pnlTop);         // Top

        pnlTop.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)webView).EndInit();
        ResumeLayout(false);
    }

    #endregion

    private Panel pnlTop = null!;
    private Label lblOwner = null!;
    private TextBox txtOwner = null!;
    private Label lblRepo = null!;
    private TextBox txtRepo = null!;
    private Label lblRole = null!;
    private ComboBox cboRole = null!;
    private Button btnStart = null!, btnStop = null!;
    private Label lblStatus = null!;

    private Panel pnlLeft = null!;
    private Label lblTasks = null!;
    private ListView lstTasks = null!;
    private ColumnHeader colTask = null!;
    private Button btnRefreshTasks = null!;

    private SplitContainer splitMain = null!;
    private Microsoft.Web.WebView2.WinForms.WebView2 webView = null!;

    private Panel pnlLog = null!;
    private Label lblLog = null!;
    private ComboBox cboLogLevel = null!;
    private RichTextBox rtbLog = null!;
}
