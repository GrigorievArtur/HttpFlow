using System.IO;
using System.Text;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Httpflow.Desktop.Features.NodesPanel.ViewModels;

namespace Httpflow.Desktop.Features.NodesPanel.Views;

public partial class TestDetailsPanelView : UserControl
{
    public TestDetailsPanelView()
    {
        InitializeComponent();
    }

    private TestDetailsPanelViewModel? ViewModel => DataContext as TestDetailsPanelViewModel;

    private async void OnExportJsonButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel?.ExportSelectedTestJson() is not { } json)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export test JSON",
            SuggestedFileName = $"{SanitizeFileName(ViewModel.TestName)}.httpflow-test.json",
            FileTypeChoices =
            [
                new FilePickerFileType("JSON")
                {
                    Patterns = ["*.json"]
                }
            ]
        });

        if (file is null)
        {
            return;
        }

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream, Encoding.UTF8);
        await writer.WriteAsync(json);
    }

    private async void OnImportJsonButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null || ViewModel is null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import test JSON",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("JSON")
                {
                    Patterns = ["*.json"]
                }
            ]
        });

        var file = files.Count > 0 ? files[0] : null;
        if (file is null)
        {
            return;
        }

        await using var stream = await file.OpenReadAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        ViewModel.ImportTestJson(await reader.ReadToEndAsync());
    }

    private static string SanitizeFileName(string fileName)
    {
        var safeName = string.IsNullOrWhiteSpace(fileName) ? "test" : fileName.Trim();
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            safeName = safeName.Replace(invalidCharacter, '-');
        }

        return safeName;
    }
}
