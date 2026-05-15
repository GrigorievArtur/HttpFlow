using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Httpflow.Desktop.Services;
using Httpflow.Desktop.ViewModels;

namespace Httpflow.Desktop.Views;

public partial class ProjectWorkspacePage : UserControl
{
    private bool _isSidebarOpen = true;
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
    private readonly NodeBuilder _nodeBuilder = new();
    
    public ProjectWorkspacePage()
    {
        InitializeComponent();
        var viewModel = new ProjectWorkspaceViewModel();
        viewModel.NodeCreated += OnNodeCreated;
        DataContext = viewModel;
        NodeCanvas.RenderTransform = _canvasScale;

        DrawGrid();
        NodeCanvas.Children.Remove(MouseProjectionDot);
        NodeCanvas.Children.Add(MouseProjectionDot);
    }

    private void SidebarToggleButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _isSidebarOpen = !_isSidebarOpen;
        SidebarPanel.IsVisible = _isSidebarOpen;
        WorkspaceLayout.ColumnDefinitions[2].Width = new GridLength(_isSidebarOpen ? 392 : 0);
        SidebarToggleButton.Content = _isSidebarOpen ? ">>" : "<<";
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

    private void Viewport_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
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
    
    private void Viewport_OnPointerPressed(object? sender, PointerPressedEventArgs e)
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

    private void Viewport_OnPointerMoved(object? sender, PointerEventArgs e)
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

    private void Viewport_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isPanning = false;
        e.Handled = true;
    }

    private void OnNodeCreated(Httpflow.Desktop.Models.Nodes.CanvasNodeRecord nodeRecord)
    {
        var nodeControl = _nodeBuilder.Build(nodeRecord);
        nodeControl.PointerPressed += Node_OnPointerPressed;
        nodeControl.PointerMoved += Node_OnPointerMoved;
        nodeControl.PointerReleased += Node_OnPointerReleased;

        Canvas.SetLeft(nodeControl, nodeRecord.X);
        Canvas.SetTop(nodeControl, nodeRecord.Y);

        NodeCanvas.Children.Add(nodeControl);
        NodeCanvas.Children.Remove(MouseProjectionDot);
        NodeCanvas.Children.Add(MouseProjectionDot);
    }

    private void Node_OnPointerPressed(object? sender, PointerPressedEventArgs e)
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

    private void Node_OnPointerMoved(object? sender, PointerEventArgs e)
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
        UpdateMouseProjectionDot(e.GetPosition(ViewportSurface));
        e.Handled = true;
    }

    private void Node_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        _draggedNode = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void ZoomInButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Zoom(ZoomStep);
    }

    private void ZoomOutButton_OnClick(object? sender, RoutedEventArgs e)
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

        if (DataContext is ProjectWorkspaceViewModel viewModel)
        {
            viewModel.SetMouseProjectionPosition(canvasPoint);
        }
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
        ZoomTextBlock.Text = $"{_zoom:P0}";
    }

    private void ZoomDefaultButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _zoom = 1;
        // _panX = 0;
        // _panY = 0;
        ApplyViewportTransform();
    }
}
