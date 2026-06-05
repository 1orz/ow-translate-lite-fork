using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using OwTranslateLite.Core;

namespace OwTranslateLite.Overlay;

public partial class OverlayWindow : Window
{
    private const int GwlExstyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExLayered = 0x00080000;

    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyClickThrough(true);
    }

    public void ApplySettings(AppSettings settings)
    {
        Shell.Opacity = settings.OverlayOpacity;
        RecordList.FontSize = settings.OverlayFontSize;
        ApplyClickThrough(settings.OverlayClickThrough);
    }

    public void UpdateRecords(IReadOnlyList<TranslationRecord> records)
    {
        RecordList.ItemsSource = records.Reverse().ToList();
    }

    public void MoveNear(Rect captureRegion)
    {
        Left = captureRegion.Left;
        Top = Math.Max(0, captureRegion.Top - Height - 12);
        Width = Math.Max(420, captureRegion.Width);
    }

    private void ApplyClickThrough(bool enabled)
    {
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

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);
}
