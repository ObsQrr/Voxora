using System.Windows;
using System.Windows.Input;

namespace KayoneZSoundpad.Views;

public partial class InputDialog : Window
{
    public InputDialog(string title, string prompt, string initial = "")
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        InputBox.Text = initial;
        InputBox.SelectAll();
        Loaded += (_, _) => InputBox.Focus();
    }

    public string Value => InputBox.Text.Trim();

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (Value.Length == 0) return;
        DialogResult = true;
    }

    private void InputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Value.Length > 0) DialogResult = true;
    }
}

