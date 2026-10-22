using System.Windows;
using System.Windows.Input;
using AuraLe.ViewModels;
using Common.WinApi;

namespace AuraLe;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        PreviewKeyDown += MainWindow_PreviewKeyDown;
        PreviewKeyUp += MainWindow_PreviewKeyUp;
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 텍스트박스 입력 중일 때는 건반 타건 가로채지 않음
        if (Keyboard.FocusedElement is System.Windows.Controls.TextBox) return;

        if (DataContext is MainViewModel { IsSynthView: true } vm)
        {
            bool isShift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            vm.Synth.HandleKeyDown(e.Key, isShift);
        }
    }

    private void MainWindow_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is System.Windows.Controls.TextBox) return;

        if (DataContext is MainViewModel { IsSynthView: true } vm)
        {
            vm.Synth.HandleKeyUp(e.Key);
        }
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        WindowHelper.EnableDragMove(this, e);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.Dispose();
        }
        Close();
    }
}