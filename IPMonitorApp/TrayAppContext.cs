using System;
using System.Drawing;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using IPMonitorCore;
using System.Linq;
using System.Runtime.InteropServices;

namespace IPMonitorApp
{
    public class TrayAppContext : ApplicationContext
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private readonly NotifyIcon _tray;
        private readonly IPMonitorRR _monitor;
        private readonly AppConfig _cfg;
        private readonly CancellationTokenSource _periodicToken = new();
        private readonly TimeSpan _periodicLogInterval;
        private bool _isExiting = false;
        private WebshareManagerForm? _managerForm;
        private ContextMenuStrip? _trayMenu;
        private Form? _trayMenuAnchorForm;

        public TrayAppContext()
            : base(CreateMessagePumpForm())
        {
            _cfg = ConfigLoader.Load();
            Logger.Setup(_cfg.settings.maxLogSizeKB, _cfg.settings.maxLogFiles, _cfg.settings.debug);
            Logger.DebugEnabled = _cfg.settings.debug;

            _periodicLogInterval = TimeSpan.FromMinutes(Math.Max(1, _cfg.settings.heartbeatIntervalMinutes));

            Logger.Info("Startup", "=======================");
            Logger.Info("Startup", "WebshareProxyUtil interactive tray starting up...");
            Logger.Info("Startup", $"Debug mode: {Logger.DebugEnabled}");

            // --- Webshare startup IP check (log at Info so visible even if debug=false) ---
            if (_cfg.webshare?.Enabled == true)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var apiKey = ConfigUtils.GetApiKey();
                        if (string.IsNullOrWhiteSpace(apiKey))
                        {
                            Logger.Warn("Startup", "Webshare enabled but no API key found.");
                        }
                        else
                        {
                            Logger.Info("Startup", "Performing initial IP check and authorization...");
                            var ip = await WebshareUtil.WhatsMyIpAsync(apiKey);
                            var paged = await WebshareUtil.ListAuthorizedIpsAsync(apiKey);
                            Logger.Info("Startup", $"Current public IP: {ip ?? "(unknown)"}");
                            Logger.Info("Startup", $"Authorized IP count: {paged.Results.Count}");
                            if (_cfg.settings.debug)
                                Logger.Debug("Startup", $"Authorized IPs: {string.Join(", ", paged.Results.Select(a => a.IpAddress))}");

                            await WebshareUtil.CheckAndAddIpIfChangedAsync(apiKey);
                            Logger.Info("Startup", "Initial IP check finished.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Startup", $"Startup IP check failed: {ex.Message}");
                    }
                });
            }
            else
            {
                Logger.Info("Startup", "Webshare integration disabled.");
            }

            // --- Start ping loop (this must run on startup) ---
            try
            {
                Logger.Info("Startup", "Starting ping loop...");
                PingLoop.Start(_cfg);
                Logger.Info("Startup", "PingLoop started.");
            }
            catch (Exception ex)
            {
                Logger.Error("Startup", $"Failed to start PingLoop: {ex.Message}");
            }

            // --- Initialize monitor and tray icon ---
            _monitor = new IPMonitorRR(_cfg);
            _monitor.OnStatusChanged += UpdateIcon;

            _tray = new NotifyIcon
            {
                Icon = LoadIcon("icon_paused"),
                Visible = true,
                Text = "WebshareProxyUtil: Paused"
            };

            // Subscribe for network changes (will call WebshareUtil.CheckAndAddIpIfChangedAsync)
            NetworkChange.NetworkAddressChanged += async (s, e) =>
            {
                try
                {
                    var cfg = ConfigLoader.Load();
                    if (cfg.webshare?.Enabled != true) return;

                    var apiKey = ConfigUtils.GetApiKey();
                    Logger.Info("NetworkChange", "Network address changed � validating public IP...");
                    await WebshareUtil.CheckAndAddIpIfChangedAsync(apiKey);
                }
                catch (Exception ex)
                {
                    Logger.Error("NetworkChange", $"IP change handling failed: {ex.Message}");
                }
            };

            // Periodic heartbeat / validation loop (runs in background)
            _ = Task.Run(() => PeriodicStatusLoop(_periodicToken.Token));

            // --- Context menu with robust Exit ---
            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("Start Monitoring", null, async (_, __) => await StartMonitoringAsync("tray"));
            _trayMenu.Items.Add("Stop Monitoring", null, async (_, __) => await StopMonitoringAsync("tray"));
            _trayMenu.Items.Add("View Log", null, (_, __) => ShowManagerTab("logs"));
            _trayMenu.Items.Add("Open Manager", null, (_, __) => ShowManagerTab("settings"));
            _trayMenu.Items.Add("Settings", null, (_, __) => ShowManagerTab("settings"));
            _trayMenu.Items.Add(new ToolStripSeparator());

            // Robust Exit: disable menu, stop services, dispose tray, then Application.Exit()
            _trayMenu.Items.Add("Exit", null, async (_, __) => await ExitApplicationAsync("tray"));
            _trayMenu.Closed += (_, __) => HideTrayMenuAnchor();

            // Do not assign NotifyIcon.ContextMenuStrip. WinForms can anchor the automatic tray
            // menu using the active window's monitor in multi-monitor layouts. Showing it
            // manually from Cursor.Position anchors it to the actual tray click location.
            _tray.MouseUp += (_, e) =>
            {
                if (e.Button == MouseButtons.Right)
                    ShowTrayMenuAtCursor();
            };

            _tray.MouseDoubleClick += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    ShowManagerTab("settings");
            };

            HandleFirstRunAndStartupDisplay();

            // Start monitoring automatically on launch
            try
            {
                _monitor.Start();
                Logger.Info("Startup", "Monitoring service started automatically on startup.");
            }
            catch (Exception ex)
            {
                Logger.Error("Startup", $"Failed to auto-start monitor: {ex.Message}");
            }
        }



        private async Task StartMonitoringAsync(string source)
        {
            try
            {
                PingLoop.Start(_cfg);
                _monitor.Start();
                UpdateIcon(MonitorStatus.Ok);
                Logger.Info("UI", $"Monitoring started from {source}.");
            }
            catch (Exception ex)
            {
                Logger.Error("UI", $"Failed to start monitoring from {source}: {ex.Message}");
                UpdateIcon(MonitorStatus.Warning);
                throw;
            }
        }

        private async Task StopMonitoringAsync(string source)
        {
            try
            {
                try { PingLoop.Stop(); } catch (Exception ex) { Logger.Warn("UI", $"PingLoop stop warning: {ex.Message}"); }
                await _monitor.StopAsync();
                UpdateIcon(MonitorStatus.Paused);
                Logger.Info("UI", $"Monitoring stopped from {source}. Tray icon set to paused.");
            }
            catch (Exception ex)
            {
                Logger.Error("UI", $"Failed to stop monitoring from {source}: {ex.Message}");
                UpdateIcon(MonitorStatus.Warning);
                throw;
            }
        }

        private async Task ExitApplicationAsync(string source)
        {
            try
            {
                Logger.Info("App", $"Exit requested from {source}.");

                try { if (_trayMenu != null) _trayMenu.Enabled = false; } catch { }

                _periodicToken.Cancel();

                try { await _monitor.StopAsync(); } catch (Exception ex) { Logger.Warn("Exit", $"Monitor stop error: {ex.Message}"); }
                try { PingLoop.Stop(); } catch (Exception ex) { Logger.Warn("Exit", $"PingLoop stop error: {ex.Message}"); }

                _isExiting = true;

                try { if (_trayMenu != null) { _trayMenu.Close(); _trayMenu.Dispose(); _trayMenu = null; } } catch { }
                try { _tray.Visible = false; _tray.Dispose(); } catch { }
                try { _managerForm?.Hide(); } catch { }
                try { _managerForm?.Close(); } catch { }
                try { _trayMenuAnchorForm?.Close(); } catch { }
                try { _trayMenuAnchorForm?.Dispose(); } catch { }
                try { MainForm?.Hide(); } catch { }
                try { MainForm?.Close(); } catch { }

                await Task.Delay(150);

                Logger.Info("App", "Shutdown complete; exiting application.");
            }
            catch (Exception ex)
            {
                Logger.Error("App", $"Unexpected error while exiting from {source}: {ex.Message}");
                throw;
            }
            finally
            {
                _isExiting = true;
                try { ExitThread(); } catch { }
                try { Application.ExitThread(); } catch { }
                try { Application.Exit(); } catch { }
                Environment.Exit(0);
            }
        }

        private void HandleFirstRunAndStartupDisplay()
        {
            try
            {
                var apiKeyMissing = _cfg.webshare?.Enabled == true && string.IsNullOrWhiteSpace(ConfigUtils.GetApiKey());
                var openSettings = false;

                if (!_cfg.settings.firstRunComplete)
                {
                    Logger.Info("Startup", "First interactive run detected. Showing startup behavior prompt.");

                    using var firstRun = new FirstRunOptionsForm(apiKeyMissing);
                    var result = firstRun.ShowDialog();

                    var cfg = ConfigLoader.Load();
                    cfg.settings.firstRunComplete = true;

                    if (result == DialogResult.OK)
                    {
                        cfg.settings.startMinimizedToTray = firstRun.StartMinimizedToTray;
                        openSettings = firstRun.OpenSettingsAfterStart || apiKeyMissing || !firstRun.StartMinimizedToTray;
                    }
                    else
                    {
                        cfg.settings.startMinimizedToTray = false;
                        openSettings = true;
                    }

                    ConfigLoader.Save(cfg);
                    Logger.Info("Startup", $"First-run options saved. StartMinimizedToTray={cfg.settings.startMinimizedToTray}; OpenSettings={openSettings}");
                }
                else
                {
                    openSettings = !_cfg.settings.startMinimizedToTray || apiKeyMissing;
                    Logger.Info("Startup", $"Saved startup behavior loaded. StartMinimizedToTray={_cfg.settings.startMinimizedToTray}; OpenSettings={openSettings}");
                }

                if (openSettings)
                    ShowManagerTab("settings");

                if (apiKeyMissing)
                    PromptForMissingApiKeyIfNeeded();
            }
            catch (Exception ex)
            {
                Logger.Error("Startup", "Failed while processing first-run/startup behavior: " + ex.Message);
                try { ShowManagerTab("settings"); } catch { }
            }
        }

        private void PromptForMissingApiKeyIfNeeded()
        {
            try
            {
                if (_cfg.webshare?.Enabled != true)
                    return;

                if (!string.IsNullOrWhiteSpace(ConfigUtils.GetApiKey()))
                    return;

                Logger.Warn("Startup", "Webshare is enabled but no API key is configured. Opening first-run setup prompt.");

                try
                {
                    _tray.BalloonTipTitle = "Webshare API key required";
                    _tray.BalloonTipText = "Open settings and enter your Webshare API key before the utility can manage authorized IPs.";
                    _tray.BalloonTipIcon = ToolTipIcon.Warning;
                    _tray.ShowBalloonTip(10000);
                }
                catch
                {
                }

                try
                {
                    MainForm?.BeginInvoke(new Action(() => ShowManagerTab("settings")));
                }
                catch
                {
                    ShowManagerTab("settings");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Startup", "Failed while checking first-run API key state: " + ex.Message);
            }
        }

        private static Form CreateMessagePumpForm()
        {
            var form = new Form
            {
                ShowInTaskbar = false,
                WindowState = FormWindowState.Minimized,
                Opacity = 0,
                Size = new Size(0, 0),
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-32000, -32000),
                Text = "WebshareProxyUtil Message Pump"
            };

            form.Load += (_, __) =>
            {
                try
                {
                    form.Hide();
                }
                catch
                {
                }
            };

            return form;
        }

        private async Task PeriodicStatusLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_cfg.webshare?.Enabled == true)
                    {
                        var apiKey = ConfigUtils.GetApiKey();
                        if (!string.IsNullOrWhiteSpace(apiKey))
                        {
                            Logger.Info("Periodic", "Running scheduled IP validation...");
                            await WebshareUtil.CheckAndAddIpIfChangedAsync(apiKey);
                        }
                    }

                    // Always log a simple heartbeat at Info level so logs show activity even with debug off
                    Logger.Info("Periodic", "Heartbeat: monitoring active.");
                }
                catch (Exception ex)
                {
                    Logger.Error("Periodic", $"Periodic loop error: {ex.Message}");
                }

                try { await Task.Delay(_periodicLogInterval, token); } catch (TaskCanceledException) { break; }
            }
        }

        private void ShowTrayMenuAtCursor()
        {
            if (_isExiting || _trayMenu == null || _trayMenu.IsDisposed)
                return;

            try
            {
                var cursor = Cursor.Position;
                var screen = Screen.FromPoint(cursor);
                var workingArea = screen.WorkingArea;

                EnsureTrayMenuAnchor();

                if (_trayMenuAnchorForm == null || _trayMenuAnchorForm.IsDisposed)
                    return;

                var anchorX = Math.Min(Math.Max(cursor.X, workingArea.Left), Math.Max(workingArea.Left, workingArea.Right - 1));
                var anchorY = Math.Min(Math.Max(cursor.Y, workingArea.Top), Math.Max(workingArea.Top, workingArea.Bottom - 1));

                _trayMenuAnchorForm.Location = new Point(anchorX, anchorY);
                _trayMenuAnchorForm.Size = new Size(1, 1);

                if (!_trayMenuAnchorForm.Visible)
                    _trayMenuAnchorForm.Show();

                _trayMenuAnchorForm.BringToFront();

                try
                {
                    SetForegroundWindow(_trayMenuAnchorForm.Handle);
                }
                catch
                {
                }

                var horizontalMiddle = workingArea.Left + (workingArea.Width / 2);
                var verticalMiddle = workingArea.Top + (workingArea.Height / 2);

                var direction = ToolStripDropDownDirection.Default;

                if (cursor.Y >= verticalMiddle && cursor.X >= horizontalMiddle)
                    direction = ToolStripDropDownDirection.AboveLeft;
                else if (cursor.Y >= verticalMiddle && cursor.X < horizontalMiddle)
                    direction = ToolStripDropDownDirection.AboveRight;
                else if (cursor.Y < verticalMiddle && cursor.X >= horizontalMiddle)
                    direction = ToolStripDropDownDirection.BelowLeft;
                else
                    direction = ToolStripDropDownDirection.BelowRight;

                _trayMenu.Show(_trayMenuAnchorForm, Point.Empty, direction);
            }
            catch (Exception ex)
            {
                Logger.Error("TrayAppContext", $"Failed to show tray context menu: {ex.Message}");
            }
        }

        private void EnsureTrayMenuAnchor()
        {
            if (_trayMenuAnchorForm != null && !_trayMenuAnchorForm.IsDisposed)
                return;

            _trayMenuAnchorForm = new Form
            {
                ShowInTaskbar = false,
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Size = new Size(1, 1),
                Opacity = 0,
                TopMost = true,
                Text = "WebshareProxyUtil Tray Menu Anchor"
            };
        }

        private void HideTrayMenuAnchor()
        {
            try
            {
                if (_trayMenuAnchorForm != null && !_trayMenuAnchorForm.IsDisposed)
                    _trayMenuAnchorForm.Hide();
            }
            catch
            {
            }
        }

        private void ShowManagerTab(string tab)
        {
            if (_isExiting)
                return;

            try
            {
                if (_managerForm == null || _managerForm.IsDisposed)
                {
                    _managerForm = new WebshareManagerForm(
                        () => StartMonitoringAsync("manager window"),
                        () => StopMonitoringAsync("manager window"),
                        () => ExitApplicationAsync("manager window"),
                        UpdateIcon);
                    _managerForm.FormClosed += (_, __) => _managerForm = null;
                    _managerForm.Show();
                }
                else
                {
                    if (!_managerForm.Visible)
                        _managerForm.Show();

                    if (_managerForm.WindowState == FormWindowState.Minimized)
                        _managerForm.WindowState = FormWindowState.Normal;

                    _managerForm.Activate();
                    _managerForm.BringToFront();
                }

                _managerForm.SelectTab(tab);
            }
            catch (Exception ex)
            {
                Logger.Error("UI", "Failed to show manager window: " + ex.Message);
            }
        }

        private Icon LoadIcon(string name)
        {
            var assembly = typeof(TrayAppContext).Assembly;
            var resourceName = $"IPMonitorApp.Icons.{name}.ico";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            return new Icon(stream);
        }

        private void UpdateIcon(MonitorStatus status)
        {
            try
            {
                _tray.Icon = status switch
                {
                    MonitorStatus.Ok => LoadIcon("icon_ok"),
                    MonitorStatus.Warning => LoadIcon("icon_warn"),
                    MonitorStatus.Error => LoadIcon("icon_err"),
                    MonitorStatus.Paused => LoadIcon("icon_paused"),
                    _ => LoadIcon("icon_ok")
                };
                _tray.Text = $"WebshareProxyUtil: {status}";
            }
            catch (Exception ex)
            {
                Logger.Error("TrayAppContext", $"Failed to update tray icon: {ex.Message}");
            }
        }

        protected override void OnMainFormClosed(object? sender, EventArgs e)
        {
            if (_isExiting)
            {
                base.OnMainFormClosed(sender, e);
                return;
            }

            Logger.Warn("TrayAppContext", "Hidden message-pump form closed unexpectedly; keeping tray context alive.");
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                _periodicToken.Cancel();
                PingLoop.Stop();
                try { _trayMenu?.Dispose(); } catch { }
                try { _trayMenuAnchorForm?.Dispose(); } catch { }
                try { _tray.Dispose(); } catch { }
            }
            catch { }
            base.Dispose(disposing);
        }
    }
}
