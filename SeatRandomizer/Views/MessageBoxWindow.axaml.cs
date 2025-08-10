// Views/MessageBoxWindow.axaml.cs
using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Threading.Tasks;

namespace SeatRandomizer.Views;

public partial class MessageBoxWindow : Window
{
    private TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();

    public MessageBoxWindow()
    {
        InitializeComponent();
    }

    public static async Task<bool> ShowAsync(Window parent, string title, string message, string yesText = "Yes", string noText = "No")
    {
        var msgBox = new MessageBoxWindow
        {
            Title = title
        };
        msgBox.MessageTextBlock.Text = message;
        msgBox.YesButton.Content = yesText;
        msgBox.NoButton.Content = noText;

        var _ = msgBox.ShowDialog(parent); // Fire and forget show
        return await msgBox._tcs.Task;
    }

    private void YesButton_Click(object sender, RoutedEventArgs e)
    {
        _tcs.SetResult(true);
        Close();
    }

    private void NoButton_Click(object sender, RoutedEventArgs e)
    {
        _tcs.SetResult(false);
        Close();
    }

    // 处理窗口关闭事件，确保 Task 被设置
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _tcs.TrySetResult(false); // 默认为 false
    }
}