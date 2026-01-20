namespace WavForge.Ffmpeg;

public interface IFfmpegInstaller
{
    Task<bool> DownloadAndInstallAsync(CancellationToken ct = default);
}
