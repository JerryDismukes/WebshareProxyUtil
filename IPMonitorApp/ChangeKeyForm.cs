using System;
using System.Drawing;
using System.Windows.Forms;

namespace IPMonitorApp
{
    public class ChangeKeyForm : Form
    {
        private readonly TextBox txtKey;
        private readonly Button btnOk;
        private readonly Button btnCancel;

        public string ApiKey => txtKey.Text.Trim();

        public ChangeKeyForm()
            : this("Enter your Webshare API key. This is required before the utility can list proxies, manage authorized IPs, or auto-authorize your public IP.")
        {
        }

        public ChangeKeyForm(string prompt)
        {
            Text = "Webshare API Key Required";
            Width = 680;
            Height = 320;
            MinimumSize = new Size(680, 320);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            var lbl = new Label
            {
                Text = prompt,
                Left = 16,
                Top = 16,
                Width = 630,
                Height = 46,
                AutoSize = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblKey = new Label
            {
                Text = "API Key:",
                Left = 16,
                Top = 80,
                Width = 80,
                Height = 24,
                AutoSize = false
            };

            txtKey = new TextBox
            {
                Left = 100,
                Top = 76,
                Width = 546,
                Height = 28,
                UseSystemPasswordChar = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblNote = new Label
            {
                Text = "The key is stored in Windows Credential Manager for the interactive user. A machine-scoped encrypted DPAPI copy is also stored in the shared config so boot-time headless mode can run before user logon.",
                Left = 16,
                Top = 118,
                Width = 630,
                Height = 72,
                AutoSize = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            btnOk = new Button
            {
                Text = "Save",
                Left = 456,
                Top = 224,
                Width = 90,
                Height = 32,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            btnCancel = new Button
            {
                Text = "Cancel",
                Left = 556,
                Top = 224,
                Width = 90,
                Height = 32,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            btnOk.Click += BtnOk_Click;

            btnCancel.Click += (_, __) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            Controls.AddRange(new Control[]
            {
        lbl,
        lblKey,
        txtKey,
        lblNote,
        btnOk,
        btnCancel
            });
        }
        private void BtnOk_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtKey.Text))
            {
                MessageBox.Show(
                    "Enter a Webshare API key before saving.",
                    "API Key Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                txtKey.Focus();
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
