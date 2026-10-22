using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuraLe.Models;
using AuraLe.ViewModels;

namespace AuraLe.Views;

/// <summary>
/// SynthDeckView.xaml에 대한 상호 작용 논리
/// </summary>
public partial class SynthDeckView : UserControl
{
    public SynthDeckView()
    {
        InitializeComponent();
    }

    private void Key_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is QwertyKeyNote note)
        {
            if (DataContext is SynthViewModel vm)
            {
                vm.PlayNoteDirectCommand.Execute(note);
            }
        }
    }
}
