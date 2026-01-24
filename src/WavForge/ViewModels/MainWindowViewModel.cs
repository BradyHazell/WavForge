using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WavForge.Ffmpeg;
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
    private readonly UpdateService _updateService;
    private readonly ISettingsService _settingsService;
    private readonly IUserPromptService _prompter;
    
    private readonly FfmpegBootstrapper _ffmpegBootstrapper;
    private readonly IFfmpegRunner _ffmpeg;
    private readonly IFfprobeService _ffprobe;
    private CancellationTokenSource? _cts;
    
    [ObservableProperty] private string? _inputPath;
    [ObservableProperty] private string? _outputPath;
    
    [ObservableProperty] private bool _isBusy;
    
    [ObservableProperty] private double _progress; // 0..1
    [ObservableProperty] private string? _progressText;
    [ObservableProperty] private string? _statusMessage;

    [ObservableProperty] private bool _isError;

    [ObservableProperty] private bool _isUpdateReadyBannerVisible;
    [ObservableProperty] private string? _updateReadyText;

    // Derived enablement flags for the UI
    public bool CanConvert => !IsBusy && IsInputValid() && IsOutputValid();
    public bool CanCancel => IsBusy;
    public bool CanBrowse => !IsBusy;

    public bool RevealFileOnCompletion
    {
        get => _settingsService.Settings.RevealFileOnCompletion;
        set
        {
            if (_settingsService.Settings.RevealFileOnCompletion == value)
            {
                return;
            }

            _settingsService.Settings.RevealFileOnCompletion = value;
            _settingsService.Save();
            OnPropertyChanged();
        }
    }
    
    public bool ConfirmBeforeOverwrite
    {
        get => _settingsService.Settings.ConfirmBeforeOverwrite;
        set
        {
            if (_settingsService.Settings.ConfirmBeforeOverwrite == value)
            {
                return;
            }

            _settingsService.Settings.ConfirmBeforeOverwrite = value;
            _settingsService.Save();
            OnPropertyChanged();
        }
    }
    
    public MainWindowViewModel(IFileDialogService fileDialogService, IWindowProvider windowProvider, FfmpegBootstrapper ffmpegBootstrapper, IFfmpegRunner ffmpeg, IFfprobeService ffprobe, UpdateService updateService, ISettingsService settingsService, IUserPromptService prompter)
    {
        _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
        _windowProvider = windowProvider;
        _ffmpegBootstrapper = ffmpegBootstrapper;
        _ffmpeg = ffmpeg;
        _ffprobe = ffprobe;
        _updateService = updateService;
        _settingsService = settingsService;
        _prompter = prompter;
        ProgressText = "Idle";
        StatusMessage = "Select an input WAV and output path.";

        _ = StartUpdateCheckAsync();
    }

    private async Task StartUpdateCheckAsync()
    {
        try
        {
            if (_updateService.IsUpdatePendingRestart)
            {
                ShowUpdateReady();
                return;
            }
            
            bool ready = await _updateService.CheckAndDownloadInBackgroundAsync();
            if (ready)
            {
                ShowUpdateReady();
            }
        }
        catch
        {
            // ignored
        }
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

            var installProgress = new Progress<FfmpegInstallProgress>(p =>
            {
                ProgressText = $"Installing: {p.Stage}";

                if (p.Percent.HasValue)
                {
                    Progress = p.Percent.Value;
                }
            });
            
            // 1) Ensure ffmpeg exists
            string? ffmpegPath = await _ffmpegBootstrapper.EnsureFfmpegAsync(installProgress, _cts.Token);
            if (ffmpegPath is null)
            {
                IsError = true;
                StatusMessage = "FFmpeg is required to continue.";
                return;
            }

            // 2) Try locate ffprobe next to ffmpeg (Windows installer places it there)
            string? ffprobePath = null;
            string? dir = Path.GetDirectoryName(ffmpegPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                string candidate = Path.Combine(dir, "ffprobe.exe");
                if (File.Exists(candidate))
                {
                    ffprobePath = candidate;
                }
            }

            // 3) Get duration (optional)
            double? durationSeconds = null;
            if (ffprobePath is not null)
            {
                ProgressText = "Analysing audio…";
                durationSeconds = await _ffprobe.GetDurationInSecondsAsync(ffprobePath, InputPath!, _cts.Token);
            }

            // 4) Convert
            var ffProgress = new Progress<FfmpegConversionProgress>(p =>
            {
                if (p.Percent.HasValue)
                {
                    Progress = p.Percent.Value;
                }

    #pragma warning disable IDE0045
                if (p.Processed is not null && durationSeconds.HasValue)
    #pragma warning restore IDE0045
                {
                    ProgressText = $"Processing: {p.Processed:hh\\:mm\\:ss} / {TimeSpan.FromSeconds(durationSeconds.Value):hh\\:mm\\:ss}";
                }
                else
                {
                    ProgressText = p.Processed is not null ? $"Processing: {p.Processed:hh\\:mm\\:ss}" : p.Stage;
                }
            });

            if (File.Exists(OutputPath) && ConfirmBeforeOverwrite)
            {
                bool overrideConfirm = await _prompter.ConfirmAsync("Output file already exists. Overwrite?", "Confirm overwrite");
                if (!overrideConfirm)
                {
                    StatusMessage = "Cancelled";
                    ProgressText = "Idle";
                    Progress = 0;
                    return;
                }
            }

            bool ok = await _ffmpeg.ConvertWav24To16Async(
                ffmpegPath,
                InputPath!,
                OutputPath!,
                durationSeconds,
                ffProgress,
                _cts.Token);

            if (!ok)
            {
                if (_cts.Token.IsCancellationRequested)
                {
                    StatusMessage = "Cancelled";
                    ProgressText = "Idle";
                    Progress = 0;
                    return;
                }

                IsError = true;
                StatusMessage = "Conversion failed. Check FFmpeg output for details.";
                ProgressText = "Failed";
                Progress = 0;
                return;
            }

            StatusMessage = "✓ Conversion complete";
            ProgressText = "Done";
            Progress = 1;

            if (RevealFileOnCompletion)
            {
                FileReveal.Reveal(OutputPath!);
            }
        }
        catch (OperationCanceledException)
        {
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
    
    private void ShowUpdateReady()
    {
        IsUpdateReadyBannerVisible = true;
        UpdateReadyText = "Update ready — restart to apply.";
        OnPropertyChanged(nameof(IsUpdateReadyBannerVisible));
        OnPropertyChanged(nameof(UpdateReadyText));
        RestartToApplyUpdateCommand.NotifyCanExecuteChanged();
    }

    
    [RelayCommand(CanExecute = nameof(IsUpdateReadyBannerVisible))]
    private void RestartToApplyUpdate()
    {
        _updateService.RestartToApplyUpdate();
        
        Window? window = _windowProvider.GetMainWindow();
        window?.Close();
    }

    
    [RelayCommand]
    private void DismissUpdateBanner()
    {
        IsUpdateReadyBannerVisible = false;
        OnPropertyChanged(nameof(IsUpdateReadyBannerVisible));
        RestartToApplyUpdateCommand.NotifyCanExecuteChanged();
    }
}
