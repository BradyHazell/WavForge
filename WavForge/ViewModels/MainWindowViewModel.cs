using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WavForge.Services;

namespace WavForge.ViewModels;

#pragma warning disable CA1001 // CTS is disposed after usage
#pragma warning disable CA1852 // Can't be made sealed because the ObservableObject base 
internal partial class MainWindowViewModel : ViewModelBase
#pragma warning restore CA1852
#pragma warning restore CA1001
{
    private readonly IFileDialogService _fileDialogService;
    private readonly IWindowProvider _windowProvider;
    private CancellationTokenSource? _cts;
    
    [ObservableProperty] private string? _inputPath;
    [ObservableProperty] private string? _outputPath;
    
    [ObservableProperty] private bool _isBusy;
    
    [ObservableProperty] private double _progress; // 0..1
    [ObservableProperty] private string? _progressText;
    [ObservableProperty] private string? _statusMessage;

    [ObservableProperty] private bool _isError;

    // Derived enablement flags for the UI
    public bool CanConvert => !IsBusy && IsInputValid() && IsOutputValid();
    public bool CanCancel => IsBusy;
    public bool CanBrowse => !IsBusy;

    public MainWindowViewModel(IFileDialogService fileDialogService, IWindowProvider windowProvider)
    {
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        _windowProvider = windowProvider;
        ProgressText = "Idle";
        StatusMessage = "Select an input WAV and output path.";
    }

    partial void OnIsBusyChanged(bool value) => NotifyCommandStates();
    partial void OnInputPathChanged(string? value) => NotifyCommandStates();
    partial void OnOutputPathChanged(string? value) => NotifyCommandStates();
    
    private void NotifyCommandStates()
    {
        OnPropertyChanged(nameof(CanConvert));
        OnPropertyChanged(nameof(CanCancel));
        ConvertCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    private bool IsInputValid()
        => !string.IsNullOrWhiteSpace(InputPath) && File.Exists(InputPath);

    private bool IsOutputValid()
    {
        return !string.IsNullOrWhiteSpace(OutputPath);
    }
    
    [RelayCommand(CanExecute = nameof(CanBrowse))]
    private async Task BrowseInputAsync()
    {
        Window? owner = _windowProvider.GetMainWindow();
        if (owner is null)
        {
            return;
        }

        IsError = false;
        StatusMessage = null;

        string? picked = await _fileDialogService.PickInputWavAsync(owner);
        if (string.IsNullOrWhiteSpace(picked))
        {
            return;
        }

        InputPath = picked;

        // Handy default: suggest an output next to the input.
        string? dir = Path.GetDirectoryName(picked);
        string name = Path.GetFileNameWithoutExtension(picked);
        OutputPath = Path.Combine(dir ?? ".", $"{name}-16bit.wav");
    }

    [RelayCommand(CanExecute = nameof(CanBrowse))]
    private async Task BrowseOutputAsync()
    {
        Window? owner = _windowProvider.GetMainWindow();
        if (owner is null)
        {
            return;
        }

        
        IsError = false;
        StatusMessage = null;

#pragma warning disable S3358
        string suggested = !string.IsNullOrWhiteSpace(OutputPath) ? Path.GetFileName(OutputPath) :
            !string.IsNullOrWhiteSpace(InputPath) ? $"{Path.GetFileNameWithoutExtension(InputPath)}-16bit.wav" :
            "output-16bit.wav";
#pragma warning restore S3358

        string? picked = await _fileDialogService.PickOutputWavAsync(owner, suggested);
        if (string.IsNullOrWhiteSpace(picked))
        {
            return;
        }

        OutputPath = picked;
    }

    [RelayCommand(CanExecute = nameof(CanConvert))]
    private async Task ConvertAsync()
    {
        IsError = false;
        StatusMessage = null;

        _cts = new CancellationTokenSource();

        try
        {
            IsBusy = true;
            Progress = 0;
            ProgressText = "Starting…";
            StatusMessage = "Converting…";

            // Simulate a conversion that takes ~3 seconds, updating UI.
            const int steps = 30;
            for (int i = 0; i < steps; i++)
            {
                _cts.Token.ThrowIfCancellationRequested();

                await Task.Delay(100, _cts.Token);

                Progress = (i + 1) / (double)steps;
                int pct = (int)Math.Round(Progress * 100);
                ProgressText = $"Processing… {pct}%";
            }

            StatusMessage = "✓ Conversion complete";
            ProgressText = "Done";
        }
        catch (OperationCanceledException)
        {
            IsError = false;
            StatusMessage = "Cancelled";
            ProgressText = "Idle";
            Progress = 0;
        }
        catch (Exception ex)
        {
            IsError = true;
            StatusMessage = $"Error: {ex.Message}";
            ProgressText = "Failed";
            Progress = 0;
        }
        finally
        {
            IsBusy = false;

            _cts.Dispose();
            _cts = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _cts?.Cancel();
    }
}
