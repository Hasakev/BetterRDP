using BetterRdp.Core;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BetterRdp_App.Dialogs;

/// <summary>Collects a Display Profile: name, mode, and the mode-dependent
/// monitor / resolution / scale fields. Mirrors the PySide6 ProfileDialog.</summary>
public sealed partial class ProfileDialog : ContentDialog
{
    private static readonly (DisplayMode Mode, string Label)[] Modes =
    [
        (DisplayMode.FullscreenMultimon, "Full screen (selected monitors)"),
        (DisplayMode.WindowedFixed, "Windowed (fixed resolution)"),
        (DisplayMode.WindowedDynamic, "Windowed (dynamic resolution)"),
    ];

    private readonly List<CheckBox> _monitorBoxes = [];

    public ProfileDialog()
    {
        InitializeComponent();

        foreach (var (mode, label) in Modes)
            ModeInput.Items.Add(new ComboBoxItem { Content = label, Tag = mode });
        ModeInput.SelectedIndex = 0;

        ScaleInput.Items.Add(new ComboBoxItem { Content = "Default", Tag = null });
        foreach (var s in new[] { 100, 125, 150, 175, 200 })
            ScaleInput.Items.Add(new ComboBoxItem { Content = $"{s}%", Tag = s });
        ScaleInput.SelectedIndex = 0;

        // Enumerate physical displays. The index used here is the OS enumeration order;
        // mstsc's selectedmonitors IDs may differ — calibrate against `mstsc /l` (smoke S2).
        var displays = DisplayArea.FindAll();
        for (int i = 0; i < displays.Count; i++)
        {
            var b = displays[i].OuterBounds;
            var box = new CheckBox { Content = $"{i}: {b.Width}x{b.Height}", Tag = i };
            _monitorBoxes.Add(box);
            MonitorsPanel.Children.Add(box);
        }
        if (_monitorBoxes.Count > 0)
            _monitorBoxes[0].IsChecked = true;

        SyncVisibility();
    }

    /// <summary>Pre-fill all fields from an existing profile and switch to "edit" wording.</summary>
    public void LoadForEdit(DisplayProfile profile)
    {
        Title = "Edit display profile";
        PrimaryButtonText = "Save";
        NameInput.Text = profile.Name;

        for (int i = 0; i < ModeInput.Items.Count; i++)
            if ((DisplayMode)((ComboBoxItem)ModeInput.Items[i]).Tag == profile.Mode)
            {
                ModeInput.SelectedIndex = i;
                break;
            }

        foreach (var box in _monitorBoxes)
            box.IsChecked = profile.Monitors.Contains((int)box.Tag);

        if (profile.Width is int w) WidthInput.Value = w;
        if (profile.Height is int h) HeightInput.Value = h;

        for (int i = 0; i < ScaleInput.Items.Count; i++)
        {
            var tag = ((ComboBoxItem)ScaleInput.Items[i]).Tag;
            if ((tag is int s && profile.ScaleFactor == s) || (tag is null && profile.ScaleFactor is null))
            {
                ScaleInput.SelectedIndex = i;
                break;
            }
        }

        SyncVisibility();
    }

    private DisplayMode CurrentMode => (DisplayMode)((ComboBoxItem)ModeInput.SelectedItem).Tag;

    private void OnModeChanged(object sender, SelectionChangedEventArgs e) => SyncVisibility();

    private void SyncVisibility()
    {
        if (ModeInput.SelectedItem is null)
            return;
        MonitorsRow.Visibility = CurrentMode == DisplayMode.FullscreenMultimon ? Visibility.Visible : Visibility.Collapsed;
        ResolutionRow.Visibility = CurrentMode == DisplayMode.WindowedFixed ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>The entered Display Profile, or null if the name is blank.</summary>
    public DisplayProfile? Result
    {
        get
        {
            var name = NameInput.Text.Trim();
            if (name.Length == 0)
                return null;

            var mode = CurrentMode;
            var monitors = mode == DisplayMode.FullscreenMultimon
                ? _monitorBoxes.Where(b => b.IsChecked == true).Select(b => (int)b.Tag).ToList()
                : [];
            int? scale = ((ComboBoxItem)ScaleInput.SelectedItem).Tag is int s ? s : null;

            return new DisplayProfile
            {
                Name = name,
                Mode = mode,
                Monitors = monitors,
                Width = mode == DisplayMode.WindowedFixed ? (int)WidthInput.Value : null,
                Height = mode == DisplayMode.WindowedFixed ? (int)HeightInput.Value : null,
                ScaleFactor = scale,
            };
        }
    }
}
