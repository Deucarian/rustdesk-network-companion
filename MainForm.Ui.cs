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
        MinimumSize = new Size(740, 480);
        ClientSize = new Size(900, 530);
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
        title.Name = "PageTitle"; subtitle.Name = "PageSubtitle";
        selectedName.Name = "SelectedComputer"; selectedRoute.Name = "SelectedRoute";
        currentNetworkLabel.Name = "CurrentNetwork"; statusLabel.Name = "Status";
        title.AutoSize = false; subtitle.AutoSize = false;
        WindowContent.AutoScroll = true;
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

        var layingOut = false;
        WindowContent.Layout += (_, _) =>
        {
            if (layingOut) return;
            layingOut = true;
            try
            {
                var scale = DeviceDpi / 96F;
                int Px(float value) => (int)Math.Round(value * scale);
                int TextHeight(Label label, int width) => TextRenderer.MeasureText(label.Text, label.Font,
                    new Size(Math.Max(1, width), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
                var viewportWidth = WindowContent.ClientSize.Width;
                var width = Math.Min(Px(1080), Math.Max(Px(300), viewportWidth - Px(44)));
                var left = (viewportWidth - width) / 2;
                var right = left + width;
                var scroll = WindowContent.AutoScrollPosition;
                void Place(Control control, int x, int y, int w, int h) =>
                    control.SetBounds(x + scroll.X, y + scroll.Y, w, h);

                var addSize = addButton.GetPreferredSize(Size.Empty);
                var manageSize = profilesButton.GetPreferredSize(Size.Empty);
                var toolbarWidth = addSize.Width + manageSize.Width + Px(10);
                var titleWidth = title.GetPreferredSize(Size.Empty).Width;
                var stackedToolbar = titleWidth + toolbarWidth + Px(24) > width;
                var titleHeight = TextHeight(title, width);
                var toolbarY = Px(18);
                Place(title, left, Px(16), stackedToolbar ? width : width - toolbarWidth - Px(24), titleHeight);
                var subtitleY = Px(16) + Math.Max(titleHeight, stackedToolbar ? 0 : addSize.Height) + Px(6);
                var subtitleHeight = TextHeight(subtitle, width);
                Place(subtitle, left, subtitleY, width, subtitleHeight);
                if (stackedToolbar) toolbarY = subtitleY + subtitleHeight + Px(12);
                Place(addButton, right - addSize.Width, toolbarY, addSize.Width, addSize.Height);
                Place(profilesButton, right - toolbarWidth, toolbarY, manageSize.Width, manageSize.Height);

                var networkY = Math.Max(subtitleY + subtitleHeight, toolbarY + addSize.Height) + Px(18);
                var networkHeight = Math.Max(Px(20), TextHeight(currentNetworkLabel, width - Px(30)));
                Place(globe, left, networkY, Px(19), Px(19));
                Place(currentNetworkLabel, left + Px(30), networkY, width - Px(30), networkHeight);
                var cardTop = networkY + networkHeight + Px(12);

                var connectSize = connectButton.GetPreferredSize(Size.Empty);
                var removeSize = removeButton.GetPreferredSize(Size.Empty);
                var actionsWidth = connectSize.Width + removeSize.Width + Px(10);
                var stackedActions = width - actionsWidth - Px(72) < Px(220);
                var summaryWidth = width - Px(52) - (stackedActions ? 0 : actionsWidth + Px(20));
                var nameHeight = TextHeight(selectedName, summaryWidth);
                var routeHeight = TextHeight(selectedRoute, summaryWidth);
                var summaryHeight = Math.Max(Px(38), nameHeight + Px(3) + routeHeight);
                var actionHeight = stackedActions ? summaryHeight + Px(12) + connectSize.Height : Math.Max(summaryHeight, connectSize.Height);
                var statusHeight = Math.Max(Px(20), TextHeight(statusLabel, width - Px(30)));
                var belowCard = Px(18 + 16 + 18 + 14 + 20) + actionHeight + statusHeight;
                // Keep a short list compact, even when maximized. A long list scrolls
                // inside the card; unusually tall text can scroll the whole dashboard.
                var availableHeight = WindowContent.ClientSize.Height - cardTop - belowCard;
                var cardHeight = Math.Clamp(targetsGrid.ContentHeight + card.Padding.Vertical, Px(112), Math.Max(Px(112), availableHeight));
                Place(card, left, cardTop, width, cardHeight);
                var ruleY = cardTop + cardHeight + Px(18);
                Place(topRule, left, ruleY, width, 1);
                var summaryY = ruleY + Px(16);
                var actionsY = summaryY + (stackedActions ? summaryHeight + Px(12) : 0);
                Place(selectedIcon, left + Px(2), summaryY + Px(3), Px(32), Px(32));
                Place(selectedName, left + Px(52), summaryY, summaryWidth, nameHeight);
                Place(selectedRoute, left + Px(52), summaryY + nameHeight + Px(3), summaryWidth, routeHeight);
                Place(connectButton, right - connectSize.Width, actionsY, connectSize.Width, connectSize.Height);
                Place(removeButton, right - actionsWidth, actionsY, removeSize.Width, removeSize.Height);
                var footerY = summaryY + actionHeight + Px(18);
                Place(bottomRule, left, footerY, width, 1);
                Place(info, left, footerY + Px(14), Px(18), Px(18));
                Place(statusLabel, left + Px(30), footerY + Px(14), width - Px(30), statusHeight);
                WindowContent.AutoScrollMinSize = new Size(0, footerY + Px(14) + statusHeight + Px(20));
            }
            finally { layingOut = false; }
        };
        targetsGrid.ContentHeightChanged += (_, _) => WindowContent.PerformLayout();
        foreach (var label in new[] { currentNetworkLabel, selectedName, selectedRoute, statusLabel })
            label.TextChanged += (_, _) => WindowContent.PerformLayout();
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
    private int lastContentHeight;
    internal int ContentHeight => ColumnHeadersHeight + Rows.Cast<DataGridViewRow>().Sum(row => row.Height);
    internal event EventHandler? ContentHeightChanged;
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
        ColumnHeadersHeight = 40;
        RowTemplate.MinimumHeight = 52;
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
        if (sizingRows || Columns.Count != 3) return;
        var previousWidth = Columns.Cast<DataGridViewColumn>().Sum(c => c.Width);
        sizingRows = true;
        try
        {
            var scale = DeviceDpi / 96F;
            int Px(float value) => (int)Math.Round(value * scale);
            var baseHeight = Px(52);
            foreach (DataGridViewRow gridRow in Rows)
            {
                if (gridRow.DataBoundItem is not ComputerRow row) continue;
                var badgeWidth = TextRenderer.MeasureText(row.IsPublic ? "Public" : "Private", AppTheme.Small).Width + Px(18);
                int Measure(string text, Font font, int width) => TextRenderer.MeasureText(text, font,
                    new Size(Math.Max(Px(40), width), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
                var textHeight = Math.Max(Measure(row.Name, AppTheme.Strong, Columns[0].Width - Px(92)),
                    Measure(row.ProfileName, Font, Columns[2].Width - Px(40) - badgeWidth));
                textHeight = Math.Max(textHeight, Measure(row.RustDeskId, Font, Columns[1].Width - Px(28)));
                gridRow.Height = Math.Max(baseHeight, textHeight + Px(20));
            }
        }
        finally { sizingRows = false; }
        if (previousWidth != Columns.Cast<DataGridViewColumn>().Sum(c => c.Width)) ScheduleRowSizing();
        if (lastContentHeight != ContentHeight)
        {
            lastContentHeight = ContentHeight;
            ContentHeightChanged?.Invoke(this, EventArgs.Empty);
        }
        Invalidate();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ColumnHeadersHeight = (int)(40 * DeviceDpi / 96F);
        RowTemplate.MinimumHeight = (int)(52 * DeviceDpi / 96F);
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
            TextRenderer.DrawText(graphics, text, Font, Rectangle.Inflate(bounds, -Px(14), 0), AppTheme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
        else
        {
            var textBounds = Rectangle.Inflate(bounds, -Px(14), -Px(8));
            if (columnIndex == 0)
            {
                if (selected)
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var blue = new SolidBrush(AppTheme.Blue);
                    var check = new Rectangle(bounds.Left + Px(14), bounds.Top + (bounds.Height - Px(20)) / 2, Px(20), Px(20));
                    graphics.FillEllipse(blue, check);
                    GlyphPainter.Draw(graphics, UiGlyph.Check, Rectangle.Inflate(check, -Px(3), -Px(3)), Color.White);
                }
                GlyphPainter.Draw(graphics, UiGlyph.Monitor, new Rectangle(bounds.Left + Px(49), bounds.Top + (bounds.Height - Px(22)) / 2, Px(22), Px(22)), AppTheme.Muted);
                textBounds.X = bounds.Left + Px(82); textBounds.Width = Math.Max(1, bounds.Right - Px(10) - textBounds.Left);
            }
            if (columnIndex == 2 && Rows[rowIndex].DataBoundItem is ComputerRow row)
            {
                var badgeText = row.IsPublic ? "Public" : "Private";
                var badgeSize = TextRenderer.MeasureText(badgeText, AppTheme.Small);
                var badgeWidth = badgeSize.Width + Px(18);
                var naturalWidth = TextRenderer.MeasureText(row.ProfileName, Font).Width;
                var badgeX = Math.Min(bounds.Right - badgeWidth - Px(16), textBounds.Left + naturalWidth + Px(12));
                var badge = new RectangleF(badgeX, bounds.Top + (bounds.Height - Px(24)) / 2, badgeWidth, Px(24));
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
