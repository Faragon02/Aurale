using System.Windows;
using System.Windows.Input;

namespace Common.WinApi;

/// <summary>
/// WPF 윈도우 창 제어 헬퍼
/// </summary>
public static class WindowHelper
{
    /// <summary>
    /// 마우스 왼쪽 버튼 클릭 시 무테 창 드래그 이동을 처리합니다.
    /// </summary>
    public static void EnableDragMove(Window window, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            window.DragMove();
        }
    }

    /// <summary>
    /// 창 투명도(Opacity)를 0.2 ~ 1.0 범위로 안전하게 설정합니다.
    /// </summary>
    public static void SetOpacity(Window window, double opacity)
    {
        window.Opacity = Math.Clamp(opacity, 0.2, 1.0);
    }
}
