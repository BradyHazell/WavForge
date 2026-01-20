namespace WavForge.Services;

internal interface IUserPromptService
{
    Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmText = "Yes",
        string cancelText = "No");
}
