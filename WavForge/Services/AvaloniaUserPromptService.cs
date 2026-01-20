using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace WavForge.Services;

internal sealed class AvaloniaUserPromptService : IUserPromptService
{
    private readonly IWindowProvider _windowProvider;

    public AvaloniaUserPromptService(IWindowProvider windowProvider)
    {
        _windowProvider = windowProvider;
    }

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText = "Yes", string cancelText = "No")
    {
        Window? owner = _windowProvider.GetMainWindow();
        if (owner is null)
        {
            return false;
        }

        var tcs = new TaskCompletionSource<bool>();

        var dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 180,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap
        };

        var cancelButton = new Button
        {
            Content = cancelText,
            MinWidth = 80
        };

        var confirmButton = new Button
        {
            Content = confirmText,
            MinWidth = 80
        };

        cancelButton.Click += (_, _) =>
        {
            tcs.TrySetResult(false);
            dialog.Close();
        };

        confirmButton.Click += (_, _) =>
        {
            tcs.TrySetResult(true);
            dialog.Close();
        };

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children =
            {
                messageText,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children =
                    {
                        cancelButton,
                        confirmButton
                    }
                }
            }
        };

        await dialog.ShowDialog(owner);
        return await tcs.Task;
    }
}
