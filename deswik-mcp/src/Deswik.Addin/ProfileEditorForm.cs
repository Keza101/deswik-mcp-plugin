using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Deswik.Ug.Design.Profiles;

namespace Deswik.Addin;

internal sealed class ProfileEditorForm : Form
{
    private readonly PropertyGrid _grid = new() { Dock = DockStyle.Fill, HelpVisible = true };
    private readonly ComboBox _saved = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 225 };
    private DesignProfile _profile = ProfileJson.Starter();

    private static string ProfileDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "DeswikMcp", "Profiles");

    public ProfileEditorForm()
    {
        Text = "UG design profiles — synthetic only";
        Width = 690;
        Height = 560;
        MinimumSize = new System.Drawing.Size(530, 430);
        StartPosition = FormStartPosition.CenterParent;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var warning = new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            Text = "SYNTHETIC TEST PROFILE — not approved for production drill design. Edit values, then save a new JSON profile."
        };

        var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
        var starter = new Button { Text = "Load starter", AutoSize = true };
        starter.Click += (_, _) => ShowProfile(ProfileJson.Starter());
        var open = new Button { Text = "Open saved", AutoSize = true };
        open.Click += (_, _) => OpenSaved();
        var save = new Button { Text = "Save as new", AutoSize = true };
        save.Click += (_, _) => SaveAsNew();
        bar.Controls.Add(starter);
        bar.Controls.Add(_saved);
        bar.Controls.Add(open);
        bar.Controls.Add(save);

        layout.Controls.Add(warning, 0, 0);
        layout.Controls.Add(bar, 0, 1);
        layout.Controls.Add(_grid, 0, 2);
        Controls.Add(layout);
        ShowProfile(_profile);
        RefreshSaved();
    }

    private void ShowProfile(DesignProfile profile)
    {
        _profile = profile;
        _grid.SelectedObject = profile;
        _grid.ExpandAllGridItems();
    }

    private void RefreshSaved()
    {
        _saved.Items.Clear();
        if (!Directory.Exists(ProfileDirectory)) return;
        foreach (var path in Directory.EnumerateFiles(ProfileDirectory, "*.json").OrderBy(path => path))
            _saved.Items.Add(new SavedProfile(path));
        if (_saved.Items.Count > 0) _saved.SelectedIndex = 0;
    }

    private void OpenSaved()
    {
        if (_saved.SelectedItem is not SavedProfile selected)
        {
            MessageBox.Show("No saved profile is available yet.", Text);
            return;
        }
        try
        {
            ShowProfile(ProfileFiles.Load(selected.Path));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Cannot open profile", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveAsNew()
    {
        try
        {
            var saved = ProfileFiles.SaveNew(_profile, ProfileDirectory);
            ShowProfile(saved.Profile);
            RefreshSaved();
            MessageBox.Show($"Saved a new synthetic profile:\n{saved.Path}", Text);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Profile was not saved", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private sealed record SavedProfile(string Path)
    {
        public override string ToString() => System.IO.Path.GetFileName(Path);
    }
}
