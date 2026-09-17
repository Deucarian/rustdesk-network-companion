using System.Drawing.Drawing2D;

namespace Simultria.RustDeskCompanion;

internal sealed partial class MainForm
{
    private readonly ComputerGrid targetsGrid = new() { Name = "Computers", AccessibleName = "Saved computers" };
    private readonly Label currentNetworkLabel = AppTheme.Label("", color: AppTheme.Muted);
    private readonly Label statusLabel = AppTheme.Label("", AppTheme.Small, AppTheme.Muted);
    private readonly Label selectedName = AppTheme.Label("No computer selected", AppTheme.Strong);
    private readonly Label selectedRoute = AppTheme.Label("Add a computer to get started.", color: AppTheme.Muted);
    private readonly ModernButton connectButton = new() { Name = "Connect", Text = "Connect", Primary = true, Glyph = UiGlyph.Arrow, GlyphAfter = true };
    private readonly ModernButton addButton = new() { Name = "AddComputer", Text = "Add computer", Primary = true, Glyph = UiGlyph.Plus };
    private readonly ModernButton removeButton = new() { Name = "RemoveComputer", Text = "Remove computer", Glyph = UiGlyph.Trash };
    private readonly ModernButton profilesButton = new() { Name = "ManageNetworks", Text = "Manage networks", Glyph = UiGlyph.Network };
    private readonly Label emptyState = AppTheme.Label("No computers yet\nAdd your first computer to get started.", color: AppTheme.Muted);
    private AppSettings settings = null!;
    private readonly bool saveLoadedSettings;
    private bool connecting;
    private const string ReadyMessage = "Each computer uses its saved network. Existing sessions stay open.";

    public MainForm() : this(null) { }

    internal MainForm(AppSettings? initialSettings)
    {
        settings = initialSettings!;
        saveLoadedSettings = initialSettings is null;
        Text = "RustDeskHop";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(940, 640);
        ClientSize = new Size(1040, 684);
        BuildUi();
        Load += (_, _) =>
        {
            settings ??= ConfigStore.Load();
            if (saveLoadedSettings) ConfigStore.Save(settings);
            RefreshTargets();
        };
        Activated += (_, _) => RefreshNetworkStatus();
    }

    private void BuildUi()
    {
        var title = AppTheme.Label("Your computers", AppTheme.Heading);
        var subtitle = AppTheme.Label("Choose a computer. We’ll use its saved network.", color: AppTheme.Muted);
        var globe = new GlyphControl(UiGlyph.Globe);
        var selectedIcon = new GlyphControl(UiGlyph.Monitor);
        var info = new GlyphControl(UiGlyph.Info);
        var topRule = new Divider();
        var bottomRule = new Divider();
        var card = new SurfacePanel { Name = "ComputersCard", Padding = new Padding(2) };
        targetsGrid.Dock = DockStyle.Fill;
        card.Controls.Add(targetsGrid);
        emptyState.AutoSize = false; emptyState.TextAlign = ContentAlignment.MiddleCenter;
        emptyState.Dock = DockStyle.Fill; emptyState.Visible = false;
        card.Controls.Add(emptyState);
        foreach (var label in new[] { currentNetworkLabel, selectedName, selectedRoute, statusLabel })
            label.AutoSize = false;
        currentNetworkLabel.TextAlign = ContentAlignment.MiddleLeft;
        selectedName.TextAlign = ContentAlignment.MiddleLeft;
        selectedRoute.TextAlign = ContentAlignment.MiddleLeft;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        WindowContent.Controls.AddRange([title, subtitle, profilesButton, addButton, globe, currentNetworkLabel, card,
            topRule, selectedIcon, selectedName, selectedRoute, removeButton, connectButton, bottomRule, info, statusLabel]);

        WindowContent.Layout += (_, _) =>
        {
            var scale = DeviceDpi / 96F;
            int Px(float value) => (int)Math.Round(value * scale);
            var width = WindowContent.ClientSize.Width;
            var height = WindowContent.ClientSize.Height;
            var margin = Px(30);
            var right = width - margin;
            title.Location = new Point(margin, Px(25));
            subtitle.Location = new Point(margin, Px(73));
            var addWidth = Px(188); var manageWidth = Px(218);
            addButton.SetBounds(right - addWidth, Px(36), addWidth, Px(46));
            profilesButton.SetBounds(addButton.Left - Px(14) - manageWidth, Px(36), manageWidth, Px(46));
            // Keep the title and actions on separate lines on a narrow window.
            var compact = profilesButton.Left < subtitle.Right + Px(18);
            var networkY = Px(compact ? 149 : 121);
            if (compact)
            {
                profilesButton.Top = Px(105); addButton.Top = Px(105);
                networkY = Px(169);
            }
            globe.SetBounds(margin + Px(2), networkY, Px(23), Px(23));
            currentNetworkLabel.SetBounds(margin + Px(42), networkY - Px(2), right - margin - Px(42), Px(28));
            var cardTop = networkY + Px(46);
            var cardBottom = height - Px(208);
            card.SetBounds(margin, cardTop, right - margin, Math.Max(Px(152), cardBottom - cardTop));
            var ruleY = card.Bottom + Px(24);
            topRule.SetBounds(margin, ruleY, right - margin, 1);
            var actionY = ruleY + Px(27);
            connectButton.SetBounds(right - Px(150), actionY, Px(150), Px(54));
            removeButton.SetBounds(connectButton.Left - Px(18) - Px(225), actionY + Px(5), Px(225), Px(44));
            selectedIcon.SetBounds(margin + Px(5), actionY + Px(4), Px(46), Px(46));
            var summaryX = margin + Px(86);
            var summaryWidth = Math.Max(Px(120), removeButton.Left - summaryX - Px(20));
            selectedName.SetBounds(summaryX, actionY - Px(2), summaryWidth, Px(27));
            selectedRoute.SetBounds(summaryX, actionY + Px(27), summaryWidth, Px(44));
            bottomRule.SetBounds(margin, height - Px(59), right - margin, 1);
            info.SetBounds(margin + Px(2), height - Px(35), Px(22), Px(22));
            statusLabel.SetBounds(margin + Px(46), height - Px(48), right - margin - Px(46), Px(46));
        };
        targetsGrid.SelectionChanged += (_, _) => UpdateSelection();
        targetsGrid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await ConnectSelectedAsync(); };
        targetsGrid.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await ConnectSelectedAsync(); }
        };
        connectButton.Click += async (_, _) => await ConnectSelectedAsync();
        addButton.Click += (_, _) => AddClient();
        removeButton.Click += (_, _) => RemoveClient();
        profilesButton.Click += (_, _) => ManageProfiles();
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        var index = targetsGrid.SelectedRows.Count == 0 ? -1 : targetsGrid.SelectedRows[0].Index;
        var target = settings is not null && index >= 0 && index < settings.Targets.Count ? settings.Targets[index] : null;
        var profile = target is null ? null : settings!.Profiles.FirstOrDefault(p => p.Id == target.ProfileId);
        selectedName.Text = target?.Name ?? "No computer selected";
        selectedRoute.Text = target is null ? "Add a computer to get started." : profile is null ? "Choose a valid network before connecting." : $"Connect using {profile.Name}";
        connectButton.Enabled = target is not null && !connecting;
        removeButton.Enabled = target is not null && !connecting;
        addButton.Enabled = !connecting;
        profilesButton.Enabled = !connecting;
        targetsGrid.Enabled = !connecting;
    }
}

internal sealed record ComputerRow(string Name, string RustDeskId, string ProfileName, bool IsPublic);

internal sealed class ComputerGrid : DataGridView
{
    private bool sizingRows;
    private bool sizingScheduled;
    public ComputerGrid()
    {
        DoubleBuffered = true;
        BorderStyle = BorderStyle.None;
        BackgroundColor = Color.White;
        GridColor = Color.White;
        CellBorderStyle = DataGridViewCellBorderStyle.None;
        AdvancedCellBorderStyle.All = DataGridViewAdvancedCellBorderStyle.None;
        ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        EnableHeadersVisualStyles = false;
        AllowUserToAddRows = false; AllowUserToDeleteRows = false;
        AllowUserToResizeRows = false; AllowUserToResizeColumns = false;
        ReadOnly = true; MultiSelect = false; RowHeadersVisible = false;
        AutoGenerateColumns = false; SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        ColumnHeadersHeight = 52;
        RowTemplate.MinimumHeight = 64;
        Font = AppTheme.Body;
        DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White, ForeColor = AppTheme.Ink, SelectionBackColor = AppTheme.Selection,
            SelectionForeColor = AppTheme.Ink, WrapMode = DataGridViewTriState.True,
            Alignment = DataGridViewContentAlignment.MiddleLeft, Padding = new Padding(20, 12, 12, 12),
        };
        Columns.Add(new DataGridViewTextBoxColumn { Name = "Computer", HeaderText = "Computer", DataPropertyName = "Name", FillWeight = 37, SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle { Padding = new Padding(116, 12, 12, 12), Font = AppTheme.Strong } });
        Columns.Add(new DataGridViewTextBoxColumn { Name = "RustDeskId", HeaderText = "RustDesk ID", DataPropertyName = "RustDeskId", FillWeight = 27, SortMode = DataGridViewColumnSortMode.NotSortable });
        Columns.Add(new DataGridViewTextBoxColumn { Name = "Network", HeaderText = "Network", DataPropertyName = "ProfileName", FillWeight = 36, SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle { Padding = new Padding(20, 12, 94, 12) } });
        DataBindingComplete += (_, _) => ScheduleRowSizing();
        ColumnWidthChanged += (_, _) => ScheduleRowSizing();
        SizeChanged += (_, _) => ScheduleRowSizing();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ScheduleRowSizing();
    }

    private void ScheduleRowSizing()
    {
        if (!IsHandleCreated || IsDisposed || sizingRows || sizingScheduled) return;
        sizingScheduled = true;
        // Fill-mode columns cannot change row heights from inside their resize event.
        // Coalesce requests and measure only after the grid's layout has completed.
        BeginInvoke((Action)(() =>
        {
            sizingScheduled = false;
            if (!IsDisposed) SizeRowsToContent();
        }));
    }

    private void SizeRowsToContent()
    {
        if (sizingRows || Columns.Count != 3 || Rows.Count == 0) return;
        var previousWidth = Columns.Cast<DataGridViewColumn>().Sum(c => c.Width);
        sizingRows = true;
        try
        {
            var scale = DeviceDpi / 96F;
            int Px(float value) => (int)Math.Round(value * scale);
            var baseHeight = Math.Max(Px(64), Rows.Count <= 3 ? (ClientSize.Height - ColumnHeadersHeight) / 3 : 0);
            foreach (DataGridViewRow gridRow in Rows)
            {
                if (gridRow.DataBoundItem is not ComputerRow row) continue;
                var badgeWidth = TextRenderer.MeasureText(row.IsPublic ? "Public" : "Private", AppTheme.Small).Width + Px(18);
                int Measure(string text, Font font, int width) => TextRenderer.MeasureText(text, font,
                    new Size(Math.Max(Px(40), width), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
                var textHeight = Math.Max(Measure(row.Name, AppTheme.Strong, Columns[0].Width - Px(126)),
                    Measure(row.ProfileName, Font, Columns[2].Width - Px(48) - badgeWidth));
                textHeight = Math.Max(textHeight, Measure(row.RustDeskId, Font, Columns[1].Width - Px(40)));
                gridRow.Height = Math.Max(baseHeight, textHeight + Px(24));
            }
        }
        finally { sizingRows = false; }
        if (previousWidth != Columns.Cast<DataGridViewColumn>().Sum(c => c.Width)) ScheduleRowSizing();
        Invalidate();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ColumnHeadersHeight = (int)(52 * DeviceDpi / 96F);
        RowTemplate.MinimumHeight = (int)(64 * DeviceDpi / 96F);
        foreach (DataGridViewRow row in Rows) row.MinimumHeight = RowTemplate.MinimumHeight;
        ScheduleRowSizing();
    }

    protected override void OnCellPainting(DataGridViewCellPaintingEventArgs e)
    {
        base.OnCellPainting(e);
        if (e.ColumnIndex < 0) return;
        if (e.RowIndex < 0) PaintCell(e.Graphics!, e.CellBounds, e.RowIndex, e.ColumnIndex, e.FormattedValue?.ToString());
        e.Handled = true;
    }

    protected override void OnRowPrePaint(DataGridViewRowPrePaintEventArgs e)
    {
        base.OnRowPrePaint(e);
        e.PaintParts = DataGridViewPaintParts.None;
    }

    private void PaintCell(Graphics graphics, Rectangle bounds, int rowIndex, int columnIndex, string? text)
    {
        var scale = DeviceDpi / 96F;
        int Px(float value) => (int)Math.Round(value * scale);
        var selected = rowIndex >= 0 && Rows[rowIndex].Selected;
        using var background = new SolidBrush(selected ? AppTheme.Selection : Color.White);
        graphics.FillRectangle(background, bounds);
        if (rowIndex < 0)
        {
            TextRenderer.DrawText(graphics, text, Font, Rectangle.Inflate(bounds, -Px(20), 0), AppTheme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
        else
        {
            var textBounds = Rectangle.Inflate(bounds, -Px(20), -Px(8));
            if (columnIndex == 0)
            {
                if (selected)
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var blue = new SolidBrush(AppTheme.Blue);
                    var check = new Rectangle(bounds.Left + Px(22), bounds.Top + (bounds.Height - Px(25)) / 2, Px(25), Px(25));
                    graphics.FillEllipse(blue, check);
                    GlyphPainter.Draw(graphics, UiGlyph.Check, Rectangle.Inflate(check, -Px(3), -Px(3)), Color.White);
                }
                GlyphPainter.Draw(graphics, UiGlyph.Monitor, new Rectangle(bounds.Left + Px(75), bounds.Top + (bounds.Height - Px(26)) / 2, Px(26), Px(26)), AppTheme.Muted);
                textBounds.X = bounds.Left + Px(116); textBounds.Width = Math.Max(1, bounds.Right - Px(10) - textBounds.Left);
            }
            if (columnIndex == 2 && Rows[rowIndex].DataBoundItem is ComputerRow row)
            {
                var badgeText = row.IsPublic ? "Public" : "Private";
                var badgeSize = TextRenderer.MeasureText(badgeText, AppTheme.Small);
                var badgeWidth = badgeSize.Width + Px(18);
                var naturalWidth = TextRenderer.MeasureText(row.ProfileName, Font).Width;
                var badgeX = Math.Min(bounds.Right - badgeWidth - Px(16), textBounds.Left + naturalWidth + Px(12));
                var badge = new RectangleF(badgeX, bounds.Top + (bounds.Height - Px(27)) / 2, badgeWidth, Px(27));
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var shape = AppTheme.Round(badge, Px(14));
                using var brush = new SolidBrush(row.IsPublic ? Color.FromArgb(211, 229, 255) : Color.FromArgb(237, 236, 246));
                graphics.FillPath(brush, shape);
                TextRenderer.DrawText(graphics, badgeText, AppTheme.Small, Rectangle.Round(badge), row.IsPublic ? AppTheme.Blue : Color.FromArgb(68, 66, 97),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                textBounds.Width = Math.Max(1, badgeX - Px(10) - textBounds.Left);
            }
            TextRenderer.DrawText(graphics, text, columnIndex == 0 ? AppTheme.Strong : Font, textBounds, AppTheme.Ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }
    }

    protected override void OnRowPostPaint(DataGridViewRowPostPaintEventArgs e)
    {
        base.OnRowPostPaint(e);
        var graphicsState = e.Graphics.Save();
        e.Graphics.SetClip(new Rectangle(0, ColumnHeadersHeight, ClientSize.Width, Math.Max(0, ClientSize.Height - ColumnHeadersHeight)), CombineMode.Intersect);
        using (var background = new SolidBrush(Rows[e.RowIndex].Selected ? AppTheme.Selection : Color.White))
            e.Graphics.FillRectangle(background, e.RowBounds);
        for (var column = 0; column < Columns.Count; column++)
        {
            var cellBounds = GetCellDisplayRectangle(column, e.RowIndex, false);
            PaintCell(e.Graphics, cellBounds, e.RowIndex, column, Rows[e.RowIndex].Cells[column].FormattedValue?.ToString());
        }
        var bounds = new RectangleF(1, e.RowBounds.Top, ClientSize.Width - (Controls.OfType<VScrollBar>().Any(s => s.Visible) ? SystemInformation.VerticalScrollBarWidth : 0) - 3, e.RowBounds.Height - 1);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (Rows[e.RowIndex].Selected)
        {
            using var path = AppTheme.Round(bounds, 8 * DeviceDpi / 96F);
            using var pen = new Pen(AppTheme.Blue);
            e.Graphics.DrawPath(pen, path);
        }
        else
        {
            using var pen = new Pen(AppTheme.Line);
            e.Graphics.DrawLine(pen, bounds.Left, bounds.Bottom, bounds.Right, bounds.Bottom);
        }
        e.Graphics.Restore(graphicsState);
    }
}
