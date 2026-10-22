using System.IO;

namespace AuraLe.Models;

/// <summary>
/// AuraLe 사용자 설정 모델
/// </summary>
public class AppSettings
{
    /// <summary>
    /// MP3 저장 및 재생 기본 폴더 경로 (C 드라이브 기본 생성)
    /// </summary>
    public string DownloadPath { get; set; } = @"C:\AuraLe\Music";

    /// <summary>
    /// 다운로드 및 오디오 변환 임시 캐시 폴더 경로
    /// </summary>
    public string TempPath { get; set; } = @"C:\AuraLe\Temp";

    /// <summary>
    /// 마지막 설정 볼륨 (0.0 ~ 1.0)
    /// </summary>
    public float Volume { get; set; } = 0.8f;

    /// <summary>
    /// 사용자 정의 카세트 배경 이미지 파일 경로 (P2)
    /// </summary>
    public string? BackgroundImagePath { get; set; }

    /// <summary>
    /// 창 투명도 (0.2 ~ 1.0) (P2)
    /// </summary>
    public double WindowOpacity { get; set; } = 1.0;

    /// <summary>
    /// 마지막으로 재생/선택된 음원 파일 전체 경로
    /// </summary>
    public string? LastPlayedFilePath { get; set; }

    /// <summary>
    /// 배경 이미지 가로 오프셋 위치 (-300 ~ 300)
    /// </summary>
    public double BackgroundImageOffsetX { get; set; } = 0;

    /// <summary>
    /// 셔플(랜덤) 재생 활성화 여부
    /// </summary>
    public bool IsShuffle { get; set; } = false;

    /// <summary>
    /// 반복 재생 모드 (Off, RepeatAll, RepeatOne)
    /// </summary>
    public RepeatMode RepeatMode { get; set; } = RepeatMode.Off;

    /// <summary>
    /// 메인 테마 색상 1 (HEX)
    /// </summary>
    public string ThemeColor1 { get; set; } = "#00F0FF";

    /// <summary>
    /// 메인 테마 색상 2 (그라데이션 끝 색상 HEX)
    /// </summary>
    public string? ThemeColor2 { get; set; } = "#FFE600";

    /// <summary>
    /// 그라데이션 테마 활성화 여부
    /// </summary>
    public bool IsGradientTheme { get; set; } = false;

    /// <summary>
    /// 선택된 색상 카테고리 ("Normal", "Neon", "Gradient")
    /// </summary>
    public string ThemeCategory { get; set; } = "Neon";

    /// <summary>
    /// 커스텀 카세트 릴 휠 이미지 파일 경로 (null이면 기본 Assets/reel.png)
    /// </summary>
    public string? CustomReelImagePath { get; set; }

    /// <summary>
    /// 네온 발광(Glow) 블러 효과 활성화 여부
    /// </summary>
    public bool IsNeonGlow { get; set; } = false;
}
