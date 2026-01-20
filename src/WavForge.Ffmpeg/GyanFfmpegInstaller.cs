using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace WavForge.Ffmpeg;

public sealed class GyanFfmpegInstaller : IFfmpegInstaller
{
    private const string DownloadUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

    private readonly HttpClient _http;
    private readonly string _installDir;
    private readonly string _ffmpegExePath;
    private readonly string _ffprobeExePath;

    public GyanFfmpegInstaller(HttpClient http)
    {
        _http = http;

        _installDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WavForge",
            "ffmpeg"
        );

        _ffmpegExePath = Path.Combine(_installDir, "ffmpeg.exe");
        _ffprobeExePath = Path.Combine(_installDir, "ffprobe.exe");
    }

    public async Task<bool> DownloadAndInstallAsync(
        IProgress<FfmpegInstallProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            progress?.Report(new FfmpegInstallProgress("Auto-install only supported on Windows.", null));
            return false;
        }

        Directory.CreateDirectory(_installDir);

        string tempZipPath = Path.Combine(Path.GetTempPath(), $"wavforge-ffmpeg-{Guid.NewGuid():N}.zip");
        string tempExtractDir = Path.Combine(Path.GetTempPath(), $"wavforge-ffmpeg-{Guid.NewGuid():N}");

        try
        {
            progress?.Report(new FfmpegInstallProgress("Downloading FFmpeg…", 0));

            await DownloadFileAsync(DownloadUrl, tempZipPath, progress, ct);

            progress?.Report(new FfmpegInstallProgress("Extracting…", null));

            Directory.CreateDirectory(tempExtractDir);
            await ZipFile.ExtractToDirectoryAsync(tempZipPath, tempExtractDir, ct);

            // Find ffmpeg.exe inside the extracted tree:
            // It’s usually: ffmpeg-*-essentials_build\bin\ffmpeg.exe
            string? ffmpegSource = Directory
                                       .EnumerateFiles(tempExtractDir, "ffmpeg.exe", SearchOption.AllDirectories)
                                       .FirstOrDefault(p => p.EndsWith(Path.Combine("bin", "ffmpeg.exe"), StringComparison.OrdinalIgnoreCase))
                                   ?? Directory.EnumerateFiles(tempExtractDir, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();

            if (ffmpegSource is null)
            {
                progress?.Report(new FfmpegInstallProgress("Failed: ffmpeg.exe not found in the downloaded package.", null));
                return false;
            }

            string? ffprobeSource = Directory
                                        .EnumerateFiles(tempExtractDir, "ffprobe.exe", SearchOption.AllDirectories)
                                        .FirstOrDefault(p => p.EndsWith(Path.Combine("bin", "ffprobe.exe"), StringComparison.OrdinalIgnoreCase))
                                    ?? Directory.EnumerateFiles(tempExtractDir, "ffprobe.exe", SearchOption.AllDirectories).FirstOrDefault();

            // Replace existing files safely
            ReplaceFile(ffmpegSource, _ffmpegExePath);

            if (ffprobeSource is not null)
            {
                ReplaceFile(ffprobeSource, _ffprobeExePath);
            }

            progress?.Report(new FfmpegInstallProgress("Verifying FFmpeg…", null));

            if (!await VerifyRunsAsync(_ffmpegExePath, ct))
            {
                progress?.Report(new FfmpegInstallProgress("Failed: ffmpeg.exe could not be executed after install.", null));
                return false;
            }

            progress?.Report(new FfmpegInstallProgress("Installed successfully.", 1));
            return true;
        }
        catch (OperationCanceledException)
        {
            progress?.Report(new FfmpegInstallProgress("Cancelled.", null));
            return false;
        }
        catch (Exception ex)
        {
            progress?.Report(new FfmpegInstallProgress($"Failed: {ex.Message}", null));
            return false;
        }
        finally
        {
            SafeDeleteFile(tempZipPath);
            SafeDeleteDirectory(tempExtractDir);
        }
    }

    private static void ReplaceFile(string source, string destination)
    {
        // Ensure destination directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        // On Windows, File.Copy over an existing file can fail if the file is in use.
        // Delete + copy is simplest for a utility.
        if (File.Exists(destination))
        {
            File.Delete(destination);
        }

        File.Copy(source, destination, overwrite: false);
    }

    private static async Task<bool> VerifyRunsAsync(string ffmpegPath, CancellationToken ct)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            // Read at least a little output to ensure it actually started
            _ = await process.StandardOutput.ReadLineAsync(ct);
            await process.WaitForExitAsync(ct);

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task DownloadFileAsync(
        string url,
        string destinationPath,
        IProgress<FfmpegInstallProgress>? progress,
        CancellationToken ct)
    {
        using HttpResponseMessage response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        long? total = response.Content.Headers.ContentLength;

        await using Stream input = await response.Content.ReadAsStreamAsync(ct);
        await using var output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

        byte[] buffer = new byte[1024 * 64];
        long readTotal = 0;

        while (true)
        {
            int read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            if (read == 0)
            {
                break;
            }

            await output.WriteAsync(buffer.AsMemory(0, read), ct);
            readTotal += read;

            if (total.HasValue && total.Value > 0)
            {
                double pct = readTotal / (double)total.Value;
                progress?.Report(new FfmpegInstallProgress("Downloading FFmpeg…", pct));
            }
        }
    }

    private static void SafeDeleteFile(string path)
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

    private static void SafeDeleteDirectory(string path)
    {
        try 
        { 
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // ignored
        }
    }
}
