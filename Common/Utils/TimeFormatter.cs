namespace Common.Utils;

/// <summary>
/// 시간 표시 포맷 변환 유틸리티
/// </summary>
public static class TimeFormatter
{
    /// <summary>
    /// TimeSpan을 "mm:ss" 또는 "hh:mm:ss" 형태의 문자열로 변환
    /// </summary>
    public static string Format(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
        {
            time = TimeSpan.Zero;
        }

        if (time.TotalHours >= 1)
        {
            return $"{(int)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";
        }

        return $"{time.Minutes:D2}:{time.Seconds:D2}";
    }

    /// <summary>
    /// 초(seconds)를 "mm:ss" 형태의 문자열로 변환
    /// </summary>
    public static string FormatSeconds(double totalSeconds)
    {
        return Format(TimeSpan.FromSeconds(totalSeconds));
    }
}
