namespace Simultria.RustDeskCompanion;

internal sealed class PublicSignInForm : BrandedForm
{
    private readonly string rustDeskPath;
    private readonly Label statusLabel = new();
    private readonly System.Windows.Forms.Timer loginTimer = new() { Interval = 750 };

    public PublicSignInForm(string rustDeskPath)
    {
        this.rustDeskPath = rustDeskPath;

        Text = "One-time RustDesk public sign-in";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(520, 260);
        Font = new Font("Segoe UI", 10F);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 5,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label
        {
            Text = "Finish signing in inside RustDesk",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 10),
        });

        layout.Controls.Add(new Label
        {
            Text = "In RustDesk, open Settings → Account → Login and choose Google, GitHub, or Microsoft. Complete the browser sign-in yourself. RustDeskHop will notice when it succeeds and continue your saved connection automatically.",
            AutoSize = true,
            MaximumSize = new Size(475, 0),
            Margin = new Padding(0, 0, 0, 14),
        });

        statusLabel.Text = "Waiting for RustDesk sign-in…";
        statusLabel.AutoSize = true;
        statusLabel.ForeColor = Color.DimGray;
        layout.Controls.Add(statusLabel);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
        };

        var openRustDesk = new Button { Text = "Open RustDesk", AutoSize = true, Padding = new Padding(8, 3, 8, 3) };
        openRustDesk.Click += (_, _) => OpenRustDesk();
        buttons.Controls.Add(openRustDesk);

        var checkAgain = new Button { Text = "I've signed in — check again", AutoSize = true, Padding = new Padding(8, 3, 8, 3) };
        checkAgain.Click += (_, _) => CheckLogin();
        buttons.Controls.Add(checkAgain);

        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, Padding = new Padding(8, 3, 8, 3) };
        buttons.Controls.Add(cancel);

        layout.Controls.Add(buttons, 0, 4);
        Controls.Add(layout);

        CancelButton = cancel;
        loginTimer.Tick += (_, _) => CheckLogin();
        Shown += (_, _) =>
        {
            OpenRustDesk();
            loginTimer.Start();
        };
        FormClosed += (_, _) => loginTimer.Stop();
    }

    private void OpenRustDesk()
    {
        try
        {
            RustDeskLauncher.Open(rustDeskPath);
            statusLabel.Text = "RustDesk is open. Waiting for sign-in…";
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"RustDesk could not be opened: {ex.Message}";
        }
    }

    private void CheckLogin()
    {
        if (!RustDeskAccountState.HasLoginToken())
        {
            statusLabel.Text = "Waiting for RustDesk sign-in…";
            return;
        }

        loginTimer.Stop();
        statusLabel.Text = "Sign-in detected. Connecting…";
        DialogResult = DialogResult.OK;
        Close();
    }
}
