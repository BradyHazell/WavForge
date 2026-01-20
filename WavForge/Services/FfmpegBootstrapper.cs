using WavForge.Ffmpeg;

namespace WavForge.Services;

internal sealed class FfmpegBootstrapper
{
    private readonly IFfmpegLocator _locator;
    private readonly IFfmpegInstaller _installer;
    private readonly IUserPromptService _prompter;

    public FfmpegBootstrapper(
        IFfmpegLocator locator,
        IFfmpegInstaller installer,
        IUserPromptService prompter)
    {
        _locator = locator;
        _installer = installer;
        _prompter = prompter;
    }

    public async Task<string?> EnsureFfmpegAsync()
    {
        string? existing = _locator.FindFfmpeg();
        if (existing is not null)
        {
            return existing;
        }

        bool consent = await _prompter.ConfirmAsync(
            title: "FFmpeg required",
            message:
            "FFmpeg is required to convert audio files, but it was not found on your system.\n\n" +
            "Would you like WavForge to download it automatically?",
            confirmText: "Download",
            cancelText: "Cancel");

        if (!consent)
        {
            return null;
        }

        bool success = await _installer.DownloadAndInstallAsync();
        if (!success)
        {
            return null;
        }

        return _locator.FindFfmpeg();
    }
}
