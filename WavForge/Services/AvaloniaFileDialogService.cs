using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace WavForge.Services;

internal sealed class AvaloniaFileDialogService : IFileDialogService
{
    private static readonly FilePickerFileType _wavType = new("WAV audio")
    {
        Patterns = ["*.wav", "*.WAV"]
    };

    public async Task<string?> PickInputWavAsync(Window owner, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        
        IReadOnlyList<IStorageFile> files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select a WAV file",
            AllowMultiple = false,
            FileTypeFilter =
            [
                _wavType,
                FilePickerFileTypes.All
            ]
        });
        
#pragma warning disable CA1826
        return files.FirstOrDefault()?.TryGetLocalPath();
#pragma warning restore CA1826
    }

    public async Task<string?> PickOutputWavAsync(Window owner, string? suggestedFileName = null, CancellationToken ct = default)
    {
        IStorageFile? file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Choose output WAV location",
            SuggestedFileName = string.IsNullOrWhiteSpace(suggestedFileName) ? "output-16bit.wav" : suggestedFileName,
            DefaultExtension = "wav",
            FileTypeChoices =
            [
                _wavType,
                FilePickerFileTypes.All
            ]
        });

        return file?.TryGetLocalPath();
    }
}
