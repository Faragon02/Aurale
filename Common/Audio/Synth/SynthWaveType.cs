namespace Common.Audio.Synth;

/// <summary>
/// 신디사이저 생성 파형 및 음색 프리셋 (피아노 / 하프 / 통기타 / 일렉기타)
/// </summary>
public enum SynthWaveType
{
    /// <summary>
    /// 영롱하고 부드러운 어쿠스틱/전자 피아노 (해머 타현 배음 + 댐퍼 릴리즈 감쇄)
    /// </summary>
    PianoEP,

    /// <summary>
    /// 어쿠스틱 타현 하프 / 벨 (빠른 어택 + 지수 감쇄)
    /// </summary>
    AcousticHarp,

    /// <summary>
    /// 어쿠스틱 통기타 (Karplus-Strong 물리 모델링: 실제 현 찰현감과 울림)
    /// </summary>
    AcousticGuitar,

    /// <summary>
    /// 빈티지 일렉 기타 (따뜻한 튜브 오버드라이브 앰프 드라이브 + 캐비닛)
    /// </summary>
    ElectricGuitar
}
