using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WindowsIconsAdmin_App.ViewModels;

public class FolderItemViewModel : INotifyPropertyChanged
{
    private string _status = "Pendiente";
    private bool _hasGitWarning;
    private bool _isRemoteWarning;
    private bool _isSelected = true;

    public FolderItemViewModel(string fullPath)
    {
        FullPath = fullPath;
        FolderName = Path.GetFileName(fullPath.TrimEnd('\\', '/'));
        if (string.IsNullOrEmpty(FolderName))
        {
            FolderName = fullPath;
        }

        HasGitWarning = Directory.Exists(Path.Combine(fullPath, ".git"));
        IsRemoteWarning = fullPath.StartsWith(@"\\") || fullPath.StartsWith("//");
    }

    public string FullPath { get; }
    public string FolderName { get; }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public bool HasGitWarning
    {
        get => _hasGitWarning;
        set => SetField(ref _hasGitWarning, value);
    }

    public bool IsRemoteWarning
    {
        get => _isRemoteWarning;
        set => SetField(ref _isRemoteWarning, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public string WarningText
    {
        get
        {
            if (HasGitWarning && IsRemoteWarning) return "Aviso: Repositorio Git y unidad de red detectados";
            if (HasGitWarning) return "Aviso: Repositorio Git detectado (.git)";
            if (IsRemoteWarning) return "Aviso: Ruta de red / remota (Windows puede ignorar desktop.ini)";
            return string.Empty;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
