using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using OwTranslateLite.Core;
using MediaColor = System.Windows.Media.Color;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;

namespace OwTranslateLite.Overlay;

public partial class OverlayWindow : Window
{
    private const double MinOverlayWidth = 260;
    private const double MinOverlayHeight = 100;
    private const double MinVisiblePixels = 80;
    private const int GwlExstyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExLayered = 0x00080000;

    private AppSettings? _settings;
    private IReadOnlyList<TranslationRecord> _records = [];
    private bool _isClickThrough = true;
    private bool _isDragging;
    private WpfPoint _dragStartMouse;
    private double _dragStartLeft;
    private double _dragStartTop;

    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyClickThrough(_isClickThrough);
        DragHandle.MouseLeftButtonDown += DragHandle_MouseLeftButtonDown;
        DragHandle.MouseMove += DragHandle_MouseMove;
        DragHandle.MouseLeftButtonUp += DragHandle_MouseLeftButtonUp;
        ResizeGrip.DragDelta += ResizeGrip_DragDelta;
    }

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        _isClickThrough = settings.OverlayClickThrough;
        RecordList.FontSize = settings.OverlayFontSize;
        ApplyBackgroundOpacity(settings.OverlayOpacity);
        ApplyClickThrough(settings.OverlayClickThrough);
        ApplySavedBounds(settings);
        RenderRecords();
    }

    public void UpdateRecords(IReadOnlyList<TranslationRecord> records)
    {
        _records = records.ToList();
        RenderRecords();
    }

    public void MoveNear(Rect captureRegion)
    {
        if (_settings?.OverlayLeft is double left && _settings.OverlayTop is double top)
        {
            ApplySavedBounds(_settings);
            return;
        }

        Left = captureRegion.Left;
        Top = Math.Max(0, captureRegion.Top - Height - 12);
        Width = Math.Max(420, captureRegion.Width);
        KeepMostlyOnScreen();
    }

    private void ApplySavedBounds(AppSettings settings)
    {
        if (settings.OverlayLeft is double left &&
            settings.OverlayTop is double top &&
            IsFinite(left) &&
            IsFinite(top))
        {
            Left = left;
            Top = top;
            if (settings.OverlayWidth is double width && IsFinite(width))
            {
                Width = Math.Max(MinOverlayWidth, width);
            }

            if (settings.OverlayHeight is double height && IsFinite(height))
            {
                Height = Math.Max(MinOverlayHeight, height);
            }

            KeepMostlyOnScreen();
        }
    }

    private void RenderRecords()
    {
        RecordList.ItemsSource = _records.ToList();
        Dispatcher.BeginInvoke(() => TranslationScrollViewer.ScrollToEnd(), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isClickThrough || e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        _isDragging = true;
        _dragStartMouse = GetCursorPositionDip();
        _dragStartLeft = Left;
        _dragStartTop = Top;
        DragHandle.CaptureMouse();
        e.Handled = true;
    }

    private void DragHandle_MouseMove(object sender, WpfMouseEventArgs e)
    {
        if (!_isDragging || _isClickThrough || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        WpfPoint current = GetCursorPositionDip();
        Left = _dragStartLeft + current.X - _dragStartMouse.X;
        Top = _dragStartTop + current.Y - _dragStartMouse.Y;
        KeepMostlyOnScreen();
        e.Handled = true;
    }

    private void DragHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndDrag();
        e.Handled = true;
    }

    private void EndDrag()
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        DragHandle.ReleaseMouseCapture();
    }

    private void ResizeGrip_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (_isClickThrough)
        {
            return;
        }

        Width = Math.Max(MinOverlayWidth, Width + e.HorizontalChange);
        Height = Math.Max(MinOverlayHeight, Height + e.VerticalChange);
        KeepMostlyOnScreen();
    }

    private void ApplyBackgroundOpacity(double opacity)
    {
        FloatingPanel.Background = CreateBackgroundBrush(opacity);
        FloatingPanel.BorderBrush = CreateBorderBrush(opacity);
    }

    private static SolidColorBrush CreateBackgroundBrush(double opacity)
    {
        byte alpha = (byte)(Math.Clamp(opacity, 0.0, 1.0) * 255);
        return new SolidColorBrush(MediaColor.FromArgb(alpha, 7, 9, 10));
    }

    private static SolidColorBrush CreateBorderBrush(double opacity)
    {
        byte alpha = (byte)(Math.Clamp(opacity, 0.0, 1.0) * 120);
        return new SolidColorBrush(MediaColor.FromArgb(alpha, 120, 217, 149));
    }

    private void ApplyClickThrough(bool enabled)
    {
        DragHandleRow.Height = enabled ? new GridLength(0) : new GridLength(20);
        DragHandle.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
        ResizeGrip.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
        DragHandle.IsHitTestVisible = !enabled;
        ResizeGrip.IsHitTestVisible = !enabled;

        if (!IsLoaded)
        {
            return;
        }

        nint handle = new WindowInteropHelper(this).Handle;
        if (handle == 0)
        {
            return;
        }

        int style = GetWindowLong(handle, GwlExstyle);
        if (enabled)
        {
            style |= WsExTransparent | WsExLayered;
        }
        else
        {
            style &= ~WsExTransparent;
            style |= WsExLayered;
        }

        SetWindowLong(handle, GwlExstyle, style);
    }

    private void KeepMostlyOnScreen()
    {
        double minLeft = SystemParameters.VirtualScreenLeft - ActualWidth + MinVisiblePixels;
        double maxLeft = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - MinVisiblePixels;
        double minTop = SystemParameters.VirtualScreenTop;
        double maxTop = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - MinVisiblePixels;

        if (IsFinite(Left))
        {
            Left = Math.Clamp(Left, minLeft, maxLeft);
        }

        if (IsFinite(Top))
        {
            Top = Math.Clamp(Top, minTop, maxTop);
        }
    }

    private WpfPoint GetCursorPositionDip()
    {
        if (!GetCursorPos(out NativePoint nativePoint))
        {
            return PointToScreen(Mouse.GetPosition(this));
        }

        WpfPoint point = new(nativePoint.X, nativePoint.Y);
        PresentationSource? source = PresentationSource.FromVisual(this);
        return source?.CompositionTarget is null
            ? point
            : source.CompositionTarget.TransformFromDevice.Transform(point);
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
