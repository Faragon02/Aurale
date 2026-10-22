using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Common.Audio.Synth;

/// <summary>
/// 초저지연 실시간 가상 신디사이저 엔진 (NAudio 기반)
/// </summary>
public class SynthEngine : IDisposable
{
    private IWavePlayer? _outputDevice;
    private readonly SynthSampleProvider _sampleProvider;
    private bool _isDisposed;
    private bool _isInitialized;

    public SynthWaveType WaveType
    {
        get => _sampleProvider.WaveType;
        set => _sampleProvider.WaveType = value;
    }

    public float Volume
    {
        get => _sampleProvider.MasterVolume;
        set => _sampleProvider.MasterVolume = Math.Clamp(value, 0f, 1f);
    }

    public SynthEngine()
    {
        _sampleProvider = new SynthSampleProvider(44100);
    }

    /// <summary>
    /// 저지연 오디오 디바이스 초기화 및 재생 시작
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized) return;

        try
        {
            // Windows WASAPI 공유 모드 (초저지연 40~50ms 버퍼)
            var wasapi = new WasapiOut(AudioClientShareMode.Shared, 50);
            wasapi.Init(_sampleProvider);
            wasapi.Play();
            _outputDevice = wasapi;
            _isInitialized = true;
        }
        catch
        {
            // WASAPI 실패 시 DirectSound/WaveOut으로 안전 폴백
            var waveOut = new WaveOutEvent { DesiredLatency = 60 };
            waveOut.Init(_sampleProvider);
            waveOut.Play();
            _outputDevice = waveOut;
            _isInitialized = true;
        }
    }

    public void NoteOn(int midiNote, float velocity = 0.9f)
    {
        if (!_isInitialized) Initialize();
        double freq = NoteFrequencyTable.MidiToFrequency(midiNote);
        _sampleProvider.NoteOn(midiNote, freq, velocity);
    }

    public void NoteOff(int midiNote)
    {
        _sampleProvider.NoteOff(midiNote);
    }

    public void AllNotesOff()
    {
        _sampleProvider.AllNotesOff();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        try
        {
            _sampleProvider.AllNotesOff();
            _outputDevice?.Stop();
            _outputDevice?.Dispose();
            _outputDevice = null;
        }
        catch
        {
            // 무시
        }
    }
}
