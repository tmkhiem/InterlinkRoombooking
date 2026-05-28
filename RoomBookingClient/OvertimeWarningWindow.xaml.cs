using System.Windows;

namespace RoomBookingClient;

public partial class OvertimeWarningWindow : Window
{
    public OvertimeWarningWindow()
    {
        InitializeComponent();
    }

    public void SetMessage(string message)
    {
        MessageText.Text = message;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
