namespace WavForge.Ffmpeg;

public interface IFfmpegLocator
{
    /// <summary>
    /// Returns the path to the ffmpeg executable, or null if not found.
    /// </summary>
    string? FindFfmpeg();
}
