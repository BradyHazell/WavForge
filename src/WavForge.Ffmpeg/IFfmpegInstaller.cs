namespace WavForge.Ffmpeg;

public interface IFfmpegInstaller
{
    Task<bool> DownloadAndInstallAsync(IProgress<FfmpegInstallProgress>? progress = null, CancellationToken ct = default);
}
