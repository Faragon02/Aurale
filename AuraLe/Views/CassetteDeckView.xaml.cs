using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuraLe.ViewModels;
using Common.Utils;

namespace AuraLe.Views;

/// <summary>
/// Interaction logic for CassetteDeckView.xaml
/// </summary>
public partial class CassetteDeckView : UserControl
{
    private bool _isUserSeeking = false;
    private double _pendingSeekSeconds = -1;

    public CassetteDeckView()
    {
        InitializeComponent();

        // [SeekSlider 버그 해결] WPF Slider 내부 컨트롤이 MouseDown 이벤트를 가로채더라도 강제 수신
        SeekSlider.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(SeekSlider_PreviewMouseDown), true);
    }

    private void SeekSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _isUserSeeking = true;
            SeekSlider.CaptureMouse();

            if (DataContext is MainViewModel vm && SeekSlider.ActualWidth > 0 && vm.TotalDurationSeconds > 0)
            {
                var x = e.GetPosition(SeekSlider).X;
                var ratio = Math.Clamp(x / SeekSlider.ActualWidth, 0.0, 1.0);
                var target = ratio * vm.TotalDurationSeconds;
                _pendingSeekSeconds = target;
                SeekSlider.Value = target;

                // 마우스 누르는 순간: 즉시 일시정지 및 타이머 중단하여 이전 시간 충돌 원천 차단
                vm.BeginSeek(target);
            }

            e.Handled = true;
        }
    }

    private void SeekSlider_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isUserSeeking && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdateSeekValueFromMouse(e.GetPosition(SeekSlider).X);
        }
    }

    private void UpdateSeekValueFromMouse(double mouseX)
    {
        if (SeekSlider.ActualWidth > 0 && DataContext is MainViewModel vm && vm.TotalDurationSeconds > 0)
        {
            var ratio = Math.Clamp(mouseX / SeekSlider.ActualWidth, 0.0, 1.0);
            _pendingSeekSeconds = ratio * vm.TotalDurationSeconds;
            SeekSlider.Value = _pendingSeekSeconds;

            // 드래그 중 실시간 시간 텍스트 및 슬라이더 미리보기
            vm.UpdateSeekPreview(_pendingSeekSeconds);
        }
    }

    private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // 사용자가 슬라이더를 직접 조작 중일 때만 시간 텍스트 미리보기 업데이트
        if (_isUserSeeking && DataContext is MainViewModel vm)
        {
            vm.CurrentTimeText = TimeFormatter.FormatSeconds(e.NewValue);
        }
    }

    private void SeekSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isUserSeeking)
        {
            CommitSeek();
            e.Handled = true;
        }
    }

    private void SeekSlider_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_isUserSeeking)
        {
            CommitSeek();
        }
    }

    private void CommitSeek()
    {
        if (_isUserSeeking)
        {
            _isUserSeeking = false;
            SeekSlider.ReleaseMouseCapture();

            if (DataContext is MainViewModel vm)
            {
                var target = _pendingSeekSeconds >= 0 ? _pendingSeekSeconds : SeekSlider.Value;
                _pendingSeekSeconds = -1;

                // 마우스를 떼는 순간: 해당 위치로 Seek 이동 후 원래 재생 중이었다면 재생 재개
                vm.EndSeek(target);
            }
        }
    }
}
