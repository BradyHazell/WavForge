using Avalonia.Controls;

namespace WavForge.Views;

#pragma warning disable CA1852 // Can't be made sealed because the MainWindow.axaml implementation
internal partial class MainWindow : Window
#pragma warning restore CA1852
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
