namespace Httpflow.Desktop.Shell.ViewModels;

public sealed class ShellNotificationViewModel
{
    public ShellNotificationViewModel(string title, string message, bool isError)
    {
        Title = title;
        Message = message;
        IsError = isError;
    }

    public string Title { get; }

    public string Message { get; }

    public bool IsError { get; }

    public bool IsInfo => !IsError;
}
