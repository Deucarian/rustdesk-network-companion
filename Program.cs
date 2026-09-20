using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Simultria.RustDeskCompanion;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (RustDeskPublicProfileSetup.TryHandleCommand(args, out var exitCode))
        {
            Environment.ExitCode = exitCode;
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed class AppSettings
{
    public List<ServerProfile> Profiles { get; set; } = [];
    public List<TargetDefinition> Targets { get; set; } = [];
}

internal sealed class ServerProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New profile";
    public string ServerAddress { get; set; } = "";
    public string PublicKey { get; set; } = "";
    public bool RequiresPrivateNetwork { get; set; }
    public string ProbeHost { get; set; } = "";
    public int ProbePort { get; set; } = 21116;

    [JsonIgnore]
    public bool IsPublic => string.Equals(ServerAddress.Trim(), "public", StringComparison.OrdinalIgnoreCase);

    public override string ToString() => Name;
}

internal sealed class TargetDefinition
{
    public string Name { get; set; } = "New client";
    public string RustDeskId { get; set; } = "";
    public string ProfileId { get; set; } = "";
}

internal static class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SimultriaRustDeskCompanion");

    public static string FilePath => Path.Combine(DirectoryPath, "settings.json");

    public static AppSettings Load()
    {
        var saved = TryRead(FilePath);
        if (saved is not null && saved.Profiles.Count > 0)
        {
            return saved;
        }

        // A developer can keep machine-specific profiles beside the executable in an
        // ignored settings.local.json file without putting them in the public repository.
        var localSeed = TryRead(Path.Combine(AppContext.BaseDirectory, "settings.local.json"));
        if (localSeed is not null && localSeed.Profiles.Count > 0)
        {
            return localSeed;
        }

        return CreateDefaults();
    }

    private static AppSettings? TryRead(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions);
            }
        }
        catch
        {
            // The UI will still start with safe defaults if the settings file is damaged.
        }

        return null;
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(DirectoryPath);
        var temporaryPath = FilePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temporaryPath, FilePath, true);
    }

    private static AppSettings CreateDefaults()
    {
        var publicProfile = new ServerProfile
        {
            Id = "public",
            Name = "RustDesk Public",
            ServerAddress = "public",
        };

        var privateProfile = new ServerProfile
        {
            Id = "private-example",
            Name = "Private RustDesk",
            ServerAddress = "private-server.example:21116",
            PublicKey = "",
            RequiresPrivateNetwork = true,
            ProbeHost = "private-server.example",
            ProbePort = 21116,
        };

        return new AppSettings
        {
            Profiles = [publicProfile, privateProfile],
            Targets = [],
        };
    }
}

internal static class RustDeskLocator
{
    public static string? Find()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "RustDesk", "rustdesk.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "RustDesk", "rustdesk.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RustDesk", "rustdesk.exe"),
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}

internal static class RustDeskConfigReader
{
    public static string? ReadConfiguredServer()
    {
        return ReadConfiguredServer(RustDeskPaths.UserConfigPath);
    }

    internal static string? ReadConfiguredServer(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var text = File.ReadAllText(path);
            // The top-level value is the effective server in current RustDesk builds.
            // Prefer it over a stale custom-rendezvous-server option when both exist.
            var rendezvous = Regex.Match(text, "^rendezvous_server\\s*=\\s*['\"](?<value>[^'\"]+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            if (rendezvous.Success && !string.IsNullOrWhiteSpace(rendezvous.Groups["value"].Value))
            {
                return rendezvous.Groups["value"].Value.Trim();
            }

            var custom = Regex.Match(text, "custom-rendezvous-server\\s*=\\s*['\"](?<value>[^'\"]+)", RegexOptions.IgnoreCase);
            return custom.Success ? custom.Groups["value"].Value.Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    public static ServerProfile? DetectDefaultProfile(AppSettings settings)
    {
        var configured = ReadConfiguredServer();
        if (string.IsNullOrWhiteSpace(configured))
        {
            return settings.Profiles.FirstOrDefault(profile => profile.IsPublic)
                ?? settings.Profiles.FirstOrDefault();
        }

        if (string.Equals(configured, "public", StringComparison.OrdinalIgnoreCase)
            || configured.StartsWith("rs-", StringComparison.OrdinalIgnoreCase))
        {
            return settings.Profiles.FirstOrDefault(p => p.IsPublic);
        }

        var configuredHost = NormalizeHost(configured);
        return settings.Profiles.FirstOrDefault(p =>
            string.Equals(p.ServerAddress, configured, StringComparison.OrdinalIgnoreCase)
            || string.Equals(NormalizeHost(p.ServerAddress), configuredHost, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeHost(string address)
    {
        var value = address.Trim().TrimEnd('/');
        var firstColon = value.IndexOf(':');
        var lastColon = value.LastIndexOf(':');
        return firstColon >= 0 && firstColon == lastColon
            ? value[..firstColon]
            : value;
    }
}

internal static class NetworkProbe
{
    public static async Task<bool> CanReachAsync(ServerProfile profile, CancellationToken cancellationToken = default)
    {
        if (!profile.RequiresPrivateNetwork)
        {
            return true;
        }

        var host = string.IsNullOrWhiteSpace(profile.ProbeHost)
            ? profile.ServerAddress.Split(':', 2)[0]
            : profile.ProbeHost;
        var port = profile.ProbePort > 0 ? profile.ProbePort : 21116;

        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            await client.ConnectAsync(host, port, timeout.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

internal static class RustDeskSessions
{
    public static bool HasVisibleWindows()
    {
        return Process.GetProcessesByName("rustdesk")
            .Any(p =>
            {
                try { return p.MainWindowHandle != IntPtr.Zero; }
                catch { return false; }
            });
    }

    public static void CloseVisibleWindows()
    {
        foreach (var process in Process.GetProcessesByName("rustdesk"))
        {
            try
            {
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    process.CloseMainWindow();
                    process.WaitForExit(1500);
                }
            }
            catch
            {
                // Never force-kill RustDesk's service. It may be providing unattended access.
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}

internal sealed partial class MainForm : BrandedForm
{

    private void RefreshTargets()
    {
        RefreshNetworkStatus();

        targetsGrid.DataSource = settings.Targets
            .Select(target => new ComputerRow(target.Name, target.RustDeskId,
                settings.Profiles.FirstOrDefault(p => p.Id == target.ProfileId)?.Name ?? "Missing profile",
                settings.Profiles.FirstOrDefault(p => p.Id == target.ProfileId)?.IsPublic ?? false))
            .ToList();

        emptyState.Visible = settings.Targets.Count == 0;
        statusLabel.Text = ReadyMessage;
        UpdateSelection();
    }

    private void RefreshNetworkStatus()
    {
        if (settings is null)
        {
            return;
        }

        var current = RustDeskConfigReader.DetectDefaultProfile(settings);
        currentNetworkLabel.Text = current is null
            ? "RustDesk default: unknown"
            : $"RustDesk default: {current.Name}";
    }

    private async Task ConnectSelectedAsync()
    {
        if (!connectButton.Enabled)
        {
            return;
        }

        if (targetsGrid.SelectedRows.Count == 0)
        {
            MessageBox.Show(this, "Select a client first.", "RustDesk Network Switcher", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var index = targetsGrid.SelectedRows[0].Index;
        if (index < 0 || index >= settings.Targets.Count)
        {
            return;
        }

        var target = settings.Targets[index];
        var profile = settings.Profiles.FirstOrDefault(p => p.Id == target.ProfileId);
        if (profile is null)
        {
            MessageBox.Show(this, "This client points to a missing network profile. Use Manage networks to repair it.", "Configuration needed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        connecting = true;
        UpdateSelection();
        try
        {
            var current = RustDeskConfigReader.DetectDefaultProfile(settings);
            if (current is not null && !string.Equals(current.Id, profile.Id, StringComparison.OrdinalIgnoreCase))
            {
                var result = MessageBox.Show(
                    this,
                    $"{target.Name} uses “{profile.Name}”, while RustDesk's default is “{current.Name}”.\n\nRustDeskHop will route only this new connection through “{profile.Name}”. Existing sessions stay open.\n\nConnect now?",
                    "Route through another RustDesk network?",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result != DialogResult.Yes)
                {
                    statusLabel.Text = "Connection cancelled — existing sessions were not changed.";
                    return;
                }
            }

            if (profile.RequiresPrivateNetwork)
            {
                statusLabel.Text = $"Checking access to {profile.Name}…";
                var reachable = await NetworkProbe.CanReachAsync(profile);
                if (!reachable && !TailscaleState.IsRunning() && TailscaleState.TryStart())
                {
                    statusLabel.Text = "Starting Tailscale and retrying…";
                    await Task.Delay(2_000);
                    reachable = await NetworkProbe.CanReachAsync(profile);
                }

                if (!reachable)
                {
                    statusLabel.Text = $"{profile.Name} is not reachable.";
                    var explanation = TailscaleState.IsRunning()
                        ? "Tailscale is running, but the private RustDesk server did not answer. The server device may be offline or disconnected from Tailscale."
                        : "Tailscale is not running. Open and connect Tailscale, then try again.";
                    MessageBox.Show(
                        this,
                        $"{profile.Name} is not reachable.\n\n{explanation}\n\nRustDesk was not launched.",
                        "Private network unavailable",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }

            var rustDeskPath = RustDeskLocator.Find();
            if (rustDeskPath is null)
            {
                MessageBox.Show(this, "RustDesk was not found in the usual installation locations.", "RustDesk not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (profile.IsPublic && !await EnsurePublicLoginAsync(rustDeskPath, current))
            {
                return;
            }

            var connectTarget = ConnectionTargetBuilder.Build(target, profile);
            RustDeskLauncher.Connect(rustDeskPath, connectTarget);
            statusLabel.Text = $"Connecting to {target.Name} via {profile.Name}.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"RustDesk could not be started.\n\n{ex.Message}", "Connection failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            connecting = false;
            UpdateSelection();
            RefreshNetworkStatus();
        }
    }

    private async Task<bool> EnsurePublicLoginAsync(string rustDeskPath, ServerProfile? current)
    {
        if (RustDeskAccountState.HasLoginToken())
        {
            return true;
        }

        if (current is null || !current.IsPublic)
        {
            var result = MessageBox.Show(
                this,
                "RustDesk's public network needs a one-time browser sign-in. To make the Google/GitHub buttons available, RustDeskHop must make the public network RustDesk's default. Saved private clients will still use their own direct route.\n\nVisible RustDesk sessions will close, and Windows may ask for administrator approval. Continue?",
                "Prepare public RustDesk sign-in?",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
            {
                statusLabel.Text = "Public sign-in setup cancelled.";
                return false;
            }

            RustDeskSessions.CloseVisibleWindows();
            await Task.Delay(750);
            statusLabel.Text = "Preparing RustDesk's public sign-in…";
            var setup = await RustDeskPublicProfileSetup.RequestAsync();
            if (!setup.Success)
            {
                MessageBox.Show(this, setup.Message, "Public sign-in setup failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = "Public sign-in setup failed.";
                return false;
            }
        }

        using var signIn = new PublicSignInForm(rustDeskPath);
        if (signIn.ShowDialog(this) != DialogResult.OK)
        {
            statusLabel.Text = "Waiting for RustDesk public sign-in.";
            return false;
        }

        statusLabel.Text = "Public sign-in detected. Continuing connection…";
        return true;
    }

    private void AddClient()
    {
        if (settings.Profiles.Count == 0)
        {
            MessageBox.Show(this, "Add a network profile first.", "Configuration needed", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new TargetEditorForm(settings.Profiles, null);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            settings.Targets.Add(dialog.Target);
            ConfigStore.Save(settings);
            RefreshTargets();
        }
    }

    private void RemoveClient()
    {
        if (targetsGrid.SelectedRows.Count == 0)
        {
            return;
        }

        var index = targetsGrid.SelectedRows[0].Index;
        if (index < 0 || index >= settings.Targets.Count)
        {
            return;
        }

        var target = settings.Targets[index];
        if (MessageBox.Show(this, $"Remove “{target.Name}” from the companion?", "Remove client", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            settings.Targets.RemoveAt(index);
            ConfigStore.Save(settings);
            RefreshTargets();
        }
    }

    private void ManageProfiles()
    {
        using var dialog = new ProfilesForm(settings.Profiles);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            settings.Profiles = dialog.Profiles;
            ConfigStore.Save(settings);
            RefreshTargets();
        }
    }
}

internal sealed class TargetEditorForm : BrandedForm
{
    private readonly TextBox nameBox = new();
    private readonly TextBox idBox = new();
    private readonly ComboBox profileBox = new();

    public TargetDefinition Target { get; }

    public TargetEditorForm(IReadOnlyList<ServerProfile> profiles, TargetDefinition? existing)
    {
        Target = existing is null
            ? new TargetDefinition { ProfileId = profiles[0].Id }
            : new TargetDefinition { Name = existing.Name, RustDeskId = existing.RustDeskId, ProfileId = existing.ProfileId };

        Text = existing is null ? "Add computer" : "Edit computer";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(500, 250);

        WindowContent.Padding = new Padding(UiMetrics.Inset);
        var surface = new SurfacePanel { Dock = DockStyle.Fill, Padding = new Padding(UiMetrics.Inset) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 3; row++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        nameBox.Name = "ComputerName"; idBox.Name = "RustDeskId"; profileBox.Name = "Network";
        AddRow(layout, 0, "Name", nameBox);
        AddRow(layout, 1, "RustDesk ID", idBox);

        profileBox.DropDownStyle = ComboBoxStyle.DropDownList;
        profileBox.DataSource = profiles.ToList();
        profileBox.DisplayMember = nameof(ServerProfile.Name);
        profileBox.ValueMember = nameof(ServerProfile.Id);
        AddRow(layout, 2, "Network", profileBox);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Margin = Padding.Empty, Padding = new Padding(0, 12, 0, 0) };
        var save = new ModernButton { Text = "Save computer", Primary = true, DialogResult = DialogResult.OK, AutoSize = true, Margin = Padding.Empty };
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text) || string.IsNullOrWhiteSpace(idBox.Text) || profileBox.SelectedValue is null)
            {
                MessageBox.Show(this, "Name, RustDesk ID, and network are required.", "Incomplete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            Target.Name = nameBox.Text.Trim();
            Target.RustDeskId = idBox.Text.Trim();
            Target.ProfileId = profileBox.SelectedValue.ToString()!;
        };
        buttons.Controls.Add(save);
        buttons.Controls.Add(new ModernButton { Text = "Cancel", Quiet = true, DialogResult = DialogResult.Cancel, AutoSize = true });
        layout.Controls.Add(buttons, 0, 3);
        layout.SetColumnSpan(buttons, 2);
        surface.Controls.Add(layout);
        WindowContent.Controls.Add(surface);

        nameBox.Text = Target.Name;
        idBox.Text = Target.RustDeskId;
        profileBox.SelectedValue = Target.ProfileId;
        AcceptButton = save;
        CancelButton = (Button)buttons.Controls[1];
    }

    private static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
    {
        AppTheme.StyleEditor(control);
        control.AccessibleName = label;
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 16, 8), ForeColor = AppTheme.Muted }, 0, row);
        control = new InputSurface(control);
        control.Dock = DockStyle.Fill;
        layout.Controls.Add(control, 1, row);
    }
}

internal sealed class ProfilesForm : BrandedForm
{
    private readonly ListBox profileList = new();
    private readonly TextBox nameBox = new();
    private readonly TextBox addressBox = new();
    private readonly TextBox keyBox = new();
    private readonly CheckBox privateNetworkBox = new() { Text = "Requires Tailscale/private network", AutoSize = true };
    private readonly TextBox probeHostBox = new();
    private readonly NumericUpDown probePortBox = new() { Minimum = 1, Maximum = 65535, Value = 21116 };
    private readonly List<ServerProfile> profiles;
    private int selectedIndex = -1;

    public List<ServerProfile> Profiles => profiles;

    public ProfilesForm(IEnumerable<ServerProfile> source)
    {
        profiles = source.Select(Clone).ToList();
        Text = "Manage networks";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(780, 490);
        ClientSize = new Size(860, 460);
        WindowContent.Padding = new Padding(UiMetrics.SectionGap);
        var surface = new SurfacePanel { Padding = new Padding(UiMetrics.Inset) };

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.Panel1,
            SplitterWidth = UiMetrics.Inset,
        };
        split.Size = ClientSize;
        split.Panel1MinSize = 176;
        split.Panel2MinSize = 440;
        split.SplitterDistance = 208;

        profileList.Dock = DockStyle.Fill;
        profileList.Name = "Networks";
        profileList.AccessibleName = "Saved networks";
        profileList.BorderStyle = BorderStyle.None;
        profileList.BackColor = Color.White;
        profileList.IntegralHeight = false;
        profileList.DrawMode = DrawMode.OwnerDrawFixed;
        profileList.ItemHeight = UiMetrics.RowHeight;
        profileList.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            var selected = (e.State & DrawItemState.Selected) != 0;
            using var background = new SolidBrush(selected ? AppTheme.Selection : Color.White);
            e.Graphics.FillRectangle(background, e.Bounds);
            if (selected)
            {
                using var accent = new SolidBrush(AppTheme.Blue);
                e.Graphics.FillRectangle(accent, e.Bounds.Left, e.Bounds.Top + 10 * DeviceDpi / 96F,
                    3 * DeviceDpi / 96F, e.Bounds.Height - 20 * DeviceDpi / 96F);
            }
            var textBounds = Rectangle.Inflate(e.Bounds, -(int)(12 * DeviceDpi / 96F), -(int)(6 * DeviceDpi / 96F));
            TextRenderer.DrawText(e.Graphics, profileList.GetItemText(profileList.Items[e.Index]), Font, textBounds,
                AppTheme.Ink, TextFormatFlags.WordBreak | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
        };
        profileList.DisplayMember = nameof(ServerProfile.Name);
        profileList.SelectedIndexChanged += (_, _) => LoadSelected();
        split.Panel1.Controls.Add(profileList);

        var editor = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(UiMetrics.Inset, 0, 0, 0),
            ColumnCount = 2,
            RowCount = 8,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
        };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 6; row++) editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        editor.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        AddRow(editor, 0, "Profile name", nameBox);
        AddRow(editor, 1, "Server address", addressBox);
        AddRow(editor, 2, "Public key", keyBox);
        AddRow(editor, 3, "Probe host", probeHostBox);
        AddRow(editor, 4, "Probe port", probePortBox);
        privateNetworkBox.Margin = new Padding(3, 10, 3, 10);
        editor.Controls.Add(privateNetworkBox, 0, 5);
        editor.SetColumnSpan(privateNetworkBox, 2);

        var hint = new Label
        {
            Text = "Use “public” for RustDesk’s public network. Private profiles use the ID server address and its public key.",
            AutoSize = true,
            Dock = DockStyle.Top,
            ForeColor = AppTheme.Muted,
            Font = AppTheme.Small,
            Margin = new Padding(3, 8, 3, 12),
        };
        editor.Controls.Add(hint, 0, 6);
        editor.SetColumnSpan(hint, 2);

        void UpdateHintWidth()
        {
            var availableWidth = Math.Max(240, editor.ClientSize.Width - editor.Padding.Horizontal - hint.Margin.Horizontal);
            hint.MaximumSize = new Size(availableWidth, 0);
        }

        editor.SizeChanged += (_, _) => UpdateHintWidth();

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true,
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 0),
        };
        var add = new ModernButton { Text = "New", Glyph = UiGlyph.Plus, AutoSize = true };
        add.Click += (_, _) => NewProfile();
        var newNetworkArea = new Panel { Dock = DockStyle.Bottom, Height = UiMetrics.ButtonHeight + UiMetrics.Inset };
        add.Text = "New network";
        add.Dock = DockStyle.Bottom;
        newNetworkArea.Controls.Add(add);
        split.Panel1.Controls.Add(newNetworkArea);
        var save = new ModernButton { Text = "Save network", Primary = true, AutoSize = true, Margin = Padding.Empty };
        save.Click += (_, _) => SaveSelected();
        buttons.Controls.Add(save);
        var remove = new ModernButton { Text = "Delete", Quiet = true, AutoSize = true };
        remove.Click += (_, _) => DeleteSelected();
        var close = new ModernButton { Text = "Close", Quiet = true, DialogResult = DialogResult.OK, AutoSize = true };
        buttons.Controls.Add(close);
        buttons.Controls.Add(remove);
        editor.Controls.Add(buttons, 0, 7);
        editor.SetColumnSpan(buttons, 2);
        split.Panel2.Controls.Add(editor);

        surface.Controls.Add(split);
        WindowContent.Controls.Add(surface);
        WindowContent.Layout += (_, _) =>
        {
            var scale = DeviceDpi / 96F;
            var width = Math.Min((int)(UiMetrics.ContentWidth * scale), WindowContent.ClientSize.Width - WindowContent.Padding.Horizontal);
            var height = Math.Min((int)(460 * scale), WindowContent.ClientSize.Height - WindowContent.Padding.Vertical);
            surface.SetBounds((WindowContent.ClientSize.Width - width) / 2, WindowContent.Padding.Top, width, height);
        };
        AcceptButton = save;
        CancelButton = close;
        Shown += (_, _) =>
        {
            UpdateHintWidth();
            RebindProfiles(profiles.Count > 0 ? 0 : -1);
        };
    }

    private void LoadSelected()
    {
        if (profileList.SelectedIndex < 0 || profileList.SelectedIndex >= profiles.Count) return;
        selectedIndex = profileList.SelectedIndex;
        var profile = profiles[selectedIndex];
        nameBox.Text = profile.Name;
        addressBox.Text = profile.ServerAddress;
        keyBox.Text = profile.PublicKey;
        privateNetworkBox.Checked = profile.RequiresPrivateNetwork;
        probeHostBox.Text = profile.ProbeHost;
        probePortBox.Value = Math.Clamp(profile.ProbePort, 1, 65535);
    }

    private void NewProfile()
    {
        var profile = new ServerProfile { Name = "New network", ServerAddress = "", RequiresPrivateNetwork = true };
        profiles.Add(profile);
        RebindProfiles(profiles.Count - 1);
    }

    private void SaveSelected()
    {
        if (selectedIndex < 0 || selectedIndex >= profiles.Count) return;
        if (string.IsNullOrWhiteSpace(nameBox.Text) || string.IsNullOrWhiteSpace(addressBox.Text))
        {
            MessageBox.Show(this, "Profile name and server address are required.", "Incomplete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var profile = profiles[selectedIndex];
        profile.Name = nameBox.Text.Trim();
        profile.ServerAddress = addressBox.Text.Trim();
        profile.PublicKey = keyBox.Text.Trim();
        profile.RequiresPrivateNetwork = privateNetworkBox.Checked;
        profile.ProbeHost = probeHostBox.Text.Trim();
        profile.ProbePort = (int)probePortBox.Value;
        RebindProfiles(selectedIndex);
    }

    private void DeleteSelected()
    {
        if (selectedIndex < 0 || selectedIndex >= profiles.Count) return;
        if (profiles.Count <= 1)
        {
            MessageBox.Show(this, "At least one network profile must remain.", "Cannot delete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (MessageBox.Show(this, $"Delete “{profiles[selectedIndex].Name}”? Clients using it will need another profile.", "Delete profile", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        profiles.RemoveAt(selectedIndex);
        RebindProfiles(profiles.Count > 0 ? Math.Min(selectedIndex, profiles.Count - 1) : -1);
    }

    private void RebindProfiles(int index)
    {
        profileList.BeginUpdate();
        try
        {
            profileList.DataSource = null;
            profileList.DisplayMember = nameof(ServerProfile.Name);
            profileList.DataSource = profiles;
            profileList.SelectedIndex = index >= 0 && index < profiles.Count ? index : -1;
        }
        finally
        {
            profileList.EndUpdate();
        }
    }

    private static ServerProfile Clone(ServerProfile p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        ServerAddress = p.ServerAddress,
        PublicKey = p.PublicKey,
        RequiresPrivateNetwork = p.RequiresPrivateNetwork,
        ProbeHost = p.ProbeHost,
        ProbePort = p.ProbePort,
    };

    private static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
    {
        AppTheme.StyleEditor(control);
        control.AccessibleName = label;
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 16, 8), ForeColor = AppTheme.Muted }, 0, row);
        control = new InputSurface(control);
        control.Dock = DockStyle.Fill;
        layout.Controls.Add(control, 1, row);
    }
}
