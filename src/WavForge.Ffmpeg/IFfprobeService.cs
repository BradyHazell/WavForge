using System.Diagnostics;
using System.Globalization;

namespace WavForge.Ffmpeg;

public interface IFfprobeService
{
    Task<double?> GetDurationInSecondsAsync(string ffprobePath, string inputPath, CancellationToken ct = default);
}

public sealed class FfprobeService : IFfprobeService
{
    public async Task<double?> GetDurationInSecondsAsync(string ffprobePath, string inputPath, CancellationToken ct = default)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments =
                    $"-v error -show_entries format=duration " +
                    $"-of default=noprint_wrappers=1:nokey=1 " +
                    $"{Quote(inputPath)}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();
            string? line = await process.StandardOutput.ReadLineAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                return null;
            }

            if (double.TryParse(line, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds))
            {
                return seconds;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
    
    private static string Quote(string path) => $"\"{path.Replace("\"", "\\\"")}\"";
}
