using Wpf.Ui.Controls;

namespace Chromedroid.Gui;

public partial class NicknameDialog : FluentWindow
{
    public string? ResultNickname { get; private set; }

    public NicknameDialog(string deviceModel, string? currentNickname)
    {
        InitializeComponent();
        DeviceModelText.Text = deviceModel;
        NicknameTextBox.Text = currentNickname ?? string.Empty;
        NicknameTextBox.Focus();
    }

    private void Save_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        ResultNickname = string.IsNullOrWhiteSpace(NicknameTextBox.Text) ? null : NicknameTextBox.Text.Trim();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
