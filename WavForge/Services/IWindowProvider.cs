using Avalonia.Controls;

namespace WavForge.Services;

internal interface IWindowProvider
{
    Window? GetMainWindow();
}
