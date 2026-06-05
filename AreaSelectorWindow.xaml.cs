using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace OwTranslateLite;

public partial class AreaSelectorWindow : Window
{
    private Point _start;
    private bool _drawing;

    public event EventHandler<Rect>? SelectionCompleted;

    public AreaSelectorWindow()
    {
        InitializeComponent();
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        Loaded += (_, _) => Activate();
        MouseLeftButtonDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseUp;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(this);
        _drawing = true;
        SelectionRect.Visibility = Visibility.Visible;
        SelectionRect.Width = 0;
        SelectionRect.Height = 0;
        Canvas.SetLeft(SelectionRect, _start.X);
        Canvas.SetTop(SelectionRect, _start.Y);
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_drawing)
        {
            return;
        }

        Point current = e.GetPosition(this);
        double left = Math.Min(_start.X, current.X);
        double top = Math.Min(_start.Y, current.Y);
        SelectionRect.Width = Math.Abs(current.X - _start.X);
        SelectionRect.Height = Math.Abs(current.Y - _start.Y);
        Canvas.SetLeft(SelectionRect, left);
        Canvas.SetTop(SelectionRect, top);
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_drawing)
        {
            return;
        }

        ReleaseMouseCapture();
        _drawing = false;
        Point current = e.GetPosition(this);
        double left = Math.Min(_start.X, current.X);
        double top = Math.Min(_start.Y, current.Y);
        double width = Math.Abs(current.X - _start.X);
        double height = Math.Abs(current.Y - _start.Y);
        if (width < 80 || height < 30)
        {
            Close();
            return;
        }

        Point screenTopLeft = PointToScreen(new Point(left, top));
        Point screenBottomRight = PointToScreen(new Point(left + width, top + height));
        Rect rect = new(screenTopLeft.X, screenTopLeft.Y, screenBottomRight.X - screenTopLeft.X, screenBottomRight.Y - screenTopLeft.Y);
        SelectionCompleted?.Invoke(this, rect);
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
