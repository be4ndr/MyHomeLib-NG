using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MyHomeLibNG.App.ViewModels;

public sealed class LibraryManagerViewModel : ObservableObject, IDisposable
{
    private readonly MainWindowViewModel _shellViewModel;

    public LibraryManagerViewModel(MainWindowViewModel shellViewModel)
    {
        _shellViewModel = shellViewModel;
        _shellViewModel.PropertyChanged += OnShellViewModelPropertyChanged;
    }

    public ObservableCollection<LibraryProfileItemViewModel> Libraries => _shellViewModel.Libraries;

    public LibraryProfileItemViewModel? SelectedLibrary
    {
        get => _shellViewModel.SelectedLibrary;
        set
        {
            if (_shellViewModel.SelectedLibrary == value)
            {
                return;
            }

            _shellViewModel.SelectedLibrary = value;
            OnPropertyChanged();
        }
    }

    public bool CanTriggerActions => _shellViewModel.CanTriggerActions;
    public bool CanBrowseLibraries => _shellViewModel.CanBrowseLibraries;
    public bool CanRefresh => _shellViewModel.CanRefresh;
    public bool CanScanSelectedLibrary => _shellViewModel.CanScanSelectedLibrary;
    public bool HasSelectedLibrary => _shellViewModel.HasSelectedLibrary;
    public bool HasLibraries => _shellViewModel.HasLibraries;
    public bool ShowEmptyState => !_shellViewModel.HasLibraries && !_shellViewModel.IsBusy;

    public Task ActivateSelectedLibraryAsync()
        => _shellViewModel.OnSelectedLibraryChangedAsync();

    public void Dispose()
    {
        _shellViewModel.PropertyChanged -= OnShellViewModelPropertyChanged;
    }

    private void OnShellViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) ||
            e.PropertyName == nameof(MainWindowViewModel.SelectedLibrary))
        {
            OnPropertyChanged(nameof(SelectedLibrary));
        }

        if (string.IsNullOrEmpty(e.PropertyName) ||
            e.PropertyName == nameof(MainWindowViewModel.CanTriggerActions))
        {
            OnPropertyChanged(nameof(CanTriggerActions));
        }

        if (string.IsNullOrEmpty(e.PropertyName) ||
            e.PropertyName == nameof(MainWindowViewModel.CanBrowseLibraries))
        {
            OnPropertyChanged(nameof(CanBrowseLibraries));
        }

        if (string.IsNullOrEmpty(e.PropertyName) ||
            e.PropertyName == nameof(MainWindowViewModel.CanRefresh))
        {
            OnPropertyChanged(nameof(CanRefresh));
        }

        if (string.IsNullOrEmpty(e.PropertyName) ||
            e.PropertyName == nameof(MainWindowViewModel.CanScanSelectedLibrary))
        {
            OnPropertyChanged(nameof(CanScanSelectedLibrary));
        }

        if (string.IsNullOrEmpty(e.PropertyName) ||
            e.PropertyName == nameof(MainWindowViewModel.HasSelectedLibrary))
        {
            OnPropertyChanged(nameof(HasSelectedLibrary));
        }

        if (string.IsNullOrEmpty(e.PropertyName) ||
            e.PropertyName == nameof(MainWindowViewModel.HasLibraries) ||
            e.PropertyName == nameof(MainWindowViewModel.IsBusy))
        {
            OnPropertyChanged(nameof(HasLibraries));
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }
}
