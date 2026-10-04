using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using WindowsIconsAdmin.Core.Rules;

namespace WindowsIconsAdmin_App.Dialogs;

public sealed class RuleDisplayItem : INotifyPropertyChanged
{
    private FolderRule _rule;
    private int _matchCount;
    private int _totalFolders;

    public RuleDisplayItem(FolderRule rule, int matchCount = 0, int totalFolders = 0)
    {
        _rule = rule;
        _matchCount = matchCount;
        _totalFolders = totalFolders;
    }

    public FolderRule Rule
    {
        get => _rule;
        set
        {
            _rule = value;
            NotifyAll();
        }
    }

    public bool Enabled
    {
        get => _rule.Enabled;
        set
        {
            if (_rule.Enabled != value)
            {
                _rule = _rule with { Enabled = value };
                NotifyAll();
            }
        }
    }

    public string Name => _rule.Name;
    public string PriorityBadge => $"#{_rule.Priority}";

    public string Summary
    {
        get
        {
            var targetLabel = _rule.MatchTarget == RuleMatchTarget.FullPath ? "Ruta" : "Nombre";
            var condLabel = _rule.Condition switch
            {
                RuleCondition.Contains => "contiene",
                RuleCondition.StartsWith => "empieza con",
                RuleCondition.EndsWith => "termina con",
                RuleCondition.Equals => "es igual a",
                RuleCondition.Wildcard => "comodín",
                RuleCondition.Regex => "regex",
                _ => "coincide con"
            };
            var iconName = string.IsNullOrWhiteSpace(_rule.IconPath)
                ? "(sin icono)"
                : Path.GetFileName(_rule.IconPath);

            return $"{targetLabel} {condLabel} \"{_rule.Pattern}\" → {iconName}";
        }
    }

    public string MatchCountLabel
    {
        get
        {
            if (!_rule.Enabled) return "Regla desactivada";
            if (_totalFolders == 0) return "Activa";
            return _matchCount == 1
                ? "Coincide con 1 carpeta de la lista"
                : $"Coincide con {_matchCount} de {_totalFolders} carpetas";
        }
    }

    public void UpdateMatchStats(int matchCount, int totalFolders)
    {
        _matchCount = matchCount;
        _totalFolders = totalFolders;
        OnPropertyChanged(nameof(MatchCountLabel));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void NotifyAll()
    {
        OnPropertyChanged(nameof(Rule));
        OnPropertyChanged(nameof(Enabled));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(PriorityBadge));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(MatchCountLabel));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed partial class RulesDialog : ContentDialog
{
    private readonly ObservableCollection<RuleDisplayItem> _displayRules = new();
    private readonly List<string> _allFolderPaths;
    private readonly List<string> _selectedFolderPaths;
    private readonly Action<IReadOnlyList<FolderRule>> _onRulesSaved;
    private readonly Func<IReadOnlyList<FolderRule>, bool, Task> _onApplyCallback;

    private string? _editingRuleId;
    private bool _suppressFormEvents;

    public RulesDialog(
        IEnumerable<FolderRule> currentRules,
        IEnumerable<string> allFolderPaths,
        IEnumerable<string> selectedFolderPaths,
        Action<IReadOnlyList<FolderRule>> onRulesSaved,
        Func<IReadOnlyList<FolderRule>, bool, Task> onApplyCallback)
    {
        InitializeComponent();

        _allFolderPaths = allFolderPaths.ToList();
        _selectedFolderPaths = selectedFolderPaths.ToList();
        _onRulesSaved = onRulesSaved;
        _onApplyCallback = onApplyCallback;

        var ordered = currentRules
            .OrderBy(r => r.Priority)
            .Select((r, idx) => r with { Priority = idx + 1 })
            .ToList();

        foreach (var r in ordered)
        {
            _displayRules.Add(new RuleDisplayItem(r));
        }

        RulesListView.ItemsSource = _displayRules;
        RefreshRulesStateAndSave(persist: false);
        UpdateConditionHintAndPlaceholder();
        UpdateLivePreview();
    }

    public IReadOnlyList<FolderRule> Rules => _displayRules.Select(d => d.Rule).ToList();

    private IReadOnlyList<string> CurrentScopePaths =>
        (ApplyScopeBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "SelectedOnly"
            ? _selectedFolderPaths
            : _allFolderPaths;

    #region List Management & Ordering

    private void RefreshRulesStateAndSave(bool persist = true)
    {
        for (var i = 0; i < _displayRules.Count; i++)
        {
            var item = _displayRules[i];
            if (item.Rule.Priority != i + 1)
            {
                item.Rule = item.Rule with { Priority = i + 1 };
            }
            else
            {
                item.NotifyAll();
            }
        }

        if (RulesCountHeader != null)
        {
            RulesCountHeader.Text = $"Reglas guardadas ({_displayRules.Count})";
        }

        if (EmptyRulesPlaceholder != null)
        {
            EmptyRulesPlaceholder.Visibility = _displayRules.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        UpdatePerRuleMatchCounts();
        UpdateTotalMatchSummary();
        UpdateToolbarButtonStates();

        if (persist)
        {
            _onRulesSaved(Rules);
        }
    }

    private void UpdatePerRuleMatchCounts()
    {
        var paths = CurrentScopePaths;
        foreach (var item in _displayRules)
        {
            if (!item.Rule.Enabled)
            {
                item.UpdateMatchStats(0, paths.Count);
                continue;
            }

            try
            {
                var matches = RuleEngine.Evaluate(paths, [item.Rule]);
                var matchedCount = matches.Count(m => m.Rule != null);
                item.UpdateMatchStats(matchedCount, paths.Count);
            }
            catch
            {
                item.UpdateMatchStats(0, paths.Count);
            }
        }
    }

    private void UpdateTotalMatchSummary()
    {
        if (TotalMatchSummaryText == null || TotalMatchSubText == null) return;

        var paths = CurrentScopePaths;
        var activeRules = Rules.Where(r => r.Enabled).ToList();

        if (activeRules.Count == 0)
        {
            TotalMatchSummaryText.Text = "No hay reglas activas para aplicar";
            TotalMatchSubText.Text = "Crea o activa al menos una regla para asignar iconos automáticamente.";
            return;
        }

        if (paths.Count == 0)
        {
            TotalMatchSummaryText.Text = $"{activeRules.Count} reglas activas listas";
            TotalMatchSubText.Text = "Agrega carpetas en la pantalla principal para aplicarles estas reglas.";
            return;
        }

        try
        {
            var eval = RuleEngine.Evaluate(paths, activeRules);
            var matched = eval.Count(m => m.Rule != null);
            TotalMatchSummaryText.Text = $"Coincidencia total: {matched} de {paths.Count} carpetas recibirán icono";
            TotalMatchSubText.Text = $"Evaluando {activeRules.Count} reglas activas en orden de prioridad (#1 a #{_displayRules.Count}).";
        }
        catch (Exception ex)
        {
            TotalMatchSummaryText.Text = "Revisa las expresiones regulares de tus reglas";
            TotalMatchSubText.Text = ex.Message;
        }
    }

    private void UpdateToolbarButtonStates()
    {
        if (RulesListView == null) return;
        var idx = RulesListView.SelectedIndex;
        var hasSelection = idx >= 0 && idx < _displayRules.Count;

        if (MoveUpButton != null) MoveUpButton.IsEnabled = hasSelection && idx > 0;
        if (MoveDownButton != null) MoveDownButton.IsEnabled = hasSelection && idx < _displayRules.Count - 1;
        if (DuplicateRuleButton != null) DuplicateRuleButton.IsEnabled = hasSelection;
        if (DeleteRuleButton != null) DeleteRuleButton.IsEnabled = hasSelection;
    }

    private void OnRulesListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateToolbarButtonStates();
        if (RulesListView.SelectedItem is RuleDisplayItem selected)
        {
            LoadRuleIntoEditor(selected.Rule);
        }
    }

    private void OnRuleEnabledCheckBoxClick(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: RuleDisplayItem item } cb)
        {
            item.Enabled = cb.IsChecked == true;
            RefreshRulesStateAndSave(persist: true);
        }
    }

    private void OnMoveUpClick(object sender, RoutedEventArgs e)
    {
        var idx = RulesListView.SelectedIndex;
        if (idx <= 0) return;

        var item = _displayRules[idx];
        _displayRules.RemoveAt(idx);
        _displayRules.Insert(idx - 1, item);
        RulesListView.SelectedIndex = idx - 1;
        RefreshRulesStateAndSave(persist: true);
    }

    private void OnMoveDownClick(object sender, RoutedEventArgs e)
    {
        var idx = RulesListView.SelectedIndex;
        if (idx < 0 || idx >= _displayRules.Count - 1) return;

        var item = _displayRules[idx];
        _displayRules.RemoveAt(idx);
        _displayRules.Insert(idx + 1, item);
        RulesListView.SelectedIndex = idx + 1;
        RefreshRulesStateAndSave(persist: true);
    }

    private void OnDuplicateRuleClick(object sender, RoutedEventArgs e)
    {
        if (RulesListView.SelectedItem is not RuleDisplayItem selected) return;

        var copy = selected.Rule with
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = $"{selected.Rule.Name} (copia)",
            Priority = _displayRules.Count + 1
        };

        var newItem = new RuleDisplayItem(copy);
        _displayRules.Add(newItem);
        RulesListView.SelectedItem = newItem;
        RefreshRulesStateAndSave(persist: true);
        ShowDialogInfo($"Regla '{copy.Name}' duplicada.", InfoBarSeverity.Informational);
    }

    private void OnDeleteSelectedRuleClick(object sender, RoutedEventArgs e)
    {
        if (RulesListView.SelectedItem is RuleDisplayItem selected)
        {
            DeleteRuleItem(selected);
        }
    }

    private void OnDeleteRuleClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RuleDisplayItem item })
        {
            DeleteRuleItem(item);
        }
    }

    private void DeleteRuleItem(RuleDisplayItem item)
    {
        var wasEditingThis = _editingRuleId == item.Rule.Id;
        _displayRules.Remove(item);
        if (wasEditingThis)
        {
            ResetEditorToNewRule();
        }
        RefreshRulesStateAndSave(persist: true);
    }

    #endregion

    #region Editor Form & Live Preview

    private void LoadRuleIntoEditor(FolderRule rule)
    {
        _suppressFormEvents = true;
        try
        {
            _editingRuleId = rule.Id;
            EditorTitleText.Text = $"Editando regla: {rule.Name}";
            SaveRuleButton.Content = "Actualizar regla";
            RuleNameBox.Text = rule.Name;
            PatternBox.Text = rule.Pattern;
            CaseSensitiveBox.IsChecked = rule.CaseSensitive;
            IconPathBox.Text = rule.IconPath;

            TargetBox.SelectedIndex = rule.MatchTarget == RuleMatchTarget.FullPath ? 1 : 0;
            ConditionBox.SelectedIndex = rule.Condition switch
            {
                RuleCondition.Contains => 0,
                RuleCondition.StartsWith => 1,
                RuleCondition.EndsWith => 2,
                RuleCondition.Equals => 3,
                RuleCondition.Wildcard => 4,
                RuleCondition.Regex => 5,
                _ => 0
            };

            _ = LoadIconPreviewAsync(rule.IconPath);
        }
        finally
        {
            _suppressFormEvents = false;
        }

        UpdateConditionHintAndPlaceholder();
        UpdateLivePreview();
    }

    private void ResetEditorToNewRule()
    {
        _suppressFormEvents = true;
        try
        {
            _editingRuleId = null;
            RulesListView.SelectedIndex = -1;
            EditorTitleText.Text = "Crear nueva regla";
            SaveRuleButton.Content = "Guardar regla";
            RuleNameBox.Text = string.Empty;
            PatternBox.Text = string.Empty;
            CaseSensitiveBox.IsChecked = false;
            IconPathBox.Text = string.Empty;
            TargetBox.SelectedIndex = 0;
            ConditionBox.SelectedIndex = 0;
            RuleIconPreview.Source = null;
        }
        finally
        {
            _suppressFormEvents = false;
        }

        UpdateConditionHintAndPlaceholder();
        UpdateLivePreview();
    }

    private void OnNewRuleClick(object sender, RoutedEventArgs e)
    {
        ResetEditorToNewRule();
    }

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
            await LoadIconPreviewAsync(file.Path);
            UpdateLivePreview();
        }
    }

    private async Task LoadIconPreviewAsync(string? iconPath)
    {
        if (RuleIconPreview == null) return;
        if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath))
        {
            RuleIconPreview.Source = null;
            return;
        }

        try
        {
            var bytes = await File.ReadAllBytesAsync(iconPath);
            using var ms = new MemoryStream(bytes);
            var ras = ms.AsRandomAccessStream();
            var bmp = new BitmapImage();
            await bmp.SetSourceAsync(ras);
            RuleIconPreview.Source = bmp;
        }
        catch
        {
            RuleIconPreview.Source = null;
        }
    }

    private RuleCondition SelectedCondition =>
        (ConditionBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() switch
        {
            "StartsWith" => RuleCondition.StartsWith,
            "EndsWith" => RuleCondition.EndsWith,
            "Equals" => RuleCondition.Equals,
            "Wildcard" => RuleCondition.Wildcard,
            "Regex" => RuleCondition.Regex,
            _ => RuleCondition.Contains
        };

    private RuleMatchTarget SelectedMatchTarget =>
        (TargetBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "FullPath"
            ? RuleMatchTarget.FullPath
            : RuleMatchTarget.FolderName;

    private void OnConditionSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressFormEvents) return;
        UpdateConditionHintAndPlaceholder();
        UpdateLivePreview();
    }

    private void OnFormSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressFormEvents) return;
        UpdateConditionHintAndPlaceholder();
        UpdateLivePreview();
    }

    private void OnFormFieldChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressFormEvents) return;
        UpdateLivePreview();
    }

    private void OnCaseSensitiveClick(object sender, RoutedEventArgs e)
    {
        if (_suppressFormEvents) return;
        UpdateLivePreview();
    }

    private void OnApplyScopeChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdatePerRuleMatchCounts();
        UpdateTotalMatchSummary();
        UpdateLivePreview();
    }

    private void UpdateConditionHintAndPlaceholder()
    {
        if (PatternBox == null || ConditionHintText == null) return;

        var targetText = SelectedMatchTarget == RuleMatchTarget.FullPath ? "la ruta completa" : "el nombre de la carpeta";
        switch (SelectedCondition)
        {
            case RuleCondition.Contains:
                PatternBox.PlaceholderText = "Ej. proyecto, fotos, backup";
                ConditionHintText.Text = $"Coincide si {targetText} contiene este texto en cualquier parte.";
                break;
            case RuleCondition.StartsWith:
                PatternBox.PlaceholderText = "Ej. 2026-, Cliente_";
                ConditionHintText.Text = $"Coincide si {targetText} empieza exactamente con este texto.";
                break;
            case RuleCondition.EndsWith:
                PatternBox.PlaceholderText = "Ej. -final, _old, .git";
                ConditionHintText.Text = $"Coincide si {targetText} termina con este texto.";
                break;
            case RuleCondition.Equals:
                PatternBox.PlaceholderText = "Ej. Documentos, node_modules";
                ConditionHintText.Text = $"Coincide solo si {targetText} es exactamente igual al patrón.";
                break;
            case RuleCondition.Wildcard:
                PatternBox.PlaceholderText = "Ej. 202*-proyecto* o Cliente_??";
                ConditionHintText.Text = $"Usa '*' para cualquier texto y '?' para un solo carácter sobre {targetText}.";
                break;
            case RuleCondition.Regex:
                PatternBox.PlaceholderText = @"Ej. ^\d{4}-[A-Za-z]+$";
                ConditionHintText.Text = $"Evalúa una expresión regular .NET sobre {targetText} (con protección anti-bloqueo).";
                break;
        }
    }

    private void UpdateLivePreview()
    {
        if (LivePreviewStatusText == null || PatternBox == null) return;

        var pattern = PatternBox.Text.Trim();
        var condition = SelectedCondition;
        var caseSensitive = CaseSensitiveBox?.IsChecked == true;
        var target = SelectedMatchTarget;

        if (string.IsNullOrEmpty(pattern))
        {
            LivePreviewStatusText.Text = "Escribe un patrón arriba para ver al instante qué carpetas coinciden.";
            if (SampleTestResultBadge != null) SampleTestResultBadge.Text = string.Empty;
            return;
        }

        if (!RuleEngine.TryValidatePattern(condition, pattern, caseSensitive, out var validationError))
        {
            LivePreviewStatusText.Text = $"⚠ {validationError}";
            if (SampleTestResultBadge != null) SampleTestResultBadge.Text = "Patrón inválido";
            return;
        }

        var draftRule = new FolderRule(
            "preview",
            "Preview",
            true,
            condition,
            pattern,
            caseSensitive,
            IconPathBox?.Text ?? string.Empty,
            1,
            target);

        // Evaluate against sample test input if provided
        if (SampleTestResultBadge != null && SampleTestInputBox != null)
        {
            var sampleText = SampleTestInputBox.Text.Trim();
            if (string.IsNullOrEmpty(sampleText))
            {
                SampleTestResultBadge.Text = string.Empty;
            }
            else
            {
                var sampleEval = RuleEngine.Evaluate([sampleText], [draftRule]);
                SampleTestResultBadge.Text = sampleEval[0].Rule != null ? "✓ Coincide" : "✕ No coincide";
            }
        }

        // Evaluate against loaded folders
        var scopePaths = CurrentScopePaths;
        if (scopePaths.Count == 0)
        {
            LivePreviewStatusText.Text = "Patrón válido. (No hay carpetas cargadas en la lista principal; usa el campo de prueba de abajo para verificar).";
            return;
        }

        var matches = RuleEngine.Evaluate(scopePaths, [draftRule])
            .Where(m => m.Rule != null)
            .Select(m => RuleEngine.ExtractFolderName(m.FolderPath))
            .ToList();

        if (matches.Count == 0)
        {
            LivePreviewStatusText.Text = $"Coincide con 0 de {scopePaths.Count} carpetas en la lista.";
        }
        else
        {
            var previewNames = string.Join(", ", matches.Take(5));
            var suffix = matches.Count > 5 ? $" y {matches.Count - 5} más..." : string.Empty;
            LivePreviewStatusText.Text = $"✓ Coincide con {matches.Count} de {scopePaths.Count} carpetas: {previewNames}{suffix}";
        }
    }

    private bool TrySaveCurrentFormAsRule(bool showFeedback = true)
    {
        var name = RuleNameBox.Text.Trim();
        var pattern = PatternBox.Text.Trim();
        var iconPath = IconPathBox.Text.Trim();
        var condition = SelectedCondition;
        var caseSensitive = CaseSensitiveBox.IsChecked == true;
        var target = SelectedMatchTarget;

        if (!RuleEngine.TryValidatePattern(condition, pattern, caseSensitive, out var error))
        {
            if (showFeedback) ShowDialogInfo(error ?? "El patrón es inválido.", InfoBarSeverity.Warning);
            return false;
        }

        if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath))
        {
            if (showFeedback) ShowDialogInfo("Selecciona un archivo de icono (.ico o .png) válido para la regla.", InfoBarSeverity.Warning);
            return false;
        }

        if (string.IsNullOrEmpty(name))
        {
            name = $"Carpetas '{pattern}'";
        }

        if (_editingRuleId != null)
        {
            var existingIdx = _displayRules.ToList().FindIndex(d => d.Rule.Id == _editingRuleId);
            if (existingIdx >= 0)
            {
                var current = _displayRules[existingIdx].Rule;
                var updated = new FolderRule(
                    current.Id,
                    name,
                    current.Enabled,
                    condition,
                    pattern,
                    caseSensitive,
                    iconPath,
                    current.Priority,
                    target);

                _displayRules[existingIdx].Rule = updated;
                RefreshRulesStateAndSave(persist: true);
                if (showFeedback) ShowDialogInfo($"Regla '{name}' actualizada y guardada.", InfoBarSeverity.Success);
                ResetEditorToNewRule();
                return true;
            }
        }

        var newRule = new FolderRule(
            Guid.NewGuid().ToString("N"),
            name,
            true,
            condition,
            pattern,
            caseSensitive,
            iconPath,
            _displayRules.Count + 1,
            target);

        _displayRules.Add(new RuleDisplayItem(newRule));
        RefreshRulesStateAndSave(persist: true);
        if (showFeedback) ShowDialogInfo($"Regla '{name}' agregada y guardada.", InfoBarSeverity.Success);
        ResetEditorToNewRule();
        return true;
    }

    private void OnSaveRuleClick(object sender, RoutedEventArgs e)
    {
        TrySaveCurrentFormAsRule(showFeedback: true);
    }

    private async void OnApplyRulesClick(object sender, RoutedEventArgs e)
    {
        // Auto-save pending form inputs if the user filled out a rule and clicked "Apply rules now" directly
        var hasPendingInput = !string.IsNullOrWhiteSpace(PatternBox.Text) && !string.IsNullOrWhiteSpace(IconPathBox.Text);
        if (hasPendingInput)
        {
            if (!TrySaveCurrentFormAsRule(showFeedback: false))
            {
                return;
            }
        }

        var activeRules = Rules.Where(r => r.Enabled).ToList();
        if (activeRules.Count == 0)
        {
            ShowDialogInfo("No hay ninguna regla activa para aplicar. Crea o activa una regla primero.", InfoBarSeverity.Warning);
            return;
        }

        var selectedOnly = (ApplyScopeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "SelectedOnly";
        Hide();
        await _onApplyCallback(Rules, selectedOnly);
    }

    private void ShowDialogInfo(string message, InfoBarSeverity severity)
    {
        RulesInfoBar.Message = message;
        RulesInfoBar.Severity = severity;
        RulesInfoBar.IsOpen = true;
    }

    #endregion
}
