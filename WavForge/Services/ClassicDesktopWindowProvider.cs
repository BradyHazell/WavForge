using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace WavForge.Services;

internal sealed class ClassicDesktopWindowProvider : IWindowProvider
{
    public Window? GetMainWindow()
    {
        return (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    }
}
