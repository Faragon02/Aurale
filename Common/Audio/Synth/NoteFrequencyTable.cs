namespace Common.Audio.Synth;

/// <summary>
/// MIDI 음표 및 주파수(Hz) 계산과 매핑 룩업 테이블
/// </summary>
public static class NoteFrequencyTable
{
    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
    private static readonly string[] SolfegeNames = { "도", "도#", "레", "레#", "미", "파", "파#", "솔", "솔#", "라", "라#", "시" };

    /// <summary>
    /// MIDI 번호 (21 ~ 108)에 해당하는 주파수(Hz) 계산
    /// A4(69) = 440.0 Hz 기준
    /// </summary>
    public static double MidiToFrequency(int midiNote)
    {
        return 440.0 * Math.Pow(2.0, (midiNote - 69) / 12.0);
    }

    /// <summary>
    /// 주파수(Hz)로부터 가장 가까운 MIDI 음표 번호(0 ~ 127) 계산
    /// </summary>
    public static int FrequencyToMidi(double frequency)
    {
        if (frequency <= 10.0) return 0;
        int note = (int)Math.Round(69.0 + 12.0 * Math.Log2(frequency / 440.0));
        return Math.Clamp(note, 0, 127);
    }

    /// <summary>
    /// MIDI 번호로부터 영문 음이름(예: "C4", "F#5") 반환
    /// </summary>
    public static string GetNoteName(int midiNote)
    {
        if (midiNote < 0 || midiNote > 127) return "?";
        int octave = (midiNote / 12) - 1;
        int noteIndex = midiNote % 12;
        return $"{NoteNames[noteIndex]}{octave}";
    }

    /// <summary>
    /// MIDI 번호로부터 계이름(예: "도", "레#") 반환
    /// </summary>
    public static string GetSolfege(int midiNote)
    {
        if (midiNote < 0 || midiNote > 127) return "?";
        int noteIndex = midiNote % 12;
        return SolfegeNames[noteIndex];
    }

    /// <summary>
    /// 3옥타브 21키 원신/로아 표준 음역대(C3 ~ B5) 내부로 옥타브 정규화
    /// </summary>
    public static int NormalizeToThreeOctaves(int midiNote)
    {
        if (midiNote < 21) return 60; // 기본 C4
        while (midiNote < 48) midiNote += 12; // C3(48) 미만이면 옥타브 업
        while (midiNote > 83) midiNote -= 12; // B5(83) 초과면 옥타브 다운
        return midiNote;
    }
}
