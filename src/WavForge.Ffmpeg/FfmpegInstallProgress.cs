namespace WavForge.Ffmpeg;

public sealed record FfmpegInstallProgress(
    string Stage,
    double? Percent);
