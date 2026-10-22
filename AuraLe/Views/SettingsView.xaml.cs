using System.Windows;
using System.Windows.Controls;

namespace AuraLe.Views;

/// <summary>
/// SettingsView.xaml에 대한 상호 작용 논리
/// </summary>
public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            switch (tag)
            {
                case "Background":
                    SectionBackground?.BringIntoView();
                    break;
                case "Reel":
                    SectionReel?.BringIntoView();
                    break;
                case "Color":
                    SectionColor?.BringIntoView();
                    break;
                case "Etc":
                    SectionEtc?.BringIntoView();
                    break;
            }
        }
    }
}
