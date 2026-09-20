using System.Drawing.Drawing2D;

namespace Simultria.RustDeskCompanion;

internal sealed partial class MainForm
{
    private readonly ComputerGrid targetsGrid = new() { Name = "Computers", AccessibleName = "Saved computers" };
    private readonly Label currentNetworkLabel = AppTheme.Label("", AppTheme.Small, AppTheme.Muted);
    private readonly Label statusLabel = AppTheme.Label("", AppTheme.Small, AppTheme.Muted);
    private readonly Label selectedName = AppTheme.Label("No computer selected", AppTheme.Strong);
    private readonly Label selectedRoute = AppTheme.Label("Add a computer to get started.", AppTheme.Small, AppTheme.Muted);
    private readonly ModernButton connectButton = new() { Name = "Connect", Text = "Connect", Primary = true, Glyph = UiGlyph.Arrow, GlyphAfter = true };
    private readonly ModernButton addButton = new() { Name = "AddComputer", Text = "Add computer", Glyph = UiGlyph.Plus };
    private readonly ModernButton removeButton = new() { Name = "RemoveComputer", Text = "Remove", AccessibleName = "Remove selected computer", Quiet = true };
    private readonly ModernButton profilesButton = new() { Name = "ManageNetworks", Text = "Manage networks", Quiet = true };
    private readonly Label emptyState = AppTheme.Label("No computers yet\nAdd your first computer to get started.", color: AppTheme.Muted);
    private AppSettings settings = null!;
    private readonly bool saveLoadedSettings;
    private bool connecting;
    private const string ReadyMessage = "Existing sessions stay open.";

    public MainForm() : this(null) { }

    internal MainForm(AppSettings? initialSettings)
    {
        settings = initialSettings!;
        saveLoadedSettings = initialSettings is null;
        Text = "RustDeskHop";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(740, 480);
        ClientSize = new Size(900, 490);
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
        var divider = new Divider();
        var card = new SurfacePanel { Name = "ComputersCard" };
        title.Name = "PageTitle"; subtitle.Name = "PageSubtitle";
        selectedName.Name = "SelectedComputer"; selectedRoute.Name = "SelectedRoute";
        currentNetworkLabel.Name = "CurrentNetwork"; statusLabel.Name = "Status";
        foreach (var label in new[] { title, subtitle, currentNetworkLabel, selectedName, selectedRoute, statusLabel })
            label.AutoSize = false;
        WindowContent.AutoScroll = true;
        emptyState.AutoSize = false; emptyState.TextAlign = ContentAlignment.MiddleCenter;
        emptyState.Visible = false;
        card.Controls.AddRange([targetsGrid, emptyState, divider, selectedName, selectedRoute, removeButton, connectButton]);
        WindowContent.Controls.AddRange([title, subtitle, profilesButton, addButton, card, currentNetworkLabel, statusLabel]);

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
                var width = Math.Min(Px(UiMetrics.ContentWidth), WindowContent.ClientSize.Width - Px(2 * UiMetrics.PageInset));
                var left = (WindowContent.ClientSize.Width - width) / 2;
                var scroll = WindowContent.AutoScrollPosition;
                void Place(Control control, int x, int y, int w, int h) =>
                    control.SetBounds(x + scroll.X, y + scroll.Y, w, h);
                var inset = Px(UiMetrics.Inset);
                var gap = Px(UiMetrics.Gap);
                var sectionGap = Px(UiMetrics.SectionGap);
                var addSize = addButton.GetPreferredSize(Size.Empty);
                var manageSize = profilesButton.GetPreferredSize(Size.Empty);
                var toolbarWidth = addSize.Width + manageSize.Width + gap;
                var stackedToolbar = title.GetPreferredSize(Size.Empty).Width + toolbarWidth + sectionGap > width;
                var titleHeight = TextHeight(title, width);
                var top = sectionGap;
                Place(title, left, top, stackedToolbar ? width : width - toolbarWidth - sectionGap, titleHeight);
                var subtitleY = top + Math.Max(titleHeight, stackedToolbar ? 0 : addSize.Height) + gap;
                var subtitleHeight = TextHeight(subtitle, width);
                Place(subtitle, left, subtitleY, width, subtitleHeight);
                var toolbarY = stackedToolbar ? subtitleY + subtitleHeight + inset : top;
                Place(profilesButton, left + width - toolbarWidth, toolbarY, manageSize.Width, manageSize.Height);
                Place(addButton, left + width - addSize.Width, toolbarY, addSize.Width, addSize.Height);
                var cardTop = Math.Max(subtitleY + subtitleHeight, toolbarY + addSize.Height) + sectionGap;

                var innerWidth = width - 2 * inset;
                var connectSize = connectButton.GetPreferredSize(Size.Empty);
                var removeSize = removeButton.GetPreferredSize(Size.Empty);
                var actionsWidth = connectSize.Width + removeSize.Width + gap;
                var actionInset = inset + Px(UiMetrics.CellInset);
                var actionWidth = width - 2 * actionInset;
                var stackedActions = actionWidth - actionsWidth - sectionGap < Px(220);
                var summaryWidth = actionWidth - (stackedActions ? 0 : actionsWidth + sectionGap);
                var nameHeight = TextHeight(selectedName, summaryWidth);
                var routeHeight = TextHeight(selectedRoute, summaryWidth);
                var summaryHeight = nameHeight + Px(4) + routeHeight;
                var actionHeight = stackedActions ? summaryHeight + inset + connectSize.Height : Math.Max(summaryHeight, connectSize.Height);

                var networkWidth = Math.Min(innerWidth, currentNetworkLabel.GetPreferredSize(Size.Empty).Width);
                var statusWidth = statusLabel.GetPreferredSize(Size.Empty).Width;
                var stackedFooter = networkWidth + statusWidth + sectionGap > width;
                var networkHeight = TextHeight(currentNetworkLabel, width);
                var statusHeight = TextHeight(statusLabel, stackedFooter ? width : width - networkWidth - sectionGap);
                var footerHeight = stackedFooter ? networkHeight + gap + statusHeight : Math.Max(networkHeight, statusHeight);
                var cardChrome = inset * 4 + actionHeight + 1;
                var availableGridHeight = WindowContent.ClientSize.Height - cardTop - cardChrome - inset - footerHeight - sectionGap;
                var gridHeight = Math.Clamp(targetsGrid.ContentHeight, Px(100), Math.Max(Px(100), availableGridHeight));
                Place(card, left, cardTop, width, gridHeight + cardChrome);
                targetsGrid.SetBounds(inset, inset, innerWidth, gridHeight);
                emptyState.Bounds = targetsGrid.Bounds;
                var dividerY = targetsGrid.Bottom + inset;
                divider.SetBounds(inset, dividerY, innerWidth, 1);
                var summaryY = dividerY + inset;
                selectedName.SetBounds(actionInset, summaryY, summaryWidth, nameHeight);
                selectedRoute.SetBounds(actionInset, summaryY + nameHeight + Px(4), summaryWidth, routeHeight);
                var actionY = summaryY + (stackedActions ? summaryHeight + inset : Math.Max(0, (summaryHeight - connectSize.Height) / 2));
                connectButton.SetBounds(width - actionInset - connectSize.Width, actionY, connectSize.Width, connectSize.Height);
                removeButton.SetBounds(width - actionInset - actionsWidth, actionY, removeSize.Width, removeSize.Height);

                var footerY = cardTop + card.Height + inset;
                Place(currentNetworkLabel, left, footerY, stackedFooter ? width : networkWidth, networkHeight);
                var statusX = stackedFooter ? left : left + networkWidth + sectionGap;
                statusLabel.TextAlign = stackedFooter ? ContentAlignment.TopLeft : ContentAlignment.TopRight;
                Place(statusLabel, statusX, footerY + (stackedFooter ? networkHeight + gap : 0),
                    stackedFooter ? width : width - networkWidth - sectionGap, statusHeight);
                WindowContent.AutoScrollMinSize = new Size(0, footerY + footerHeight + sectionGap);
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
        ColumnHeadersHeight = UiMetrics.TableHeaderHeight;
        RowTemplate.MinimumHeight = UiMetrics.RowHeight;
        Font = AppTheme.Body;
        DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White, ForeColor = AppTheme.Ink, SelectionBackColor = AppTheme.Selection,
            SelectionForeColor = AppTheme.Ink, WrapMode = DataGridViewTriState.True,
            Alignment = DataGridViewContentAlignment.MiddleLeft, Padding = new Padding(UiMetrics.CellInset, 8, UiMetrics.CellInset, 8),
        };
        Columns.Add(new DataGridViewTextBoxColumn { Name = "Computer", HeaderText = "Computer", DataPropertyName = "Name", FillWeight = 38, SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle { Padding = new Padding(UiMetrics.ComputerTextInset, 8, UiMetrics.CellInset, 8), Font = AppTheme.Strong } });
        Columns.Add(new DataGridViewTextBoxColumn { Name = "RustDeskId", HeaderText = "RustDesk ID", DataPropertyName = "RustDeskId", FillWeight = 24, SortMode = DataGridViewColumnSortMode.NotSortable });
        Columns.Add(new DataGridViewTextBoxColumn { Name = "Network", HeaderText = "Network", DataPropertyName = "ProfileName", FillWeight = 38, SortMode = DataGridViewColumnSortMode.NotSortable });
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
            var baseHeight = Px(UiMetrics.RowHeight);
            foreach (DataGridViewRow gridRow in Rows)
            {
                if (gridRow.DataBoundItem is not ComputerRow row) continue;
                var badgeWidth = TextRenderer.MeasureText(row.IsPublic ? "Public" : "Private", AppTheme.Small).Width + Px(18);
                int Measure(string text, Font font, int width) => TextRenderer.MeasureText(text, font,
                    new Size(Math.Max(Px(40), width), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
                var textHeight = Math.Max(Measure(row.Name, AppTheme.Strong, Columns[0].Width - Px(UiMetrics.ComputerTextInset + UiMetrics.CellInset)),
                    Measure(row.ProfileName, Font, Columns[2].Width - Px(2 * UiMetrics.CellInset + UiMetrics.Gap) - badgeWidth));
                textHeight = Math.Max(textHeight, Measure(row.RustDeskId, Font, Columns[1].Width - Px(2 * UiMetrics.CellInset)));
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
        ColumnHeadersHeight = (int)(UiMetrics.TableHeaderHeight * DeviceDpi / 96F);
        RowTemplate.MinimumHeight = (int)(UiMetrics.RowHeight * DeviceDpi / 96F);
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
            var headerBounds = Rectangle.Inflate(bounds, -Px(UiMetrics.CellInset), 0);
            if (columnIndex == 0) { headerBounds.X = bounds.Left + Px(UiMetrics.ComputerTextInset); headerBounds.Width = bounds.Right - headerBounds.X; }
            TextRenderer.DrawText(graphics, text, AppTheme.Small, headerBounds, AppTheme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
        else
        {
            var textBounds = Rectangle.Inflate(bounds, -Px(UiMetrics.CellInset), -Px(8));
            if (columnIndex == 0)
            {
                GlyphPainter.Draw(graphics, UiGlyph.Monitor, new Rectangle(bounds.Left + Px(UiMetrics.CellInset), bounds.Top + (bounds.Height - Px(22)) / 2, Px(22), Px(22)), AppTheme.Muted);
                textBounds.X = bounds.Left + Px(UiMetrics.ComputerTextInset);
                textBounds.Width = Math.Max(1, bounds.Right - Px(UiMetrics.CellInset) - textBounds.Left);
            }
            if (columnIndex == 2 && Rows[rowIndex].DataBoundItem is ComputerRow row)
            {
                var badgeText = row.IsPublic ? "Public" : "Private";
                var badgeSize = TextRenderer.MeasureText(badgeText, AppTheme.Small);
                var badgeWidth = badgeSize.Width + Px(18);
                var badgeX = bounds.Right - badgeWidth - Px(UiMetrics.CellInset);
                var badge = new RectangleF(badgeX, bounds.Top + (bounds.Height - Px(24)) / 2, badgeWidth, Px(24));
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var shape = AppTheme.Round(badge, Px(14));
                using var brush = new SolidBrush(AppTheme.Badge);
                graphics.FillPath(brush, shape);
                TextRenderer.DrawText(graphics, badgeText, AppTheme.Small, Rectangle.Round(badge), AppTheme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                textBounds.Width = Math.Max(1, badgeX - Px(UiMetrics.Gap) - textBounds.Left);
            }
            TextRenderer.DrawText(graphics, text, columnIndex == 0 ? AppTheme.Strong : Font, textBounds, columnIndex == 1 ? AppTheme.Muted : AppTheme.Ink,
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
            using var accent = new SolidBrush(AppTheme.Blue);
            e.Graphics.FillRectangle(accent, 0, bounds.Top + 10 * DeviceDpi / 96F, 3 * DeviceDpi / 96F, bounds.Height - 20 * DeviceDpi / 96F);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Round(RectangleF.Inflate(bounds, -4, -4)), AppTheme.Muted, AppTheme.Selection);
        }
        e.Graphics.Restore(graphicsState);
    }
}
