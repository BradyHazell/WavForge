using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WavForge.Services;

internal static class FileReveal
{
    public static void Reveal(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{filePath}\"",
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", $"-R \"{filePath}\"");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start("xdg-open", Path.GetDirectoryName(filePath)!);
        }
    }
}
