using NAudio.Wave;

namespace Common.Audio.Synth;

/// <summary>
/// 다성음(Polyphony) 신디사이저 오디오 샘플 프로바이더
/// </summary>
public class SynthSampleProvider : ISampleProvider
{
    private readonly List<SynthVoice> _voicePool = new();
    private readonly object _lock = new();
    private readonly int _sampleRate = 44100;
    private const int MaxVoices = 16;

    public WaveFormat WaveFormat { get; }
    public float MasterVolume { get; set; } = 0.85f;
    public SynthWaveType WaveType { get; set; } = SynthWaveType.PianoEP;

    public SynthSampleProvider(int sampleRate = 44100)
    {
        _sampleRate = sampleRate;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2); // 44.1kHz Stereo

        for (int i = 0; i < MaxVoices; i++)
        {
            _voicePool.Add(new SynthVoice(_sampleRate));
        }
    }

    public void NoteOn(int midiNote, double frequency, float velocity = 0.9f)
    {
        lock (_lock)
        {
            // 같은 음을 재생 중인 보이스가 있으면 먼저 릴리즈
            foreach (var v in _voicePool)
            {
                if (v.IsActive && v.MidiNote == midiNote)
                {
                    v.NoteOff();
                }
            }

            // 비어있는 보이스 찾기
            var voice = _voicePool.FirstOrDefault(v => !v.IsActive);
            if (voice == null)
            {
                // 모든 보이스가 차 있으면 가장 오래된(릴리즈 중인) 보이스 강제 재할당
                voice = _voicePool.FirstOrDefault(v => v.IsActive);
            }

            voice?.NoteOn(midiNote, frequency, velocity, WaveType);
        }
    }

    public void NoteOff(int midiNote)
    {
        lock (_lock)
        {
            foreach (var v in _voicePool)
            {
                if (v.IsActive && v.MidiNote == midiNote)
                {
                    v.NoteOff();
                }
            }
        }
    }

    public void AllNotesOff()
    {
        lock (_lock)
        {
            foreach (var v in _voicePool)
            {
                if (v.IsActive)
                {
                    v.NoteOff();
                }
            }
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        // 버퍼 초기화
        Array.Clear(buffer, offset, count);

        lock (_lock)
        {
            int samplesPerChannel = count / 2; // Stereo

            for (int i = 0; i < samplesPerChannel; i++)
            {
                float mixedSample = 0f;

                foreach (var voice in _voicePool)
                {
                    if (voice.IsActive)
                    {
                        mixedSample += voice.GetNextSample();
                    }
                }

                // 마스터 볼륨 및 소프트 리미팅(클리핑 방지)
                mixedSample *= MasterVolume * 0.4f;
                mixedSample = Math.Clamp(mixedSample, -1.0f, 1.0f);

                // Stereo 좌/우 채널 복제
                buffer[offset + (i * 2)] = mixedSample;
                buffer[offset + (i * 2) + 1] = mixedSample;
            }
        }

        return count;
    }
}
