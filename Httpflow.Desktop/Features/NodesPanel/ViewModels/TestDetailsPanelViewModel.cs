using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Httpflow.Desktop.Features.Projects.ViewModels;
using Httpflow.Desktop.Services.Projects;

namespace Httpflow.Desktop.Features.NodesPanel.ViewModels;

public partial class TestDetailsPanelViewModel : ObservableObject
{
    private readonly ProjectSessionService _projectSessionService;
    private int? _selectedTestId;
    private bool _isLoadingTest;

    public TestDetailsPanelViewModel(ProjectSessionService projectSessionService)
    {
        _projectSessionService = projectSessionService;
    }

    public event Action<int>? TestUpdated;

    public event Action<int>? TestDeleted;

    public event Action<int>? TestImported;

    [ObservableProperty]
    private string testName = string.Empty;

    [ObservableProperty]
    private string status = "Not started";

    [ObservableProperty]
    private string orderText = "1";

    [ObservableProperty]
    private int nodeCount;

    [ObservableProperty]
    private bool canDeleteTest;

    public string ImportExportDescription => "Move this test between projects as JSON. Export keeps the test definition; import adds it as a new test with fresh ids.";

    [RelayCommand]
    private void DeleteSelectedTest()
    {
        if (_selectedTestId is not { } testId)
        {
            return;
        }

        if (_projectSessionService.DeleteTest(testId))
        {
            TestDeleted?.Invoke(testId);
        }
    }

    public void SetSelectedTest(WorkspaceTestColumnViewModel test)
    {
        _selectedTestId = test.Id;

        _isLoadingTest = true;
        try
        {
            TestName = test.Name;
            OrderText = Math.Max(1, test.Order).ToString();
            Status = string.IsNullOrWhiteSpace(test.Status) ? "Not started" : test.Status;
            NodeCount = test.NodeCount;
            CanDeleteTest = _projectSessionService.CurrentProject?.Tests.Count > 1;
        }
        finally
        {
            _isLoadingTest = false;
        }
    }

    public string? ExportSelectedTestJson()
    {
        return _selectedTestId is { } testId
            ? _projectSessionService.ExportTestToJson(testId)
            : null;
    }

    public int? ImportTestJson(string json)
    {
        var test = _projectSessionService.ImportTestFromJson(json);
        if (test is null)
        {
            return null;
        }

        TestImported?.Invoke(test.Id);
        return test.Id;
    }

    partial void OnTestNameChanged(string value)
    {
        if (_isLoadingTest || _selectedTestId is not { } testId)
        {
            return;
        }

        if (_projectSessionService.UpdateTestName(testId, value))
        {
            TestUpdated?.Invoke(testId);
        }
    }

    partial void OnOrderTextChanged(string value)
    {
        if (_isLoadingTest || _selectedTestId is not { } testId || !int.TryParse(value, out var order))
        {
            return;
        }

        if (_projectSessionService.UpdateTestOrder(testId, order))
        {
            TestUpdated?.Invoke(testId);
        }
    }
}
