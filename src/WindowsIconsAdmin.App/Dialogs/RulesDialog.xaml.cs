using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WindowsIconsAdmin.Core.Rules;

namespace WindowsIconsAdmin_App.Dialogs;

public class RuleDisplayItem
{
    public required FolderRule Rule { get; init; }
    public string Name => Rule.Name;
    public string Summary => $"{Rule.Condition}: \"{Rule.Pattern}\" -> {Path.GetFileName(Rule.IconPath)}";
}

public sealed partial class RulesDialog : ContentDialog
{
    private readonly ObservableCollection<RuleDisplayItem> _displayRules = new();
    private readonly List<FolderRule> _rules = new();
    private readonly Func<List<FolderRule>, Task> _onApplyCallback;

    public RulesDialog(IEnumerable<FolderRule> currentRules, Func<List<FolderRule>, Task> onApplyCallback)
    {
        InitializeComponent();
        _onApplyCallback = onApplyCallback;

        foreach (var r in currentRules)
        {
            _rules.Add(r);
            _displayRules.Add(new RuleDisplayItem { Rule = r });
        }

        RulesListView.ItemsSource = _displayRules;
    }

    public IReadOnlyList<FolderRule> Rules => _rules;

    private async void OnBrowseIconClick(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        var hwnd = MainWindow.Current.Hwnd;
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
        picker.FileTypeFilter.Add(".ico");
        picker.FileTypeFilter.Add(".png");

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            IconPathBox.Text = file.Path;
        }
    }

    private void OnAddRuleClick(object sender, RoutedEventArgs e)
    {
        var name = RuleNameBox.Text.Trim();
        var pattern = PatternBox.Text.Trim();
        var iconPath = IconPathBox.Text.Trim();

        if (string.IsNullOrEmpty(name)) name = $"Regla {_rules.Count + 1}";
        if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(iconPath))
        {
            return;
        }

        var condItem = (ComboBoxItem)ConditionBox.SelectedItem;
        var cond = condItem.Tag?.ToString() switch
        {
            "StartsWith" => RuleCondition.StartsWith,
            "EndsWith" => RuleCondition.EndsWith,
            "Regex" => RuleCondition.Regex,
            _ => RuleCondition.Contains
        };

        var rule = new FolderRule(
            Guid.NewGuid().ToString("N"),
            name,
            true,
            cond,
            pattern,
            CaseSensitiveBox.IsChecked ?? false,
            iconPath,
            _rules.Count + 1);

        _rules.Add(rule);
        _displayRules.Add(new RuleDisplayItem { Rule = rule });

        RuleNameBox.Text = string.Empty;
        PatternBox.Text = string.Empty;
        IconPathBox.Text = string.Empty;
    }

    private void OnDeleteRuleClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RuleDisplayItem item })
        {
            _displayRules.Remove(item);
            _rules.Remove(item.Rule);
        }
    }

    private async void OnApplyRulesClick(object sender, RoutedEventArgs e)
    {
        Hide();
        await _onApplyCallback(_rules);
    }
}
