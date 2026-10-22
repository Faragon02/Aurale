namespace AuraLe.Models;

/// <summary>
/// 음원 재생 반복 모드
/// </summary>
public enum RepeatMode
{
    /// <summary>
    /// 반복 끔 (순차 재생 후 마지막 곡에서 정지)
    /// </summary>
    Off,

    /// <summary>
    /// 전체 반복 (플레이리스트 무한 루프)
    /// </summary>
    RepeatAll,

    /// <summary>
    /// 한 곡 반복 (현재 트랙 무한 반복)
    /// </summary>
    RepeatOne
}
