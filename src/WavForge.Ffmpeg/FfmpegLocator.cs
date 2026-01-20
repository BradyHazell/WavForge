using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WavForge.Ffmpeg;

public sealed class FfmpegLocator : IFfmpegLocator
{
    private readonly string _installedFfmpegPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WavForge",
        "ffmpeg",
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg"
    );

    public string? FindFfmpeg()
    {
        if (File.Exists(_installedFfmpegPath))
        {
            return _installedFfmpegPath;
        }

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which",
                Arguments = "ffmpeg",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();
            string? result = process.StandardOutput.ReadLine();
            process.WaitForExit(1000);

            if (!string.IsNullOrWhiteSpace(result) && File.Exists(result))
            {
                return result;
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }
}
