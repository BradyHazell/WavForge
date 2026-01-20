using System.Diagnostics;
using System.Globalization;

namespace WavForge.Ffmpeg;

public interface IFfmpegRunner
{
    Task<bool> ConvertWav24To16Async(
        string ffmpegPath,
        string inputPath,
        string outputPath,
        double? durationSeconds,
        IProgress<FfmpegConversionProgress>? progress = null,
        CancellationToken ct = default);
}

public sealed class FfmpegRunner : IFfmpegRunner
{
    public async Task<bool> ConvertWav24To16Async(string ffmpegPath, string inputPath, string outputPath, double? durationSeconds,
        IProgress<FfmpegConversionProgress>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

        // Build args
        // -progress pipe:1 writes key=value lines to stdout (easy to parse)
        // -nostats avoids noisy stderr progress
        string args =
            $"-y -hide_banner -loglevel error " +
            $"-i {Quote(inputPath)} " +
            $"-c:a pcm_s16le " +
            $"-progress pipe:1 -nostats " +
            $"{Quote(outputPath)}";

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        try
        {
            progress?.Report(new FfmpegConversionProgress(null, "Starting FFmpeg…", null, null));

            process.Start();

            // If cancelled: kill ffmpeg
            using CancellationTokenRegistration reg = ct.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // ignored
                }
            });

            // Read stdout for -progress lines
            var stdoutTask = Task.Run(async () =>
            {
                string? line;
                while ((line = await process.StandardOutput.ReadLineAsync()) is not null)
                {
                    ParseProgressLine(line, durationSeconds, progress);
                }
            }, ct);

            // Read stderr for errors (loglevel error should make this small)
            var stderrTask = Task.Run(async () =>
            {
                string? line;
                while ((line = await process.StandardError.ReadLineAsync()) is not null)
                {
                    progress?.Report(new FfmpegConversionProgress(null, line, null, null));
                }
            }, ct);

            await process.WaitForExitAsync(ct);
            await Task.WhenAll(stdoutTask, stderrTask);

            if (ct.IsCancellationRequested)
            {
                return false;
            }

            return process.ExitCode == 0 && File.Exists(outputPath);
        }
        catch (OperationCanceledException)
        {
            // Ensure partial file is cleaned up
            SafeDelete(outputPath);
            return false;
        }
        catch
        {
            SafeDelete(outputPath);
            return false;
        }
    }

    private static void ParseProgressLine(
        string line,
        double? durationSeconds,
        IProgress<FfmpegConversionProgress>? progress)
    {
        if (progress is null)
        {
            return;
        }

        // out_time_ms=1234567
        if (line.StartsWith("out_time_ms=", StringComparison.Ordinal))
        {
            string value = line["out_time_ms=".Length..].Trim();
            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long us))
            {
                // FFmpeg uses microseconds for out_time_ms? (despite name); some builds output microseconds.
                // We’ll handle both by assuming:
                // - if value is huge, treat as microseconds
                // - otherwise treat as milliseconds
                var processed = TimeSpan.FromMilliseconds(us / 1000.0);

                double? percent = null;
                if (durationSeconds.HasValue && durationSeconds.Value > 0)
                {
                    percent = Math.Clamp(processed.TotalSeconds / durationSeconds.Value, 0, 1);
                }

                progress.Report(new FfmpegConversionProgress(
                    Percent: percent,
                    Stage: "Converting…",
                    Processed: processed,
                    Speed: null));
            }

            return;
        }

        // speed=1.23x
        if (line.StartsWith("speed=", StringComparison.Ordinal))
        {
            string value = line["speed=".Length..].Trim().TrimEnd('x');
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double speed))
            {
                progress.Report(new FfmpegConversionProgress(
                    Percent: null,
                    Stage: $"Speed: {speed:0.00}×",
                    Processed: null,
                    Speed: speed));
            }

            return;
        }

        // progress=end
        if (line.StartsWith("progress=", StringComparison.Ordinal) && line.EndsWith("end", StringComparison.OrdinalIgnoreCase))
        {
            progress.Report(new FfmpegConversionProgress(1, "Finalising…", null, null));
        }
    }

    private static string Quote(string path) => $"\"{path.Replace("\"", "\\\"")}\"";

    private static void SafeDelete(string path)
    {
        try 
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignored
        }
    }
}
