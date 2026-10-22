using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuraLe.ViewModels;
using Common.WinApi;

namespace AuraLe.Views;

/// <summary>
/// Interaction logic for PlaylistDrawerView.xaml
/// </summary>
public partial class PlaylistDrawerView : UserControl
{
    public PlaylistDrawerView()
    {
        InitializeComponent();
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window != null)
        {
            WindowHelper.EnableDragMove(window, e);
        }
    }

    private void Playlist_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.CurrentTrack != null)
        {
            vm.SelectAndPlayCommand.Execute(vm.CurrentTrack);
        }
    }
}
