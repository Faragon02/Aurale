namespace Common.Audio.Synth;

public enum EnvelopeState
{
    Attack,
    Decay,
    Sustain,
    Release,
    Idle
}

/// <summary>
/// 단일 음 합성 보이스 (오실레이터 + ADSR 엔벨로프)
/// </summary>
public class SynthVoice
{
    private readonly int _sampleRate;
    private double _phase;
    private double _phaseIncrement;
    private float _envelope = 0f;
    private EnvelopeState _state = EnvelopeState.Idle;

    public int MidiNote { get; private set; }
    public double Frequency { get; private set; }
    public float Velocity { get; private set; } = 1.0f;
    public SynthWaveType WaveType { get; set; } = SynthWaveType.PianoEP;

    // ADSR 타이밍 (초)
    public float AttackTime { get; set; } = 0.008f;
    public float DecayTime { get; set; } = 0.08f;
    public float SustainLevel { get; set; } = 0.65f;
    public float ReleaseTime { get; set; } = 0.15f;

    public bool IsActive => _state != EnvelopeState.Idle;

    // Karplus-Strong 통기타 물리 모델링 링 버퍼
    private float[]? _ksBuffer;
    private int _ksLength;
    private int _ksIndex;

    // 일렉기타 앰프 캐비닛 LowPass 필터 상태
    private float _ampFilterState;

    public SynthVoice(int sampleRate)
    {
        _sampleRate = sampleRate;
    }

    public void NoteOn(int midiNote, double frequency, float velocity, SynthWaveType waveType)
    {
        MidiNote = midiNote;
        Frequency = frequency;
        Velocity = Math.Clamp(velocity, 0.1f, 1.0f);
        WaveType = waveType;
        _phaseIncrement = frequency / _sampleRate;
        _phase = 0;
        _state = EnvelopeState.Attack;

        // 음색별 ADSR 및 합성 파라미터 프리셋
        switch (waveType)
        {
            case SynthWaveType.AcousticGuitar:
                // 통기타 (Karplus-Strong): 피크 타현 어택 + 자연스러운 현 공명 감쇄
                AttackTime = 0.002f;
                DecayTime = 1.25f;
                SustainLevel = 0.10f;
                ReleaseTime = 0.35f;

                int n = Math.Max(4, (int)(_sampleRate / Math.Max(20.0, frequency)));
                if (_ksBuffer == null || _ksBuffer.Length < n)
                {
                    _ksBuffer = new float[n];
                }
                _ksLength = n;
                _ksIndex = 0;

                // 손가락/피크 찰현 노이즈(30%) + 기본 배음(70%) 초기화
                for (int i = 0; i < n; i++)
                {
                    float noise = (float)(Random.Shared.NextDouble() * 2.0 - 1.0);
                    float fund = (float)Math.Sin(2.0 * Math.PI * i / n);
                    _ksBuffer[i] = fund * 0.70f + noise * 0.30f;
                }
                break;

            case SynthWaveType.ElectricGuitar:
                // 빈티지 일렉 기타: 앰프 서스테인과 굵고 따뜻한 톤
                AttackTime = 0.005f;
                DecayTime = 0.95f;
                SustainLevel = 0.45f;
                ReleaseTime = 0.38f;
                _ampFilterState = 0f;
                break;

            case SynthWaveType.AcousticHarp:
                AttackTime = 0.004f;
                DecayTime = 0.65f;
                SustainLevel = 0.05f; // 타현음(플럭)은 서스테인이 거의 없이 감쇄
                ReleaseTime = 0.25f;
                break;

            case SynthWaveType.PianoEP:
            default:
                AttackTime = 0.003f;
                DecayTime = 0.85f;
                SustainLevel = 0.35f;
                ReleaseTime = 0.38f;
                break;
        }
    }

    public void NoteOff()
    {
        if (_state != EnvelopeState.Idle)
        {
            _state = EnvelopeState.Release;
        }
    }

    /// <summary>
    /// 단일 샘플 생성 (-1.0f ~ +1.0f)
    /// </summary>
    public float GetNextSample()
    {
        if (_state == EnvelopeState.Idle) return 0f;

        // ADSR 엔벨로프 계산
        float attackRate = 1.0f / (_sampleRate * Math.Max(0.001f, AttackTime));
        float decayRate = (1.0f - SustainLevel) / (_sampleRate * Math.Max(0.001f, DecayTime));
        float releaseRate = SustainLevel / (_sampleRate * Math.Max(0.001f, ReleaseTime));

        switch (_state)
        {
            case EnvelopeState.Attack:
                _envelope += attackRate;
                if (_envelope >= 1.0f)
                {
                    _envelope = 1.0f;
                    _state = EnvelopeState.Decay;
                }
                break;
            case EnvelopeState.Decay:
                _envelope -= decayRate;
                if (_envelope <= SustainLevel)
                {
                    _envelope = SustainLevel;
                    _state = EnvelopeState.Sustain;
                }
                break;
            case EnvelopeState.Sustain:
                _envelope = SustainLevel;
                break;
            case EnvelopeState.Release:
                _envelope -= releaseRate;
                if (_envelope <= 0.001f)
                {
                    _envelope = 0f;
                    _state = EnvelopeState.Idle;
                }
                break;
        }

        // 파형 생성
        float rawWave = GenerateWaveform();

        // 위상 진행
        _phase += _phaseIncrement;
        if (_phase >= 1.0) _phase -= 1.0;

        return rawWave * _envelope * Velocity;
    }

    private float GenerateWaveform()
    {
        switch (WaveType)
        {
            case SynthWaveType.AcousticGuitar:
                // 통기타 (Karplus-Strong): 링 버퍼 피드백 순환 + 인접 샘플 저역 필터링
                if (_ksBuffer != null && _ksLength > 0)
                {
                    float current = _ksBuffer[_ksIndex];
                    float next = _ksBuffer[(_ksIndex + 1) % _ksLength];
                    // 현의 자연스러운 진동 감쇄 피드백 계수 (0.994f)
                    float feedback = (current + next) * 0.5f * 0.994f;
                    _ksBuffer[_ksIndex] = feedback;
                    _ksIndex = (_ksIndex + 1) % _ksLength;
                    return current * 1.15f;
                }
                return (float)Math.Sin(2.0 * Math.PI * _phase);

            case SynthWaveType.ElectricGuitar:
                // 빈티지 일렉기타: 톱니파 + 비대칭 펄스파 + 5도 파워코드 배음
                float saw = (float)(2.0 * (_phase - Math.Floor(_phase + 0.5)));
                float sq = _phase < 0.45 ? 0.65f : -0.65f;
                float fifth = (float)Math.Sin(3.0 * Math.PI * _phase) * 0.22f;
                float raw = saw * 0.55f + sq * 0.25f + fifth;

                // 진공관 튜브 앰프 오버드라이브 (비선형 소프트 클리핑)
                float driven = (float)Math.Tanh(raw * 2.2);

                // 기타 캐비닛 스피커 하이컷 필터 (따뜻하고 묵직한 톤)
                _ampFilterState += 0.26f * (driven - _ampFilterState);
                return _ampFilterState * 1.25f;

            case SynthWaveType.AcousticHarp:
                // 하프/타현: 사인파 + 삼각파 + 고차 미세 배음으로 현의 찰현음 모사
                float h1 = (float)Math.Sin(2.0 * Math.PI * _phase);
                float h3 = (float)Math.Sin(6.0 * Math.PI * _phase) * 0.25f;
                float tri = (float)(4.0 * Math.Abs(_phase - Math.Floor(_phase + 0.5)) - 1.0) * 0.2f;
                return (h1 + h3 + tri) * 0.75f;

            case SynthWaveType.PianoEP:
            default:
                // 영롱하고 부드러운 피아노: 기음(100%) + 2차 온화한 배음(36%) + 3차 타현 배음(16%) + 4차 맑은 배음(6%)
                float p1 = (float)Math.Sin(2.0 * Math.PI * _phase);
                float p2 = (float)Math.Sin(4.0 * Math.PI * _phase) * 0.36f;
                float p3 = (float)Math.Sin(6.0 * Math.PI * _phase) * 0.16f;
                float p4 = (float)Math.Sin(8.0 * Math.PI * _phase) * 0.06f;
                float p5 = (float)Math.Sin(10.0 * Math.PI * _phase) * 0.02f;
                return (p1 + p2 + p3 + p4 + p5) * 0.62f;
        }
    }
}
