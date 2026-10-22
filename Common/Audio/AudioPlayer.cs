using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Common.Audio;

/// <summary>
/// WASAPI 기반 무결점 고음질 오디오 재생 엔진 (창 드래그 시 끊김 완전 방지)
/// </summary>
public class AudioPlayer : IDisposable
{
    private IWavePlayer? _outputDevice;
    private AudioFileReader? _audioFile;
    private float _volume = 0.8f;
    private bool _isDisposed;
    private bool _isManualStop;

    /// <summary>
    /// 현재 재생 중인 파일 경로
    /// </summary>
    public string? CurrentFilePath { get; private set; }

    /// <summary>
    /// 현재 재생 상태 (Stopped, Playing, Paused)
    /// </summary>
    public PlaybackState State
    {
        get
        {
            if (_outputDevice == null) return PlaybackState.Stopped;
            return _outputDevice.PlaybackState switch
            {
                NAudio.Wave.PlaybackState.Playing => PlaybackState.Playing,
                NAudio.Wave.PlaybackState.Paused => PlaybackState.Paused,
                _ => PlaybackState.Stopped
            };
        }
    }

    /// <summary>
    /// 현재 재생 중 여부
    /// </summary>
    public bool IsPlaying => State == PlaybackState.Playing;

    /// <summary>
    /// 현재 재생 위치
    /// </summary>
    public TimeSpan CurrentTime
    {
        get => _audioFile?.CurrentTime ?? TimeSpan.Zero;
        set
        {
            if (_audioFile != null)
            {
                var clamped = value < TimeSpan.Zero ? TimeSpan.Zero : (value > TotalTime ? TotalTime : value);
                _audioFile.CurrentTime = clamped;
            }
        }
    }

    /// <summary>
    /// 전체 음원 길이
    /// </summary>
    public TimeSpan TotalTime => _audioFile?.TotalTime ?? TimeSpan.Zero;

    /// <summary>
    /// 볼륨 (0.0 ~ 1.0)
    /// </summary>
    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0.0f, 1.0f);
            if (_audioFile != null)
            {
                _audioFile.Volume = _volume;
            }
        }
    }

    /// <summary>
    /// 재생 상태가 변경되었을 때 발생하는 이벤트
    /// </summary>
    public event EventHandler<PlaybackState>? StateChanged;

    /// <summary>
    /// 트랙 재생이 끝났을 때(자연 종료) 발생하는 이벤트
    /// </summary>
    public event EventHandler? TrackEnded;

    /// <summary>
    /// 음원 파일 열기
    /// </summary>
    public void Open(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("오디오 파일을 찾을 수 없습니다.", filePath);
        }

        Stop();
        DisposeAudioResources();

        CurrentFilePath = filePath;
        _audioFile = new AudioFileReader(filePath)
        {
            Volume = _volume
        };

        // WASAPI(Windows Audio Session API) 공유 모드 사용
        // 창 드래그, UI 렉 등과 완전히 독립된 윈도우 오디오 서비스 스레드에서 재생되어 절대 끊기지 않음
        _outputDevice = new WasapiOut(AudioClientShareMode.Shared, 200);
        _outputDevice.Init(_audioFile);
        _outputDevice.PlaybackStopped += OnPlaybackStopped;
    }

    /// <summary>
    /// 재생 시작 또는 재개
    /// </summary>
    public void Play()
    {
        if (_outputDevice == null && CurrentFilePath != null)
        {
            Open(CurrentFilePath);
        }

        if (_outputDevice != null)
        {
            _isManualStop = false;
            _outputDevice.Play();
            StateChanged?.Invoke(this, PlaybackState.Playing);
        }
    }

    /// <summary>
    /// 일시정지
    /// </summary>
    public void Pause()
    {
        if (_outputDevice != null && State == PlaybackState.Playing)
        {
            _outputDevice.Pause();
            StateChanged?.Invoke(this, PlaybackState.Paused);
        }
    }

    /// <summary>
    /// 정지 및 재생 위치 처음으로 초기화
    /// </summary>
    public void Stop()
    {
        if (_outputDevice != null)
        {
            _isManualStop = true;
            _outputDevice.Stop();
            if (_audioFile != null)
            {
                _audioFile.Position = 0;
            }
            StateChanged?.Invoke(this, PlaybackState.Stopped);
        }
    }

    /// <summary>
    /// 특정 위치로 이동 (재생 중일 때는 즉시 해당 위치에서 연속 재생)
    /// </summary>
    public void Seek(TimeSpan targetTime)
    {
        if (_audioFile == null) return;

        var clamped = targetTime < TimeSpan.Zero ? TimeSpan.Zero : (targetTime > TotalTime ? TotalTime : targetTime);

        bool wasPlaying = State == PlaybackState.Playing;
        if (wasPlaying && _outputDevice != null)
        {
            _outputDevice.Pause();
        }

        try { File.AppendAllText(@"d:\WinTools\seek.log", $"[AudioPlayer.Seek] Req={targetTime.TotalSeconds:F1}s, Before={_audioFile.CurrentTime.TotalSeconds:F1}s, State={State}\n"); } catch {}

        _audioFile.CurrentTime = clamped;

        try { File.AppendAllText(@"d:\WinTools\seek.log", $"[AudioPlayer.Seek] After={_audioFile.CurrentTime.TotalSeconds:F1}s\n"); } catch {}

        if (wasPlaying && _outputDevice != null)
        {
            _outputDevice.Play();
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        // 자연스럽게 곡이 끝난 경우 판정:
        // 수동 정지가 아니고, 파일 끝(EOF)에 도달했거나 남은 시간이 0.5초 이내인 경우
        bool isNaturalEnd = !_isManualStop && _audioFile != null &&
            (_audioFile.Position >= _audioFile.Length - 65536 || (TotalTime - CurrentTime).TotalSeconds < 0.5);

        if (isNaturalEnd && _audioFile != null)
        {
            _audioFile.Position = 0;
            StateChanged?.Invoke(this, PlaybackState.Stopped);
            TrackEnded?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            StateChanged?.Invoke(this, PlaybackState.Stopped);
        }
    }

    private void DisposeAudioResources()
    {
        if (_outputDevice != null)
        {
            _outputDevice.PlaybackStopped -= OnPlaybackStopped;
            _outputDevice.Dispose();
            _outputDevice = null;
        }

        if (_audioFile != null)
        {
            _audioFile.Dispose();
            _audioFile = null;
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        DisposeAudioResources();
        GC.SuppressFinalize(this);
    }
}
