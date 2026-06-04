
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace IPMonitorApp
{
    public class SettingsForm : UserControl
    {
        private GroupBox gbConnection, gbLogging, gbMonitoring, gbCredentials;
        private CheckBox chkEnableDebug, chkWebshareEnabled;
        private NumericUpDown numLogSizeKB, numLogCopies, numPingInterval, numApiInterval, numMaxFailures;
        private Label lblApiKey;
        private Button btnChangeKey, btnSave;
        private ToolTip tooltip = new ToolTip();
        private AppConfig _config;

        public SettingsForm()
        {
            InitializeComponent();
            // load config and populate UI
            _config = ConfigLoader.Load();
            LoadSettingsToUi();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.Padding = new Padding(10);

            gbConnection = new GroupBox { Text = "Connection", Dock = DockStyle.Top, Height = 70 };
            gbLogging = new GroupBox { Text = "Logging", Dock = DockStyle.Top, Height = 110 };
            gbMonitoring = new GroupBox { Text = "Monitoring", Dock = DockStyle.Top, Height = 140 };
            gbCredentials = new GroupBox { Text = "Credentials", Dock = DockStyle.Top, Height = 80 };

            // Connection group (placeholder for future items)
            chkWebshareEnabled = new CheckBox { Text = "Enable Webshare IP validation", Left = 10, Top = 22, AutoSize = true };
            tooltip.SetToolTip(chkWebshareEnabled, "Enable periodic public IP checks via Webshare.");
            gbConnection.Controls.Add(chkWebshareEnabled);

            // Logging group
            chkEnableDebug = new CheckBox { Text = "Enable Debug Logging", Left = 10, Top = 22, AutoSize = true };
            tooltip.SetToolTip(chkEnableDebug, "When enabled, all ping/API calls will be logged at DEBUG level.");
            var lblLogSize = new Label { Text = "Max Log Size (KB):", Left = 10, Top = 52, AutoSize = true };
            numLogSizeKB = new NumericUpDown { Left = 140, Top = 48, Width = 120, Minimum = 1, Maximum = 1048576, Value = 500 };
            tooltip.SetToolTip(numLogSizeKB, "When log reaches this size it will rotate.");

            var lblLogCopies = new Label { Text = "Log Files to Keep:", Left = 10, Top = 80, AutoSize = true };
            numLogCopies = new NumericUpDown { Left = 140, Top = 76, Width = 80, Minimum = 1, Maximum = 50, Value = 5 };
            tooltip.SetToolTip(numLogCopies, "Number of rotated log files to retain.");

            gbLogging.Controls.Add(chkEnableDebug);
            gbLogging.Controls.Add(lblLogSize);
            gbLogging.Controls.Add(numLogSizeKB);
            gbLogging.Controls.Add(lblLogCopies);
            gbLogging.Controls.Add(numLogCopies);

            // Monitoring group
            var lblPingInterval = new Label { Text = "Ping Interval (sec):", Left = 10, Top = 22, AutoSize = true };
            numPingInterval = new NumericUpDown { Left = 160, Top = 18, Width = 80, Minimum = 1, Maximum = 3600, Value = 5 };
            tooltip.SetToolTip(numPingInterval, "How often (seconds) to ping targets.");

            var lblApiInterval = new Label { Text = "IP Check Interval (sec):", Left = 10, Top = 52, AutoSize = true };
            numApiInterval = new NumericUpDown { Left = 160, Top = 48, Width = 80, Minimum = 30, Maximum = 86400, Value = 60 };
            tooltip.SetToolTip(numApiInterval, "How often (seconds) to call webshare to verify public IP.");

            var lblMaxFailures = new Label { Text = "Max Failures:", Left = 10, Top = 82, AutoSize = true };
            numMaxFailures = new NumericUpDown { Left = 160, Top = 78, Width = 80, Minimum = 1, Maximum = 100, Value = 3 };
            tooltip.SetToolTip(numMaxFailures, "Number of consecutive failures before marking status as Error.");

            gbMonitoring.Controls.Add(lblPingInterval);
            gbMonitoring.Controls.Add(numPingInterval);
            gbMonitoring.Controls.Add(lblApiInterval);
            gbMonitoring.Controls.Add(numApiInterval);
            gbMonitoring.Controls.Add(lblMaxFailures);
            gbMonitoring.Controls.Add(numMaxFailures);

            // Credentials group
            lblApiKey = new Label { Text = "API Key: Stored securely in Credential Vault", Left = 10, Top = 24, AutoSize = true };
            btnChangeKey = new Button { Text = "Change Key...", Left = 380, Top = 18, Width = 100 };
            btnChangeKey.Click += BtnChangeKey_Click;
            tooltip.SetToolTip(btnChangeKey, "Replace the stored API key in the credential vault.");

            gbCredentials.Controls.Add(lblApiKey);
            gbCredentials.Controls.Add(btnChangeKey);

            // Save button
            btnSave = new Button { Text = "Save Settings", Width = 120, Height = 30, Left = 10, Top = gbCredentials.Bottom + 10 };
            btnSave.Click += BtnSave_Click;

            // Add groups to control
            this.Controls.AddRange(new Control[] { btnSave, gbCredentials, gbMonitoring, gbLogging, gbConnection });
        }

        private void LoadSettingsToUi()
        {
            try
            {
                var s = _config.settings;
                // tolerant getters
                chkEnableDebug.Checked = GetBool(s, "debug", false);
                numLogSizeKB.Value = GetInt(s, "maxLogSizeKB", 500);
                numLogCopies.Value = GetInt(s, "maxLogFiles", 5);
                numPingInterval.Value = GetInt(s, "pingIntervalSeconds", 5);
                numApiInterval.Value = GetInt(s, "ipCheckIntervalSeconds", 60);
                numMaxFailures.Value = GetInt(s, "maxFailures", 3);
                chkWebshareEnabled.Checked = GetBool(_config.webshare, "Enabled", true);
                // API key label remains static; can optionally display masked info
                var stored = CredentialStore.GetCredential(GetString(_config.webshare, "ApiKeyCredentialTarget") ?? "");
                if (!string.IsNullOrEmpty(stored)) lblApiKey.Text = "API Key: Stored in Credential Vault";
                else lblApiKey.Text = "API Key: (not stored)";
            }
            catch (Exception ex)
            {
                lblApiKey.Text = "Failed to load settings: " + ex.Message;
            }
        }

        private void BtnChangeKey_Click(object sender, EventArgs e)
        {
            using (var dlg = new ChangeKeyForm())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    // save into credential vault under configured target (or default)
                    var target = GetString(_config.webshare, "ApiKeyCredentialTarget") ?? "IPMonitorApp:WebshareAPIKey";
                    CredentialStore.SaveCredential(target, dlg.ApiKey);
                    lblApiKey.Text = "API Key: Stored in Credential Vault";
                }
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            // Validation
            if (numLogSizeKB.Value < 1 || numLogCopies.Value < 1 || numPingInterval.Value < 1 || numApiInterval.Value < 1 || numMaxFailures.Value < 1)
            {
                MessageBox.Show("Please correct invalid values.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // write values back to config
            SetBool(_config.settings, "debug", chkEnableDebug.Checked);
            SetInt(_config.settings, "maxLogSizeKB", (int)numLogSizeKB.Value);
            SetInt(_config.settings, "maxLogFiles", (int)numLogCopies.Value);
            SetInt(_config.settings, "pingIntervalSeconds", (int)numPingInterval.Value);
            SetInt(_config.settings, "ipCheckIntervalSeconds", (int)numApiInterval.Value);
            SetInt(_config.settings, "maxFailures", (int)numMaxFailures.Value);
            SetBool(_config.webshare, "Enabled", chkWebshareEnabled.Checked);

            ConfigLoader.Save(_config);

            // apply immediately to logger and ping loop
            try { IPMonitorCore.Logger.Setup(GetInt(_config.settings, "maxLogSizeKB", 500), GetInt(_config.settings, "maxLogFiles", 5), GetBool(_config.settings, "debug", false)); } catch { }
            try { IPMonitorApp.PingLoop.Stop(); } catch { }
            try { IPMonitorApp.PingLoop.Start(_config); } catch { }

            MessageBox.Show("Settings saved successfully.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        #region reflection tolerant helpers (same as earlier)
        private int GetInt(object obj, string propName, int fallback)
        {
            try
            {
                var t = obj.GetType();
                var p = t.GetProperty(propName) ?? t.GetProperty(char.ToUpperInvariant(propName[0]) + propName.Substring(1)) ?? t.GetProperty(propName.ToLowerInvariant());
                if (p == null) return fallback;
                var v = p.GetValue(obj);
                if (v == null) return fallback;
                return Convert.ToInt32(v);
            }
            catch { return fallback; }
        }
        private void SetInt(object obj, string propName, int value)
        {
            try
            {
                var t = obj.GetType();
                var p = t.GetProperty(propName) ?? t.GetProperty(char.ToUpperInvariant(propName[0]) + propName.Substring(1)) ?? t.GetProperty(propName.ToLowerInvariant());
                if (p == null) return;
                p.SetValue(obj, Convert.ChangeType(value, p.PropertyType));
            }
            catch { }
        }
        private bool GetBool(object obj, string propName, bool fallback)
        {
            try
            {
                var t = obj.GetType();
                var p = t.GetProperty(propName) ?? t.GetProperty(char.ToUpperInvariant(propName[0]) + propName.Substring(1)) ?? t.GetProperty(propName.ToLowerInvariant());
                if (p == null) return fallback;
                var v = p.GetValue(obj);
                if (v == null) return fallback;
                return Convert.ToBoolean(v);
            }
            catch { return fallback; }
        }
        private void SetBool(object obj, string propName, bool value)
        {
            try
            {
                var t = obj.GetType();
                var p = t.GetProperty(propName) ?? t.GetProperty(char.ToUpperInvariant(propName[0]) + propName.Substring(1)) ?? t.GetProperty(propName.ToLowerInvariant());
                if (p == null) return;
                p.SetValue(obj, Convert.ChangeType(value, p.PropertyType));
            }
            catch { }
        }
        private string? GetString(object obj, string propName)
        {
            try
            {
                var t = obj.GetType();
                var p = t.GetProperty(propName) ?? t.GetProperty(char.ToUpperInvariant(propName[0]) + propName.Substring(1)) ?? t.GetProperty(propName.ToLowerInvariant());
                if (p == null) return null;
                return p.GetValue(obj)?.ToString();
            }
            catch { return null; }
        }
        #endregion
    }
}
