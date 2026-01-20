namespace WavForge.Ffmpeg;

public record FfmpegConversionProgress(double? Percent, string Stage, TimeSpan? Processed, double? Speed);
