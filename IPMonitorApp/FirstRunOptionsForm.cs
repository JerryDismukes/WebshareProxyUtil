using System;
using System.Drawing;
using System.Windows.Forms;

namespace IPMonitorApp
{
    public class FirstRunOptionsForm : Form
    {
        private readonly RadioButton _showManager;
        private readonly RadioButton _trayOnly;
        private readonly CheckBox _openSettings;
        private readonly Button _ok;
        private readonly Button _cancel;

        public bool StartMinimizedToTray => _trayOnly.Checked;
        public bool OpenSettingsAfterStart => _openSettings.Checked;

        public FirstRunOptionsForm(bool apiKeyMissing)
        {
            Text = "Webshare Proxy Utility - First Run";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            Width = 620;
            Height = 360;

            var title = new Label
            {
                Text = "Choose how Webshare Proxy Utility should open",
                Left = 18,
                Top = 18,
                Width = 560,
                Height = 28,
                Font = new Font(Font, FontStyle.Bold)
            };

            var intro = new Label
            {
                Text = "The utility can run quietly in the system tray, or it can show the manager window whenever you launch it interactively.",
                Left = 18,
                Top = 52,
                Width = 560,
                Height = 48
            };

            _showManager = new RadioButton
            {
                Text = "Show the full manager window when I run the EXE",
                Left = 34,
                Top = 112,
                Width = 520,
                Height = 28,
                Checked = true
            };

            _trayOnly = new RadioButton
            {
                Text = "Start in the system tray only",
                Left = 34,
                Top = 146,
                Width = 520,
                Height = 28
            };

            _openSettings = new CheckBox
            {
                Text = apiKeyMissing
                    ? "Open Settings now so I can enter the Webshare API key"
                    : "Open Settings after startup",
                Left = 34,
                Top = 192,
                Width = 520,
                Height = 28,
                Checked = apiKeyMissing
            };

            var note = new Label
            {
                Text = apiKeyMissing
                    ? "A Webshare API key is required before authorized IPs, proxies, and automatic public-IP updates will work."
                    : "You can change this later from Settings.",
                Left = 18,
                Top = 230,
                Width = 560,
                Height = 42,
                ForeColor = apiKeyMissing ? Color.DarkOrange : SystemColors.ControlText
            };

            _ok = new Button
            {
                Text = "Continue",
                Left = 390,
                Top = 286,
                Width = 96,
                Height = 32,
                DialogResult = DialogResult.OK
            };

            _cancel = new Button
            {
                Text = "Cancel",
                Left = 496,
                Top = 286,
                Width = 82,
                Height = 32,
                DialogResult = DialogResult.Cancel
            };

            Controls.AddRange(new Control[]
            {
                title,
                intro,
                _showManager,
                _trayOnly,
                _openSettings,
                note,
                _ok,
                _cancel
            });

            AcceptButton = _ok;
            CancelButton = _cancel;
        }
    }
}
