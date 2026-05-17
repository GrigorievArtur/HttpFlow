using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Httpflow.Desktop.Features.Projects.ViewModels;
using Httpflow.Desktop.Models.Nodes;
using Httpflow.Desktop.Services.Nodes;
using Httpflow.Desktop.Services.Projects;

namespace Httpflow.Desktop.Features.Projects.Views;

public partial class ProjectWorkspaceView : UserControl
{
    private readonly ScaleTransform _canvasScale = new(1, 1);

    private const double MinZoom = 0.25;
    private const double MaxZoom = 3;
    private const double ZoomStep = 1.04;

    private double _zoom = 1;
    private double _panX;
    private double _panY;
    private bool _isPanning;
    private Point _lastPanPoint;
    private Control? _draggedNode;
    private Point _dragPointerOffset;
    private readonly CanvasNodeBuilder _nodeBuilder = new();
    private readonly ProjectSessionService _projectSessionService;
    private readonly List<Control> _nodeControls = [];
    private readonly ProjectWorkspaceViewModel _viewModel;
    
    public ProjectWorkspaceView()
    {
        InitializeComponent();
        _projectSessionService = ((App)Application.Current!).ProjectSessionService;
        _viewModel = new ProjectWorkspaceViewModel(_projectSessionService);
        _viewModel.NodeCreated += OnNodeCreated;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        DataContext = _viewModel;
        NodeCanvas.RenderTransform = _canvasScale;
        Loaded += OnLoaded;

        DrawGrid();
        NodeCanvas.Children.Remove(MouseProjectionDot);
        NodeCanvas.Children.Add(MouseProjectionDot);
        ApplySidebarState();
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (((App)Application.Current!).CurrentProject is not { } currentProject)
        {
            return;
        }

        await _projectSessionService.LoadProjectById(currentProject.Id);
        _viewModel.ProjectTitle = string.IsNullOrWhiteSpace(currentProject.Name) ? $"Project #{currentProject.Id}" : currentProject.Name;
        RenderLoadedNodes();
    }

    private void DrawGrid()
    {
        const double gridSize = 50;
        const double canvasWidth = 3000;
        const double canvasHeight = 2000;
        
        var gridBrush = new SolidColorBrush(Colors.Gainsboro);
        for (double x = 0; x <= canvasWidth; x += gridSize)
        {
            NodeCanvas.Children.Add(new Line
            {
                StartPoint = new Point(x, 0),
                EndPoint = new Point(x, canvasHeight),
                Stroke = gridBrush,
                StrokeThickness = 1
            });
        }

        for (double y = 0; y <= canvasHeight; y += gridSize)
        {
            NodeCanvas.Children.Add(new Line
            {
                StartPoint = new Point(0, y),
                EndPoint = new Point(canvasWidth, y),
                Stroke = gridBrush,
                StrokeThickness = 1
            });
        }
    }

    private void OnViewportPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var mouseInViewport = e.GetPosition(ViewportSurface);
        UpdateMouseProjectionDot(mouseInViewport);

        if (e.Delta.Y == 0)
        {
            return;
        }

        ZoomAtViewportPoint(e.Delta.Y > 0 ? ZoomStep : 1 / ZoomStep, mouseInViewport);
        UpdateMouseProjectionDot(mouseInViewport);
        e.Handled = true;
    }
    
    private void OnViewportPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var pointer = e.GetCurrentPoint(ViewportSurface);
        var viewportPoint = e.GetPosition(ViewportSurface);
        UpdateMouseProjectionDot(viewportPoint);

        if (pointer.Properties.IsRightButtonPressed)
        {
            return;
        }

        if (!pointer.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isPanning = true;
        _lastPanPoint = viewportPoint;
        e.Handled = true;
    }

    private void OnViewportPointerMoved(object? sender, PointerEventArgs e)
    {
        var currentPoint = e.GetPosition(ViewportSurface);
        UpdateMouseProjectionDot(currentPoint);

        if (!_isPanning)
        {
            return;
        }

        var delta = currentPoint - _lastPanPoint;

        _panX += delta.X;
        _panY += delta.Y;
        ApplyViewportTransform();
        UpdateMouseProjectionDot(currentPoint);

        _lastPanPoint = currentPoint;
        e.Handled = true;
    }

    private void OnViewportPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isPanning = false;
        e.Handled = true;
    }

    private void OnNodeCreated(CanvasNodeRecord nodeRecord)
    {
        AddNodeControl(nodeRecord);
    }

    private void OnNodePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        var pointer = e.GetCurrentPoint(NodeCanvas);
        if (!pointer.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _draggedNode = control;
        var canvasPoint = e.GetPosition(NodeCanvas);
        _dragPointerOffset = new Point(
            canvasPoint.X - Canvas.GetLeft(control),
            canvasPoint.Y - Canvas.GetTop(control));

        e.Pointer.Capture(control);
        e.Handled = true;
    }

    private void OnNodePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedNode is null || sender is not Control control)
        {
            return;
        }

        if (!ReferenceEquals(control, _draggedNode))
        {
            return;
        }

        var canvasPoint = e.GetPosition(NodeCanvas);
        Canvas.SetLeft(control, Math.Max(0, canvasPoint.X - _dragPointerOffset.X));
        Canvas.SetTop(control, Math.Max(0, canvasPoint.Y - _dragPointerOffset.Y));

        if (control.DataContext is CanvasNodeRecord nodeRecord)
        {
            _projectSessionService.MoveNode(
                nodeRecord.Id,
                (int)Math.Round(Canvas.GetLeft(control)),
                (int)Math.Round(Canvas.GetTop(control)));
        }

        UpdateMouseProjectionDot(e.GetPosition(ViewportSurface));
        e.Handled = true;
    }

    private void OnNodePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        _draggedNode = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnZoomInButtonClick(object? sender, RoutedEventArgs e)
    {
        Zoom(ZoomStep);
    }

    private void OnZoomOutButtonClick(object? sender, RoutedEventArgs e)
    {
        Zoom(1 / ZoomStep);
    }

    private void Zoom(double factor)
    {
        SetZoom(_zoom * factor);
    }

    private void ZoomAtViewportPoint(double factor, Point viewportPoint)
    {
        var newZoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);

        if (Math.Abs(newZoom - _zoom) < 0.001)
        {
            return;
        }

        var canvasPoint = ViewportToCanvas(viewportPoint);

        _zoom = newZoom;
        MoveCanvasPointToViewportPoint(canvasPoint, viewportPoint);

        ApplyViewportTransform();
    }

    private Point ViewportToCanvas(Point viewportPoint)
    {
        return new Point(
            (viewportPoint.X - _panX) / _zoom,
            (viewportPoint.Y - _panY) / _zoom);
    }

    private void MoveCanvasPointToViewportPoint(Point canvasPoint, Point viewportPoint)
    {
        _panX = viewportPoint.X - canvasPoint.X * _zoom;
        _panY = viewportPoint.Y - canvasPoint.Y * _zoom;
    }

    private void UpdateMouseProjectionDot(Point viewportPoint)
    {
        var canvasPoint = ViewportToCanvas(viewportPoint);
        const double dotRadius = 6;

        Canvas.SetLeft(MouseProjectionDot, canvasPoint.X - dotRadius);
        Canvas.SetTop(MouseProjectionDot, canvasPoint.Y - dotRadius);

        _viewModel.SetMouseProjectionPosition(canvasPoint);
    }

    private void SetZoom(double zoom)
    {
        _zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        ApplyViewportTransform();
    }

    private void ApplyViewportTransform()
    {
        _canvasScale.ScaleX = _zoom;
        _canvasScale.ScaleY = _zoom;
        NodeCanvas.Margin = new Thickness(_panX, _panY, 0, 0);
        _viewModel.ZoomDisplay = $"{_zoom:P0}";
    }

    private void RenderLoadedNodes()
    {
        foreach (var control in _nodeControls)
        {
            NodeCanvas.Children.Remove(control);
        }

        _nodeControls.Clear();

        if (_projectSessionService.CurrentProject is null)
        {
            return;
        }

        foreach (var node in _projectSessionService.CurrentProject.Nodes)
        {
            AddNodeControl(node);
        }
    }

    private void AddNodeControl(CanvasNodeRecord nodeRecord)
    {
        var nodeControl = _nodeBuilder.Build(nodeRecord);
        nodeControl.PointerPressed += OnNodePointerPressed;
        nodeControl.PointerMoved += OnNodePointerMoved;
        nodeControl.PointerReleased += OnNodePointerReleased;

        Canvas.SetLeft(nodeControl, nodeRecord.X);
        Canvas.SetTop(nodeControl, nodeRecord.Y);

        _nodeControls.Add(nodeControl);
        NodeCanvas.Children.Add(nodeControl);
        NodeCanvas.Children.Remove(MouseProjectionDot);
        NodeCanvas.Children.Add(MouseProjectionDot);
    }

    private void OnZoomDefaultButtonClick(object? sender, RoutedEventArgs e)
    {
        _zoom = 1;
        // _panX = 0;
        // _panY = 0;
        ApplyViewportTransform();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProjectWorkspaceViewModel.IsSidebarOpen))
        {
            ApplySidebarState();
        }
    }

    private void ApplySidebarState()
    {
        WorkspaceLayout.ColumnDefinitions[2].Width = _viewModel.SidebarWidth;
    }
}
