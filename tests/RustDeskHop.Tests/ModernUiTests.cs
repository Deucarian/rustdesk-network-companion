using System.Drawing;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using Simultria.RustDeskCompanion;
using Xunit;

namespace RustDeskHop.Tests;

public sealed class ModernUiTests
{
    [Fact]
    public void EmptyListHasClearGuidanceAndDisabledActions() => OnSta(() =>
    {
        using var form = new MainForm(Settings(0));
        Load(form);
        Assert.False(Find<ModernButton>(form, "Connect").Enabled);
        Assert.False(Find<ModernButton>(form, "RemoveComputer").Enabled);
        Assert.True(Find<ModernButton>(form, "AddComputer").Enabled);
        Assert.Contains(Descendants(form).OfType<Label>(), l => l.Text.Contains("No computers yet"));
        Assert.Empty(form.Controls.Find("ApplicationLogo", true));
        Assert.Single(form.Controls.Find("TitleBarIcon", true));
    });

    [Fact]
    public void SelectionUpdatesTheComputerAndAssignedRoute() => OnSta(() =>
    {
        using var form = new MainForm(Settings(3));
        Load(form);
        var grid = Find<ComputerGrid>(form, "Computers");
        Assert.Equal(3, grid.Rows.Count);
        grid.CurrentCell = grid.Rows[1].Cells[0];
        Assert.Contains(Descendants(form).OfType<Label>(), l => l.Text == "Computer 2");
        Assert.Contains(Descendants(form).OfType<Label>(), l => l.Text == "Connect using Test private network");
        Assert.True(Find<ModernButton>(form, "Connect").Enabled);
        Assert.Equal(DataGridViewCellBorderStyle.None, grid.CellBorderStyle);
        Assert.Equal(AppTheme.Selection, grid.DefaultCellStyle.SelectionBackColor);
        Assert.True(grid.ReadOnly);
        Assert.False(grid.MultiSelect);
    });

    [Theory]
    [InlineData(940, 640)]
    [InlineData(1040, 684)]
    [InlineData(1440, 940)]
    public void MainActionsFitWithoutOverlapping(int width, int height) => OnSta(() =>
    {
        using var form = new MainForm(Settings(3));
        Load(form);
        form.Size = new Size(width, height);
        form.PerformLayout();
        Application.DoEvents();
        var names = new[] { "Connect", "RemoveComputer", "AddComputer", "ManageNetworks", "ComputersCard" };
        var controls = names.Select(n => Assert.Single(form.Controls.Find(n, true))).ToArray();
        foreach (var control in controls)
            Assert.True(control.Parent!.ClientRectangle.Contains(control.Bounds), $"{control.Name}: {control.Bounds}, parent {control.Parent.ClientRectangle}");
        for (var first = 0; first < controls.Length; first++)
        for (var second = first + 1; second < controls.Length; second++)
            Assert.False(controls[first].Bounds.IntersectsWith(controls[second].Bounds), $"{controls[first].Name} overlaps {controls[second].Name}");
        Assert.All(controls.OfType<ModernButton>(), b => Assert.True(b.Width >= b.GetPreferredSize(Size.Empty).Width - 2, b.Name));
    });

    [Fact]
    public void RepeatedResizingDoesNotResizeRowsDuringColumnLayout() => OnSta(() =>
    {
        using var form = new MainForm(Settings(3));
        Load(form);
        var grid = Find<ComputerGrid>(form, "Computers");
        for (var iteration = 0; iteration < 4; iteration++)
        foreach (var size in new[] { new Size(1920, 1040), new Size(940, 640), new Size(1040, 684) })
        {
            form.Size = size;
            form.PerformLayout();
            Application.DoEvents();
            Assert.True(grid.Rows[0].Height >= 64);
            Assert.Equal(3, grid.Rows.Count);
        }
    });

    [Fact]
    public void ManyComputersRemainAvailableAndLongNamesWrap() => OnSta(() =>
    {
        var settings = Settings(50);
        settings.Targets[1].Name = "A long computer name that should wrap over several lines instead of disappearing";
        settings.Profiles[1].Name = "An unusually long private network name that must remain readable";
        using var form = new MainForm(settings);
        Load(form);
        var grid = Find<ComputerGrid>(form, "Computers");
        Assert.Equal(50, grid.Rows.Count);
        Assert.True(grid.Rows[1].Height > grid.RowTemplate.MinimumHeight);
        grid.CurrentCell = grid.Rows[49].Cells[0];
        Assert.Equal(49, Assert.Single(grid.SelectedRows.Cast<DataGridViewRow>()).Index);
        Assert.Contains(Descendants(form).OfType<Label>(), l => l.Text == "Computer 50");
    });

    [Fact]
    public void EditorsUseConsistentButtonsAndContainedFields() => OnSta(() =>
    {
        var settings = Settings(3);
        using var target = new TargetEditorForm(settings.Profiles, null);
        using var networks = new ProfilesForm(settings.Profiles);
        foreach (var form in new Form[] { target, networks })
        {
            _ = form.Handle;
            form.PerformLayout();
            foreach (var field in Descendants(form).OfType<InputSurface>())
            {
                Assert.True(field.Parent!.ClientRectangle.Contains(field.Bounds), $"Field exceeds editor: {field.Bounds}");
                Assert.True(field.Height >= 40);
            }
            var buttons = Descendants(form).OfType<Button>().Where(b => b.AccessibleName is not ("Minimize" or "Maximize or restore" or "Close"));
            Assert.All(buttons, b => Assert.IsType<ModernButton>(b));
        }
    });

    private static void Load(MainForm form)
    {
        _ = form.Handle;
        foreach (var control in Descendants(form)) _ = control.Handle;
        typeof(Form).GetMethod("OnLoad", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, [EventArgs.Empty]);
        form.PerformLayout();
        Application.DoEvents();
    }
    private static T Find<T>(Control root, string name) where T : Control => Assert.IsType<T>(Assert.Single(root.Controls.Find(name, true)));
    private static IEnumerable<Control> Descendants(Control root) => root.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));
    private static AppSettings Settings(int count) => new()
    {
        Profiles = [new() { Id = "public", Name = "Test public", ServerAddress = "public" }, new() { Id = "private", Name = "Test private network", ServerAddress = "example.invalid:21116" }],
        Targets = Enumerable.Range(1, count).Select(i => new TargetDefinition { Name = $"Computer {i}", RustDeskId = $"123456{i:000}", ProfileId = i % 2 == 0 ? "private" : "public" }).ToList(),
    };
    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception e) { failure = e; } }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "UI test timed out");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
