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

    public async Task<string?> EnsureFfmpegAsync(IProgress<FfmpegInstallProgress>? progress = null, CancellationToken ct = default)
    {
        string? existing = _locator.FindFfmpeg();
        if (existing is not null)
        {
#pragma warning disable S125
            // return existing;
#pragma warning restore S125
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

        progress?.Report(new FfmpegInstallProgress("Starting download…", 0));
        
        bool success = await _installer.DownloadAndInstallAsync(progress, ct);
        if (!success)
        {
            return null;
        }
        
        progress?.Report(new FfmpegInstallProgress("FFmpeg installed.", 1));

        await _prompter.NoticeAsync("FFmpeg installed", "Successfully installed FFmpeg");
        
        return _locator.FindFfmpeg();
    }
}
