using System.Windows;

namespace OwTranslateLite;

public partial class QuickStartWindow : Window
{
    public QuickStartWindow()
    {
        InitializeComponent();
    }

    public bool DoNotShowAgain => DoNotShowAgainCheck.IsChecked == true;

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
