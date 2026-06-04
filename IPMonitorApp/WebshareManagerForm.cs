using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using IPMonitorCore;

namespace IPMonitorApp
{
    public class WebshareManagerForm : Form
    {
        private TabControl tabs = null!;
        private DataGridView dgvAuthIps = null!;
        private DataGridView dgvProxies = null!;
        private DataGridView dgvReplacements = null!;
        private TextBox txtLog = null!;
        private TextBox txtIp = null!;
        private Button btnAddIp = null!;
        private Button btnDeleteIp = null!;
        private Button btnRefreshIps = null!;
        private Button btnListProxies = null!;
        private Button btnListReplacements = null!;
        private Label lblStatus = null!;
        private TabPage tabSettings = null!;
        private TabPage tabLogs = null!;
        private FlowLayoutPanel settingsFlow = null!;

        private CheckBox chkEnableDebug = null!;
        private CheckBox chkWebshareEnabled = null!;
        private CheckBox chkStartMinimizedToTray = null!;
        private NumericUpDown numLogSizeKB = null!;
        private NumericUpDown numLogCopies = null!;
        private NumericUpDown numPingInterval = null!;
        private NumericUpDown numApiInterval = null!;
        private NumericUpDown numMaxFailures = null!;
        private NumericUpDown numHeartbeatMinutes = null!;
        private TextBox txtPingTargets = null!;
        private TextBox txtApiKeyMasked = null!;
        private TextBox txtConfigPath = null!;
        private TextBox txtLogPath = null!;
        private Button btnToggleApiKey = null!;
        private Button btnChangeKey = null!;
        private Button btnClearApiKey = null!;
        private Button btnSaveSettings = null!;
        private Button btnInstallStartupTask = null!;
        private Button btnRemoveStartupTask = null!;
        private Button btnCheckStartupTask = null!;
        private Button btnOpenConfig = null!;
        private Button btnOpenLog = null!;
        private Button btnReloadLog = null!;
        private Button btnClearSessionLog = null!;
        private Button btnClearFileLog = null!;
        private Button btnStartMonitoring = null!;
        private Button btnStopMonitoring = null!;
        private Button btnExitApplication = null!;

        private readonly Func<Task>? _startMonitoringAsync;
        private readonly Func<Task>? _stopMonitoringAsync;
        private readonly Func<Task>? _exitApplicationAsync;
        private readonly Action<MonitorStatus>? _setMonitorStatus;

        private IPMonitorRR? _localMonitor;
        private bool showingApiKey;

        public WebshareManagerForm()
            : this(null, null, null, null)
        {
        }

        public WebshareManagerForm(Func<Task>? startMonitoringAsync, Func<Task>? stopMonitoringAsync, Func<Task>? exitApplicationAsync, Action<MonitorStatus>? setMonitorStatus = null)
        {
            _startMonitoringAsync = startMonitoringAsync;
            _stopMonitoringAsync = stopMonitoringAsync;
            _exitApplicationAsync = exitApplicationAsync;
            _setMonitorStatus = setMonitorStatus;

            Text = "Webshare Proxy Utility";
            Width = 1280;
            Height = 900;
            MinimumSize = new Size(1120, 760);
            StartPosition = FormStartPosition.CenterScreen;

            InitializeUi();
            WireEvents();
            SubscribeToLogger();

            Load += async (_, __) => await OnLoadInit();
        }

        private void InitializeUi()
        {
            tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                DrawMode = TabDrawMode.OwnerDrawFixed,
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(170, 34),
                Padding = new Point(18, 6),
                Font = new Font(Font.FontFamily, 9.5f, FontStyle.Bold)
            };
            tabs.DrawItem += Tabs_DrawItem;

            var tabIps = new TabPage("Authorized IPs") { Name = "tabIps" };
            var tabProxies = new TabPage("Proxies") { Name = "tabProxies" };
            var tabReplacements = new TabPage("Replacements") { Name = "tabReplacements" };
            tabSettings = new TabPage("Settings") { Name = "tabSettings" };
            tabLogs = new TabPage("Logs") { Name = "tabLogs" };

            tabs.TabPages.AddRange(new[] { tabIps, tabProxies, tabReplacements, tabSettings, tabLogs });

            BuildAuthorizedIpsTab(tabIps);
            BuildProxiesTab(tabProxies);
            BuildReplacementsTab(tabReplacements);
            BuildSettingsTab(tabSettings);
            BuildLogsTab(tabLogs);

            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 82,
                Padding = new Padding(8, 6, 8, 6)
            };

            lblStatus = new Label
            {
                Text = "Status: Starting...",
                Dock = DockStyle.Top,
                Height = 26,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 0, 0, 0)
            };

            var sessionControlsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 6, 0, 0)
            };

            var lblSessionControls = new Label
            {
                Text = "Session Controls:",
                Left = 0,
                Top = 12,
                Width = 112,
                Height = 24,
                TextAlign = ContentAlignment.MiddleLeft
            };

            btnStartMonitoring = new Button { Text = "Start Monitoring", Left = 120, Top = 6, Width = 150, Height = 32 };
            btnStopMonitoring = new Button { Text = "Stop Monitoring", Left = 282, Top = 6, Width = 150, Height = 32 };
            btnExitApplication = new Button { Text = "Exit Application", Left = 444, Top = 6, Width = 150, Height = 32 };

            sessionControlsPanel.Controls.AddRange(new Control[]
            {
                lblSessionControls,
                btnStartMonitoring,
                btnStopMonitoring,
                btnExitApplication
            });

            headerPanel.Controls.Add(sessionControlsPanel);
            headerPanel.Controls.Add(lblStatus);

            Controls.Add(tabs);
            Controls.Add(headerPanel);
        }

        private void Tabs_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (sender is not TabControl tabControl)
                return;

            var page = tabControl.TabPages[e.Index];
            var bounds = e.Bounds;
            var selected = e.Index == tabControl.SelectedIndex;

            var backColor = selected ? SystemColors.Window : SystemColors.ControlLight;
            var borderColor = selected ? SystemColors.Highlight : SystemColors.ControlDark;
            var textColor = selected ? SystemColors.ControlText : SystemColors.ControlDarkDark;

            using var backBrush = new SolidBrush(backColor);
            using var textBrush = new SolidBrush(textColor);
            using var borderPen = new Pen(borderColor);

            e.Graphics.FillRectangle(backBrush, bounds);
            e.Graphics.DrawRectangle(borderPen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);

            if (selected)
            {
                using var highlightPen = new Pen(SystemColors.Highlight, 3);
                e.Graphics.DrawLine(highlightPen, bounds.Left + 2, bounds.Top + 1, bounds.Right - 3, bounds.Top + 1);
            }

            TextRenderer.DrawText(
                e.Graphics,
                page.Text,
                tabControl.Font,
                bounds,
                textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void BuildAuthorizedIpsTab(TabPage tabIps)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(8)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var ipsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0)
            };

            var lblIp = new Label
            {
                Text = "IP:",
                Left = 0,
                Top = 20,
                Width = 26,
                Height = 24,
                TextAlign = ContentAlignment.MiddleLeft
            };

            txtIp = new TextBox
            {
                Left = 32,
                Top = 16,
                Width = 320,
                Height = 28,
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
            };

            btnAddIp = new Button { Text = "Add IP", Left = 368, Top = 14, Width = 100, Height = 34 };
            btnDeleteIp = new Button { Text = "Delete Selected", Left = 480, Top = 14, Width = 145, Height = 34 };
            btnRefreshIps = new Button { Text = "Refresh", Left = 638, Top = 14, Width = 100, Height = 34 };

            ipsPanel.Resize += (_, __) =>
            {
                var minimumButtonLeft = 368;
                var available = ipsPanel.ClientSize.Width - 760;
                txtIp.Width = Math.Max(240, 320 + Math.Max(0, available));
                btnAddIp.Left = txtIp.Right + 16;
                btnDeleteIp.Left = btnAddIp.Right + 12;
                btnRefreshIps.Left = btnDeleteIp.Right + 12;

                if (btnRefreshIps.Right > ipsPanel.ClientSize.Width - 4)
                {
                    txtIp.Width = Math.Max(180, ipsPanel.ClientSize.Width - 430);
                    btnAddIp.Left = Math.Max(minimumButtonLeft, txtIp.Right + 12);
                    btnDeleteIp.Left = btnAddIp.Right + 10;
                    btnRefreshIps.Left = btnDeleteIp.Right + 10;
                }
            };

            ipsPanel.Controls.AddRange(new Control[] { lblIp, txtIp, btnAddIp, btnDeleteIp, btnRefreshIps });

            dgvAuthIps = new DataGridView
            {
                Dock = DockStyle.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Margin = new Padding(0)
            };

            layout.Controls.Add(ipsPanel, 0, 0);
            layout.Controls.Add(dgvAuthIps, 0, 1);
            tabIps.Controls.Add(layout);
        }

        private void BuildProxiesTab(TabPage tabProxies)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(8)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var proxiesPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0)
            };

            btnListProxies = new Button
            {
                Text = "List Proxies",
                Left = 0,
                Top = 14,
                Width = 130,
                Height = 34
            };

            proxiesPanel.Controls.Add(btnListProxies);

            dgvProxies = new DataGridView
            {
                Dock = DockStyle.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Margin = new Padding(0)
            };

            layout.Controls.Add(proxiesPanel, 0, 0);
            layout.Controls.Add(dgvProxies, 0, 1);
            tabProxies.Controls.Add(layout);
        }

        private void BuildReplacementsTab(TabPage tabReplacements)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(8)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var repsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0)
            };

            btnListReplacements = new Button
            {
                Text = "List Replacements",
                Left = 0,
                Top = 14,
                Width = 165,
                Height = 34
            };

            repsPanel.Controls.Add(btnListReplacements);

            dgvReplacements = new DataGridView
            {
                Dock = DockStyle.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Margin = new Padding(0)
            };

            layout.Controls.Add(repsPanel, 0, 0);
            layout.Controls.Add(dgvReplacements, 0, 1);
            tabReplacements.Controls.Add(layout);
        }

        private void BuildSettingsTab(TabPage tab)
        {
            var outer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(0)
            };

            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));

            settingsFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(12, 10, 12, 10)
            };

            settingsFlow.Controls.Add(BuildConnectionGroup());
            settingsFlow.Controls.Add(BuildLoggingGroup());
            settingsFlow.Controls.Add(BuildMonitoringGroup());
            settingsFlow.Controls.Add(BuildPingTargetsGroup());
            settingsFlow.Controls.Add(BuildCredentialsGroup());
            settingsFlow.Controls.Add(BuildStartupGroup());
            settingsFlow.Controls.Add(BuildPathsGroup());

            settingsFlow.Resize += (_, __) => ResizeSettingsGroups();

            var footer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 8, 12, 10)
            };

            btnSaveSettings = new Button
            {
                Text = "Save Settings",
                Height = 34,
                Width = 180,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };

            footer.Controls.Add(btnSaveSettings);
            footer.Resize += (_, __) =>
            {
                btnSaveSettings.Left = Math.Max(12, footer.ClientSize.Width - btnSaveSettings.Width - 12);
                btnSaveSettings.Top = 8;
            };

            outer.Controls.Add(settingsFlow, 0, 0);
            outer.Controls.Add(footer, 0, 1);
            tab.Controls.Add(outer);

            ResizeSettingsGroups();
        }

        private void ResizeSettingsGroups()
        {
            if (settingsFlow == null)
                return;

            var width = Math.Max(
                620,
                settingsFlow.ClientSize.Width - settingsFlow.Padding.Left - settingsFlow.Padding.Right - SystemInformation.VerticalScrollBarWidth - 8);

            foreach (Control control in settingsFlow.Controls)
            {
                control.Width = width;
                control.Margin = new Padding(0, 0, 0, 10);
            }
        }

        private Control BuildConnectionGroup()
        {
            var gb = new GroupBox { Text = "Connection / Webshare", Height = 82 };
            chkWebshareEnabled = new CheckBox
            {
                Text = "Enable Webshare IP validation",
                Left = 14,
                Top = 32,
                AutoSize = true
            };

            gb.Controls.Add(chkWebshareEnabled);
            return gb;
        }

        private Control BuildLoggingGroup()
        {
            var gb = new GroupBox { Text = "Logging", Height = 132 };

            chkEnableDebug = new CheckBox { Text = "Enable debug logging", Left = 14, Top = 28, AutoSize = true };
            var lblLogSize = new Label { Text = "Max log size (KB):", Left = 14, Top = 62, Width = 170 };
            numLogSizeKB = new NumericUpDown { Left = 190, Top = 58, Width = 110, Minimum = 1, Maximum = 1048576, Value = 500 };
            var lblLogCopies = new Label { Text = "Log files to keep:", Left = 14, Top = 94, Width = 170 };
            numLogCopies = new NumericUpDown { Left = 190, Top = 90, Width = 90, Minimum = 1, Maximum = 50, Value = 5 };

            gb.Controls.AddRange(new Control[] { chkEnableDebug, lblLogSize, numLogSizeKB, lblLogCopies, numLogCopies });
            return gb;
        }

        private Control BuildMonitoringGroup()
        {
            var gb = new GroupBox { Text = "Monitoring / Intervals", Height = 164 };

            var lblPingInterval = new Label { Text = "Internet check interval (sec):", Left = 14, Top = 30, Width = 245 };
            numPingInterval = new NumericUpDown { Left = 285, Top = 26, Width = 90, Minimum = 1, Maximum = 3600, Value = 5 };

            var lblApiInterval = new Label { Text = "Public IP/Webshare check interval (sec):", Left = 14, Top = 64, Width = 265 };
            numApiInterval = new NumericUpDown { Left = 285, Top = 60, Width = 90, Minimum = 30, Maximum = 86400, Value = 60 };

            var lblMaxFailures = new Label { Text = "Max consecutive failures:", Left = 14, Top = 98, Width = 245 };
            numMaxFailures = new NumericUpDown { Left = 285, Top = 94, Width = 90, Minimum = 1, Maximum = 100, Value = 5 };

            var lblHeartbeat = new Label { Text = "Heartbeat log interval (min):", Left = 14, Top = 132, Width = 245 };
            numHeartbeatMinutes = new NumericUpDown { Left = 285, Top = 128, Width = 90, Minimum = 1, Maximum = 1440, Value = 60 };

            gb.Controls.AddRange(new Control[] { lblPingInterval, numPingInterval, lblApiInterval, numApiInterval, lblMaxFailures, numMaxFailures, lblHeartbeat, numHeartbeatMinutes });
            return gb;
        }

        private Control BuildPingTargetsGroup()
        {
            var gb = new GroupBox { Text = "Internet Detection Targets", Height = 250 };

            var lbl = new Label
            {
                Text = "One IP, host, DNS name, or URL per line. Internet is considered UP when any target responds.",
                Left = 14,
                Top = 26,
                Height = 36,
                AutoSize = false
            };

            txtPingTargets = new TextBox
            {
                Left = 14,
                Top = 64,
                Height = 160,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9),
                WordWrap = false,
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom
            };

            gb.Resize += (_, __) =>
            {
                var width = Math.Max(360, gb.ClientSize.Width - 28);
                lbl.Width = width;
                txtPingTargets.Width = width;
                txtPingTargets.Height = Math.Max(120, gb.ClientSize.Height - txtPingTargets.Top - 18);
            };

            gb.Controls.Add(lbl);
            gb.Controls.Add(txtPingTargets);
            return gb;
        }

        private Control BuildCredentialsGroup()
        {
            var gb = new GroupBox { Text = "Credentials", Height = 166 };

            var lbl = new Label
            {
                Text = "Webshare API key is stored in Windows Credential Manager. For boot-time headless mode, an encrypted DPAPI machine-scope copy is stored in the shared config.",
                Left = 14,
                Top = 24,
                Height = 34,
                AutoSize = false
            };

            txtApiKeyMasked = new TextBox
            {
                Left = 14,
                Top = 70,
                Width = 620,
                UseSystemPasswordChar = true,
                ReadOnly = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
            };

            btnToggleApiKey = new Button { Text = "Show", Top = 68, Width = 84, Height = 30 };
            btnChangeKey = new Button { Text = "Change Key...", Top = 68, Width = 130, Height = 30 };
            btnClearApiKey = new Button { Text = "Clear API Key", Top = 106, Width = 130, Height = 30 };

            gb.Resize += (_, __) =>
            {
                var rightPadding = 18;
                lbl.Width = Math.Max(480, gb.ClientSize.Width - 28);
                btnChangeKey.Left = Math.Max(360, gb.ClientSize.Width - btnChangeKey.Width - rightPadding);
                btnToggleApiKey.Left = btnChangeKey.Left - btnToggleApiKey.Width - 10;
                btnClearApiKey.Left = btnChangeKey.Left;
                txtApiKeyMasked.Width = Math.Max(260, btnToggleApiKey.Left - txtApiKeyMasked.Left - 12);
            };

            gb.Controls.AddRange(new Control[] { lbl, txtApiKeyMasked, btnToggleApiKey, btnChangeKey, btnClearApiKey });
            return gb;
        }

        private Control BuildStartupGroup()
        {
            var gb = new GroupBox { Text = "Startup / Headless Mode", Height = 218 };

            var lbl = new Label
            {
                Text = "Install a boot-time scheduled task to run this same EXE with --headless as SYSTEM before user logon. Installing or removing the task requires administrator rights.",
                Left = 14,
                Top = 28,
                Height = 44,
                AutoSize = false
            };

            var lblAdmin = new Label
            {
                Text = StartupTaskHelper.IsRunningElevated()
                    ? "Current process: running elevated. Boot startup install/remove is available."
                    : "Current process: not elevated. Install/remove will prompt to restart this utility as administrator.",
                Left = 14,
                Top = 76,
                Height = 24,
                AutoSize = false,
                ForeColor = StartupTaskHelper.IsRunningElevated() ? Color.DarkGreen : Color.DarkOrange
            };

            chkStartMinimizedToTray = new CheckBox
            {
                Text = "Start in system tray only when launched interactively",
                Left = 14,
                Top = 108,
                Width = 520,
                Height = 24
            };

            btnInstallStartupTask = new Button { Text = "Install Boot Startup Task", Left = 14, Top = 154, Width = 215, Height = 32 };
            btnRemoveStartupTask = new Button { Text = "Remove Startup Task", Left = 244, Top = 154, Width = 180, Height = 32 };
            btnCheckStartupTask = new Button { Text = "Check Status", Left = 438, Top = 154, Width = 130, Height = 32 };

            gb.Resize += (_, __) =>
            {
                var width = Math.Max(480, gb.ClientSize.Width - 28);
                lbl.Width = width;
                lblAdmin.Width = width;
                chkStartMinimizedToTray.Width = width;
            };

            gb.Controls.AddRange(new Control[] { lbl, lblAdmin, chkStartMinimizedToTray, btnInstallStartupTask, btnRemoveStartupTask, btnCheckStartupTask });
            return gb;
        }

        private Control BuildSessionControlsGroup()
        {
            var gb = new GroupBox { Text = "Current Session Controls", Height = 118 };

            var lbl = new Label
            {
                Text = "These controls affect the currently running tray session. Use them when the manager window is already open and you do not want to use the tray icon menu.",
                Left = 14,
                Top = 26,
                Height = 34,
                AutoSize = false
            };

            btnStartMonitoring = new Button { Text = "Start Monitoring", Left = 14, Top = 70, Width = 150, Height = 32 };
            btnStopMonitoring = new Button { Text = "Stop Monitoring", Left = 178, Top = 70, Width = 150, Height = 32 };
            btnExitApplication = new Button { Text = "Exit Application", Left = 342, Top = 70, Width = 150, Height = 32 };

            gb.Resize += (_, __) =>
            {
                lbl.Width = Math.Max(480, gb.ClientSize.Width - 28);
            };

            gb.Controls.AddRange(new Control[] { lbl, btnStartMonitoring, btnStopMonitoring, btnExitApplication });
            return gb;
        }

        private Control BuildPathsGroup()
        {
            var gb = new GroupBox { Text = "Paths", Height = 150 };

            var lblConfig = new Label { Text = "Config:", Left = 14, Top = 36, Width = 80 };
            txtConfigPath = new TextBox { Left = 100, Top = 32, Width = 780, ReadOnly = true, Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right };
            btnOpenConfig = new Button { Text = "Open", Top = 30, Width = 82, Height = 30, Anchor = AnchorStyles.Top | AnchorStyles.Right };

            var lblLog = new Label { Text = "Log:", Left = 14, Top = 82, Width = 80 };
            txtLogPath = new TextBox { Left = 100, Top = 78, Width = 780, ReadOnly = true, Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right };
            btnOpenLog = new Button { Text = "Open", Top = 76, Width = 82, Height = 30, Anchor = AnchorStyles.Top | AnchorStyles.Right };

            gb.Resize += (_, __) =>
            {
                var buttonLeft = Math.Max(360, gb.ClientSize.Width - btnOpenConfig.Width - 18);
                btnOpenConfig.Left = buttonLeft;
                btnOpenLog.Left = buttonLeft;
                var textWidth = Math.Max(300, buttonLeft - txtConfigPath.Left - 12);
                txtConfigPath.Width = textWidth;
                txtLogPath.Width = textWidth;
            };

            gb.Controls.AddRange(new Control[] { lblConfig, txtConfigPath, btnOpenConfig, lblLog, txtLogPath, btnOpenLog });
            return gb;
        }

        private void BuildLogsTab(TabPage tab)
        {
            var panel = new Panel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(6) };

            btnClearSessionLog = new Button { Text = "Clear Session View", Left = 6, Top = 7, Width = 140, Height = 30 };
            btnReloadLog = new Button { Text = "Load File Log", Left = 156, Top = 7, Width = 115, Height = 30 };
            btnClearFileLog = new Button { Text = "Reset Log File", Left = 281, Top = 7, Width = 115, Height = 30 };

            panel.Controls.AddRange(new Control[]
            {
                btnClearSessionLog,
                btnReloadLog,
                btnClearFileLog
            });

            txtLog = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Both,
                ReadOnly = true,
                Font = new Font("Consolas", 9),
                WordWrap = false
            };

            tab.Controls.Add(txtLog);
            tab.Controls.Add(panel);
        }

        private void WireEvents()
        {
            btnAddIp.Click += async (_, __) => await AddIp();
            btnDeleteIp.Click += async (_, __) => await DeleteSelectedIp();
            btnRefreshIps.Click += async (_, __) => await RefreshIps();
            btnListProxies.Click += async (_, __) => await ListProxies();
            btnListReplacements.Click += async (_, __) => await ListReplacements();
            btnSaveSettings.Click += (_, __) => SaveSettings();
            btnChangeKey.Click += (_, __) => ChangeKey();
            btnClearApiKey.Click += (_, __) => ClearApiKeyFromUi();
            btnToggleApiKey.Click += (_, __) => ToggleApiKeyDisplay();
            btnInstallStartupTask.Click += (_, __) => InstallStartupTask();
            btnRemoveStartupTask.Click += (_, __) => RemoveStartupTask();
            btnCheckStartupTask.Click += (_, __) => CheckStartupTask();
            btnOpenConfig.Click += (_, __) => OpenPath(ConfigLoader.ConfigFile);
            btnOpenLog.Click += (_, __) => OpenPath(Logger.LogFile);
            btnReloadLog.Click += (_, __) => LoadExistingLogIntoGui();
            btnClearSessionLog.Click += (_, __) => ClearSessionLogView();
            btnClearFileLog.Click += (_, __) => ResetFileLog();
            btnStartMonitoring.Click += async (_, __) => await StartMonitoringFromUi();
            btnStopMonitoring.Click += async (_, __) => await StopMonitoringFromUi();
            btnExitApplication.Click += async (_, __) => await ExitApplicationFromUi();
        }

        private async Task StartMonitoringFromUi()
        {
            try
            {
                btnStartMonitoring.Enabled = false;

                if (_startMonitoringAsync == null)
                {
                    Logger.Warn("UI", "Start Monitoring was requested without a tray session controller. Starting a local monitoring session for this process.");
                    var cfg = ConfigLoader.Load();
                    PingLoop.Start(cfg);
                    _localMonitor ??= new IPMonitorRR(cfg);
                    _localMonitor.Start();
                }
                else
                {
                    await _startMonitoringAsync();
                }

                _setMonitorStatus?.Invoke(MonitorStatus.Ok);
                lblStatus.Text = "Status: Monitoring started";
            }
            catch (Exception ex)
            {
                Logger.Error("UI", "Failed to start monitoring from manager window: " + ex.Message);
                _setMonitorStatus?.Invoke(MonitorStatus.Warning);
                MessageBox.Show("Failed to start monitoring: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (!IsDisposed)
                    btnStartMonitoring.Enabled = true;
            }
        }

        private async Task StopMonitoringFromUi()
        {
            try
            {
                btnStopMonitoring.Enabled = false;

                if (_stopMonitoringAsync == null)
                {
                    Logger.Warn("UI", "Stop Monitoring was requested without a tray session controller. Stopping local monitoring for this process.");
                    try { PingLoop.Stop(); } catch { }
                    if (_localMonitor != null)
                        await _localMonitor.StopAsync();
                }
                else
                {
                    await _stopMonitoringAsync();
                }

                _setMonitorStatus?.Invoke(MonitorStatus.Paused);
                lblStatus.Text = "Status: Monitoring stopped";
            }
            catch (Exception ex)
            {
                Logger.Error("UI", "Failed to stop monitoring from manager window: " + ex.Message);
                _setMonitorStatus?.Invoke(MonitorStatus.Warning);
                MessageBox.Show("Failed to stop monitoring: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (!IsDisposed)
                    btnStopMonitoring.Enabled = true;
            }
        }

        private async Task ExitApplicationFromUi()
        {
            try
            {
                var result = MessageBox.Show(
                    "Exit WebshareProxyUtil?\r\n\r\nThis will stop monitoring, remove the tray icon, and close the current application session.",
                    "Exit WebshareProxyUtil",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2);

                if (result != DialogResult.Yes)
                    return;

                btnExitApplication.Enabled = false;

                if (_exitApplicationAsync == null)
                {
                    Logger.Info("App", "Exit requested from standalone manager window.");
                    try { PingLoop.Stop(); } catch { }
                    try { if (_localMonitor != null) await _localMonitor.StopAsync(); } catch { }
                    Close();
                    try { Application.Exit(); } catch { }
                    Environment.Exit(0);
                    return;
                }

                await _exitApplicationAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("UI", "Failed to exit from manager window: " + ex.Message);
                MessageBox.Show("Failed to exit application: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (!IsDisposed)
                    btnExitApplication.Enabled = true;
            }
        }

        private void SubscribeToLogger()
        {
            try
            {
                Logger.OnLog += line =>
                {
                    if (IsDisposed || txtLog == null || !txtLog.IsHandleCreated)
                        return;

                    try
                    {
                        BeginInvoke((Action)(() =>
                        {
                            AppendLogLineToGui(line);
                        }));
                    }
                    catch
                    {
                    }
                };
            }
            catch
            {
            }
        }

        private async Task OnLoadInit()
        {
            try
            {
                var cfg = ConfigLoader.Load();
                Logger.Setup(cfg.settings.maxLogSizeKB, cfg.settings.maxLogFiles, cfg.settings.debug);
                Logger.Info("UI", "Webshare manager form opened.");

                LoadSettingsToUi(cfg);
                InitializeSessionLogView();
                UpdateStartupTaskStatusLabel();

                var hasApiKey = EnsureApiKeyConfigured(firstRunPrompt: true);
                LoadSettingsToUi(ConfigLoader.Load());

                if (hasApiKey)
                    await RefreshIps();
                else
                    lblStatus.Text = "Status: Webshare API key required";
            }
            catch (Exception ex)
            {
                Logger.Error("UI", "Failed to initialize manager: " + ex.Message);
                lblStatus.Text = "Status: Initialization failed";
            }
        }

        private void LoadSettingsToUi(AppConfig cfg)
        {
            chkEnableDebug.Checked = cfg.settings.debug;
            chkWebshareEnabled.Checked = cfg.webshare.Enabled;
            chkStartMinimizedToTray.Checked = cfg.settings.startMinimizedToTray;
            numLogSizeKB.Value = Clamp(cfg.settings.maxLogSizeKB, numLogSizeKB.Minimum, numLogSizeKB.Maximum);
            numLogCopies.Value = Clamp(cfg.settings.maxLogFiles, numLogCopies.Minimum, numLogCopies.Maximum);
            numPingInterval.Value = Clamp(cfg.settings.pingIntervalSeconds, numPingInterval.Minimum, numPingInterval.Maximum);
            numApiInterval.Value = Clamp(cfg.webshare.IntervalSeconds > 0 ? cfg.webshare.IntervalSeconds : cfg.settings.ipCheckIntervalSeconds, numApiInterval.Minimum, numApiInterval.Maximum);
            numMaxFailures.Value = Clamp(cfg.settings.MaxFailures, numMaxFailures.Minimum, numMaxFailures.Maximum);
            numHeartbeatMinutes.Value = Clamp(cfg.settings.heartbeatIntervalMinutes, numHeartbeatMinutes.Minimum, numHeartbeatMinutes.Maximum);
            txtPingTargets.Text = string.Join(Environment.NewLine, cfg.settings.pingTargets ?? ConfigLoader.CleanTargets(Array.Empty<string>()));
            txtApiKeyMasked.Text = ConfigUtils.GetApiKey();
            txtConfigPath.Text = ConfigLoader.ConfigFile;
            txtLogPath.Text = Logger.LogFile;
            showingApiKey = false;
            txtApiKeyMasked.UseSystemPasswordChar = true;
            btnToggleApiKey.Text = "Show";
            lblStatus.Text = "Status: Loaded settings";
        }

        private static decimal Clamp(int value, decimal min, decimal max)
        {
            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }

        private void SaveSettings()
        {
            try
            {
                var cfg = ConfigLoader.Load();
                cfg.settings.debug = chkEnableDebug.Checked;
                cfg.settings.maxLogSizeKB = (int)numLogSizeKB.Value;
                cfg.settings.maxLogFiles = (int)numLogCopies.Value;
                cfg.settings.pingIntervalSeconds = (int)numPingInterval.Value;
                cfg.settings.ipCheckIntervalSeconds = (int)numApiInterval.Value;
                cfg.settings.MaxFailures = (int)numMaxFailures.Value;
                cfg.settings.heartbeatIntervalMinutes = (int)numHeartbeatMinutes.Value;
                cfg.webshare.Enabled = chkWebshareEnabled.Checked;
                cfg.settings.startMinimizedToTray = chkStartMinimizedToTray.Checked;
                cfg.settings.firstRunComplete = true;
                cfg.webshare.IntervalSeconds = (int)numApiInterval.Value;
                cfg.settings.pingTargets = ConfigLoader.CleanTargets(txtPingTargets.Lines);

                ConfigLoader.Save(cfg);
                Logger.Setup(cfg.settings.maxLogSizeKB, cfg.settings.maxLogFiles, cfg.settings.debug);
                Logger.Info("UI", "Settings saved from GUI.");

                try { PingLoop.Stop(); } catch { }
                try { PingLoop.Start(cfg); } catch (Exception ex) { Logger.Error("UI", "Failed to restart PingLoop after settings save: " + ex.Message); }

                LoadSettingsToUi(cfg);

                MessageBox.Show("Settings saved successfully.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Logger.Error("UI", "Failed to save settings: " + ex.Message);
                MessageBox.Show("Failed to save settings: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ToggleApiKeyDisplay()
        {
            showingApiKey = !showingApiKey;
            txtApiKeyMasked.UseSystemPasswordChar = !showingApiKey;
            btnToggleApiKey.Text = showingApiKey ? "Hide" : "Show";
        }

        private void ChangeKey()
        {
            SaveApiKeyFromPrompt(firstRunPrompt: false);
        }

        private bool EnsureApiKeyConfigured(bool firstRunPrompt)
        {
            var existing = ConfigUtils.GetApiKey();
            if (!string.IsNullOrWhiteSpace(existing))
                return true;

            Logger.Warn("UI", "No Webshare API key is configured. Prompting user for first-run setup.");
            tabs.SelectedTab = tabSettings;

            MessageBox.Show(
                "A Webshare API key is required before this utility can manage authorized IPs or proxies.\r\n\r\nClick OK to open Settings and enter the key.",
                "Webshare API Key Required",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            return SaveApiKeyFromPrompt(firstRunPrompt);
        }

        private bool SaveApiKeyFromPrompt(bool firstRunPrompt)
        {
            var prompt = firstRunPrompt
                ? "First-run setup: enter your Webshare API key. This is required before the utility can list proxies, manage authorized IPs, or auto-authorize your public IP."
                : "Enter your Webshare API key. This replaces the currently stored key.";

            using var dlg = new ChangeKeyForm(prompt);
            if (dlg.ShowDialog(this) != DialogResult.OK)
            {
                Logger.Warn("UI", "Webshare API key prompt was cancelled.");
                return false;
            }

            var cfg = ConfigLoader.Load();
            var target = string.IsNullOrWhiteSpace(cfg.webshare.ApiKeyCredentialTarget)
                ? "WebshareProxyUtil:WebshareAPIKey"
                : cfg.webshare.ApiKeyCredentialTarget;

            CredentialStore.SaveCredential(target, dlg.ApiKey);

            if (!target.Equals("IPMonitorApp:WebshareAPIKey", StringComparison.OrdinalIgnoreCase))
                CredentialStore.SaveCredential("IPMonitorApp:WebshareAPIKey", dlg.ApiKey);

            cfg.settings.apiKey = ConfigSecretProtector.Protect(dlg.ApiKey);
            ConfigLoader.Save(cfg);
            txtApiKeyMasked.Text = dlg.ApiKey;
            Logger.Info("UI", "Webshare API key updated in Windows Credential Manager and encrypted shared ProgramData config for headless startup mode.");
            MessageBox.Show("API key updated. It was saved in Windows Credential Manager and an encrypted DPAPI machine-scope copy was stored in the shared config so boot-time headless mode can use it.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }

        private void ClearApiKeyFromUi()
        {
            try
            {
                var result = MessageBox.Show(
                    "Clear the stored Webshare API key?\r\n\r\nThis removes the Credential Manager entries and clears the encrypted shared config copy. Webshare actions and headless mode will not work until a new key is entered.",
                    "Clear Webshare API Key",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);

                if (result != DialogResult.Yes)
                    return;

                ConfigUtils.ClearApiKey();
                txtApiKeyMasked.Text = string.Empty;
                showingApiKey = false;
                txtApiKeyMasked.UseSystemPasswordChar = true;
                btnToggleApiKey.Text = "Show";
                lblStatus.Text = "Status: Webshare API key cleared";
                Logger.Warn("UI", "Webshare API key cleared from Credential Manager targets and shared config.");
                MessageBox.Show(
                    "The stored Webshare API key was cleared. Open Settings and use Change Key when you are ready to enter a new one.",
                    "API Key Cleared",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Logger.Error("UI", "Failed to clear Webshare API key: " + ex.Message);
                MessageBox.Show("Failed to clear API key: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task RefreshIps()
        {
            try
            {
                var apiKey = ConfigUtils.GetApiKey();
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    Logger.Warn("Webshare", "No API key configured; authorized IPs cannot be loaded.");
                    return;
                }

                var page = await WebshareUtil.ListAuthorizedIpsAsync(apiKey);

                dgvAuthIps.Columns.Clear();
                dgvAuthIps.DataSource = page.Results.Select(r => new
                {
                    r.Id,
                    Ip = r.IpAddress,
                    Created = r.CreatedAt.HasValue ? r.CreatedAt.Value.ToString("g") : "",
                    LastUsed = r.LastUsedAt.HasValue ? r.LastUsedAt.Value.ToString("g") : ""
                }).ToList();

                Logger.Info("Webshare", $"Loaded {page.Results.Count} authorized IPs.");
                lblStatus.Text = "Status: Authorized IPs refreshed";
            }
            catch (Exception ex)
            {
                Logger.Error("Webshare", "Error loading IPs: " + ex.Message);
                lblStatus.Text = "Status: Failed to refresh authorized IPs";
            }
        }

        private async Task AddIp()
        {
            try
            {
                var apiKey = ConfigUtils.GetApiKey();
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    Logger.Warn("Webshare", "No API key configured; cannot add IP.");
                    return;
                }

                var ip = string.IsNullOrWhiteSpace(txtIp.Text)
                    ? await WebshareUtil.WhatsMyIpAsync(apiKey)
                    : txtIp.Text.Trim();

                if (string.IsNullOrWhiteSpace(ip))
                {
                    Logger.Warn("Webshare", "No IP to add.");
                    return;
                }

                var obj = await WebshareUtil.CreateAuthorizedIpAsync(apiKey, ip);
                if (obj != null)
                    Logger.Info("Webshare", $"Added authorized IP {obj.Id} {obj.IpAddress}.");

                await RefreshIps();
            }
            catch (Exception ex)
            {
                Logger.Error("Webshare", "Error adding IP: " + ex.Message);
            }
        }

        private async Task DeleteSelectedIp()
        {
            try
            {
                if (dgvAuthIps.SelectedRows.Count == 0)
                {
                    Logger.Warn("Webshare", "No authorized IP row selected.");
                    return;
                }

                var idObj = dgvAuthIps.SelectedRows[0].Cells["Id"].Value;
                if (idObj == null || !int.TryParse(idObj.ToString(), out var id))
                {
                    Logger.Warn("Webshare", "Selected authorized IP row does not contain a valid Id.");
                    return;
                }

                var apiKey = ConfigUtils.GetApiKey();
                await WebshareUtil.DeleteAuthorizedIpAsync(apiKey, id);
                Logger.Info("Webshare", $"Deleted authorized IP {id}.");
                await RefreshIps();
            }
            catch (Exception ex)
            {
                Logger.Error("Webshare", "Error deleting IP: " + ex.Message);
            }
        }

        private async Task ListProxies()
        {
            try
            {
                var apiKey = ConfigUtils.GetApiKey();
                var res = await WebshareUtil.ListProxiesAsync(apiKey, "direct");

                dgvProxies.Columns.Clear();
                dgvProxies.DataSource = res.Results.Select(r => new
                {
                    r.Id,
                    Address = r.ProxyAddress,
                    r.Port,
                    r.Username,
                    r.CountryCode,
                    r.Valid
                }).ToList();

                Logger.Info("Webshare", $"Loaded {res.Results.Count} proxies.");
                lblStatus.Text = "Status: Proxies loaded";
            }
            catch (Exception ex)
            {
                Logger.Error("Webshare", "Error loading proxies: " + ex.Message);
                lblStatus.Text = "Status: Failed to load proxies";
            }
        }

        private async Task ListReplacements()
        {
            try
            {
                var apiKey = ConfigUtils.GetApiKey();
                var plan = await WebshareUtil.GetActivePlanAsync(apiKey);
                if (plan == null)
                {
                    Logger.Warn("Webshare", "No active subscription plan found.");
                    return;
                }

                Logger.Info("Webshare", $"Active plan ID: {plan.Id}, Status: {plan.Status}, Proxies: {plan.Proxy_Count}.");

                var res = await WebshareUtil.ListProxyReplacementsForActivePlanAsync(apiKey);
                dgvReplacements.Columns.Clear();
                dgvReplacements.DataSource = res.Results.Select(r => new
                {
                    r.Id,
                    r.Reason,
                    r.State,
                    Proxy = $"{r.Proxy}:{r.ProxyPort} ({r.ProxyCountryCode})",
                    ReplacedWith = $"{r.ReplacedWith}:{r.ReplacedWithPort}",
                    Created = r.CreatedAt == default ? "" : r.CreatedAt.ToString("g")
                }).ToList();

                Logger.Info("Webshare", $"Loaded {res.Results.Count} replacements for plan {plan.Id}.");
                lblStatus.Text = "Status: Replacements loaded";
            }
            catch (Exception ex)
            {
                Logger.Error("Webshare", "Error loading replacements: " + ex.Message);
                lblStatus.Text = "Status: Failed to load replacements";
            }
        }

        private void InstallStartupTask()
        {
            if (!StartupTaskHelper.IsRunningElevated())
            {
                var choice = MessageBox.Show(
                    "Installing the boot-time SYSTEM scheduled task requires administrator rights. Relaunch WebshareProxyUtil as administrator now?",
                    "Administrator Rights Required",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (choice == DialogResult.Yes)
                {
                    var launched = StartupTaskHelper.RelaunchElevated("--install-startup-task", out var relaunchDetails);
                    Logger.Info("StartupTask", relaunchDetails);
                    MessageBox.Show(relaunchDetails, launched ? "Elevation Requested" : "Elevation Failed", MessageBoxButtons.OK, launched ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                }

                return;
            }

            var ok = StartupTaskHelper.InstallBootTask(out var details);
            Logger.Info("StartupTask", details);
            UpdateStartupTaskStatusLabel();
            MessageBox.Show(details, ok ? "Startup Task Installed" : "Startup Task Install Failed", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }

        private void RemoveStartupTask()
        {
            if (!StartupTaskHelper.IsRunningElevated())
            {
                var choice = MessageBox.Show(
                    "Removing the boot-time SYSTEM scheduled task requires administrator rights. Relaunch WebshareProxyUtil as administrator now?",
                    "Administrator Rights Required",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (choice == DialogResult.Yes)
                {
                    var launched = StartupTaskHelper.RelaunchElevated("--uninstall-startup-task", out var relaunchDetails);
                    Logger.Info("StartupTask", relaunchDetails);
                    MessageBox.Show(relaunchDetails, launched ? "Elevation Requested" : "Elevation Failed", MessageBoxButtons.OK, launched ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                }

                return;
            }

            var ok = StartupTaskHelper.RemoveBootTask(out var details);
            Logger.Info("StartupTask", details);
            UpdateStartupTaskStatusLabel();
            MessageBox.Show(details, ok ? "Startup Task Removed" : "Startup Task Remove Failed", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }

        private void CheckStartupTask()
        {
            var installed = StartupTaskHelper.IsInstalled(out var details);
            Logger.Info("StartupTask", details);

            if (!installed && IsAccessDenied(details) && !StartupTaskHelper.IsRunningElevated())
            {
                lblStatus.Text = "Status: Boot startup task status requires admin";

                var choice = MessageBox.Show(
                    "Windows blocked access to the boot-time SYSTEM scheduled task status from this non-elevated session. Relaunch WebshareProxyUtil as administrator to check the startup task now?",
                    "Administrator Rights Required",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (choice == DialogResult.Yes)
                {
                    var launched = StartupTaskHelper.RelaunchElevated("--check-startup-task", out var relaunchDetails);
                    Logger.Info("StartupTask", relaunchDetails);
                    MessageBox.Show(relaunchDetails, launched ? "Elevation Requested" : "Elevation Failed", MessageBoxButtons.OK, launched ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                }

                return;
            }

            lblStatus.Text = installed ? "Status: Boot startup task is installed" : "Status: Boot startup task is not installed";
            MessageBox.Show(details, installed ? "Startup Task Installed" : "Startup Task Not Installed", MessageBoxButtons.OK, installed ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private void UpdateStartupTaskStatusLabel()
        {
            var installed = StartupTaskHelper.IsInstalled(out var details);

            if (installed)
            {
                lblStatus.Text = "Status: Boot startup task installed";
                return;
            }

            lblStatus.Text = IsAccessDenied(details) && !StartupTaskHelper.IsRunningElevated()
                ? "Status: Boot startup task status requires admin"
                : "Status: Boot startup task not installed";
        }

        private static bool IsAccessDenied(string? details)
        {
            if (string.IsNullOrWhiteSpace(details))
                return false;

            return details.IndexOf("Access is denied", StringComparison.OrdinalIgnoreCase) >= 0
                || details.IndexOf("ERROR: Access", StringComparison.OrdinalIgnoreCase) >= 0
                || details.IndexOf("permission", StringComparison.OrdinalIgnoreCase) >= 0
                || details.IndexOf("denied", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void InitializeSessionLogView()
        {
            try
            {
                txtLog.Clear();
                AppendLogLineToGui($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] [UI] Session log view started. This pane now shows current GUI/session activity only.");
                AppendLogLineToGui($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] [UI] Persistent file log: {Logger.LogFile}");
                AppendLogLineToGui($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] [UI] Use 'Load File Log' to view the full persistent log or 'Reset Log File' to clear it.");
            }
            catch
            {
            }
        }

        private void AppendLogLineToGui(string line)
        {
            if (txtLog == null || txtLog.IsDisposed)
                return;

            txtLog.AppendText(line + Environment.NewLine);
            txtLog.SelectionStart = txtLog.Text.Length;
            txtLog.ScrollToCaret();
        }

        private void ClearSessionLogView()
        {
            try
            {
                txtLog.Clear();
                AppendLogLineToGui($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] [UI] Session log view cleared. File log was not changed.");
            }
            catch
            {
            }
        }

        private void LoadExistingLogIntoGui()
        {
            try
            {
                txtLog.Clear();

                if (!File.Exists(Logger.LogFile))
                {
                    txtLog.Text = $"Log file has not been created yet: {Logger.LogFile}{Environment.NewLine}";
                    return;
                }

                txtLog.Text = File.ReadAllText(Logger.LogFile);
                txtLog.SelectionStart = txtLog.Text.Length;
                txtLog.ScrollToCaret();
                Logger.Info("UI", "Loaded persistent file log into GUI log view on request.");
            }
            catch (Exception ex)
            {
                txtLog.Text = $"Failed to load log file: {ex.Message}{Environment.NewLine}";
            }
        }

        private void ResetFileLog()
        {
            try
            {
                var confirm = MessageBox.Show(
                    "This will clear the persistent monitor.log file. The current session view will stay open and continue logging new activity. Continue?",
                    "Reset Log File",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirm != DialogResult.Yes)
                    return;

                Directory.CreateDirectory(Logger.LogDir);
                File.WriteAllText(
                    Logger.LogFile,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] [Logger] Log file reset from GUI. LogFile={Logger.LogFile}{Environment.NewLine}");

                txtLog.Clear();
                AppendLogLineToGui($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] [UI] Persistent log file reset: {Logger.LogFile}");
                AppendLogLineToGui($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] [UI] Session log view continues from this point.");
            }
            catch (Exception ex)
            {
                Logger.Error("UI", "Failed to reset log file: " + ex.Message);
                MessageBox.Show("Failed to reset log file: " + ex.Message, "Reset Log File", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenPath(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = $"\"{dir}\"",
                            UseShellExecute = true
                        });
                        return;
                    }
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to open path: " + ex.Message, "Open Path", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void SelectTab(string tab)
        {
            if (tab.Equals("logs", StringComparison.OrdinalIgnoreCase))
            {
                tabs.SelectedTab = tabLogs;
            }
            else if (tab.Equals("settings", StringComparison.OrdinalIgnoreCase))
            {
                tabs.SelectedTab = tabSettings;
            }
        }
    }
}
