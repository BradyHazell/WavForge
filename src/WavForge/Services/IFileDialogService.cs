using Avalonia.Controls;

namespace WavForge.Services;

internal interface IFileDialogService
{
    Task<string?> PickInputWavAsync(Window owner, CancellationToken ct = default);
    Task<string?> PickOutputWavAsync(Window owner, string? suggestedFileName = null, CancellationToken ct = default);
}
