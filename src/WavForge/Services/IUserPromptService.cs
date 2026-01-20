namespace WavForge.Services;

internal interface IUserPromptService
{
    Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmText = "Yes",
        string cancelText = "No");
    
    Task<bool> NoticeAsync(
        string title,
        string message,
        string okText = "OK");
}
