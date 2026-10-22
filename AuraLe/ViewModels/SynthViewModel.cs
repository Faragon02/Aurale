using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AuraLe.Models;
using Common.Audio.Analysis;
using Common.Audio.Synth;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuraLe.ViewModels;

/// <summary>
/// QWERTY 가상 신디사이저 및 악보 뷰모델
/// </summary>
public partial class SynthViewModel : ObservableObject, IDisposable
{
    private readonly SynthEngine _synthEngine;
    private readonly AudioToNoteTranscriber _transcriber;
    private readonly Dictionary<Key, QwertyKeyNote> _keyLookup = new();
    private readonly Dictionary<char, QwertyKeyNote> _charLookup = new();
    private readonly HashSet<Key> _currentlyHeldKeys = new();
    private List<TranscribedNote> _lastTranscribedNotes = new();
    private CancellationTokenSource? _autoPlayCts;

    private DispatcherTimer? _autoPlayTimer;
    private List<char> _autoPlayNotes = new();
    private int _autoPlayIndex = 0;
    private bool _isDisposed;

    #region Observable Properties

    [ObservableProperty]
    private ObservableCollection<QwertyKeyNote> _highOctaveKeys = new();

    [ObservableProperty]
    private ObservableCollection<QwertyKeyNote> _midOctaveKeys = new();

    [ObservableProperty]
    private ObservableCollection<QwertyKeyNote> _lowOctaveKeys = new();

    [ObservableProperty]
    private SynthWaveType _currentWaveType = SynthWaveType.PianoEP;

    public bool IsPianoEP => CurrentWaveType == SynthWaveType.PianoEP;
    public bool IsAcousticHarp => CurrentWaveType == SynthWaveType.AcousticHarp;
    public bool IsAcousticGuitar => CurrentWaveType == SynthWaveType.AcousticGuitar;
    public bool IsElectricGuitar => CurrentWaveType == SynthWaveType.ElectricGuitar;

    [ObservableProperty]
    private bool _isSharpMode;

    [ObservableProperty]
    private int _playSpeedMs = 320; // 재생 간격 (ms) - 기본 320ms로 여유로운 박자

    partial void OnPlaySpeedMsChanged(int value)
    {
        if (_autoPlayTimer != null)
        {
            _autoPlayTimer.Interval = TimeSpan.FromMilliseconds(Math.Clamp(value, 120, 800));
        }
    }

    [ObservableProperty]
    private string _sheetText = "// 음원을 선택하고 [음원 멜로디 추출]을 누르면 여기에 21키 QWERTY 악보가 자동 생성됩니다.";

    [ObservableProperty]
    private bool _isAutoPlaying;

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private bool _isTranscribing;

    [ObservableProperty]
    private TranscriptionMode _transcribeMode = TranscriptionMode.BasicPitchAI;

    [ObservableProperty]
    private int _transcribeProgress;

    [ObservableProperty]
    private string _transcribeStatus = "음원을 선택하고 [음원 멜로디 추출]을 누르면 Spotify AI가 21키 QWERTY 악보를 생성합니다.";

    [ObservableProperty]
    private string _targetTrackTitle = "선택된 곡 없음";

    private string? _targetTrackFilePath;

    #endregion

    public SynthViewModel()
    {
        _synthEngine = new SynthEngine { WaveType = SynthWaveType.PianoEP };
        _transcriber = new AudioToNoteTranscriber();

        InitializeKeyboardMapping();
    }

    private void InitializeKeyboardMapping()
    {
        // High Octave (C5 ~ B5: MIDI 72 ~ 83)
        var high = new[]
        {
            new QwertyKeyNote(Key.Q, 'Q', "도", "C5", 72, "High"),
            new QwertyKeyNote(Key.W, 'W', "레", "D5", 74, "High"),
            new QwertyKeyNote(Key.E, 'E', "미", "E5", 76, "High"),
            new QwertyKeyNote(Key.R, 'R', "파", "F5", 77, "High"),
            new QwertyKeyNote(Key.T, 'T', "솔", "G5", 79, "High"),
            new QwertyKeyNote(Key.Y, 'Y', "라", "A5", 81, "High"),
            new QwertyKeyNote(Key.U, 'U', "시", "B5", 83, "High")
        };

        // Mid Octave (C4 ~ B4: MIDI 60 ~ 71)
        var mid = new[]
        {
            new QwertyKeyNote(Key.A, 'A', "도", "C4", 60, "Mid"),
            new QwertyKeyNote(Key.S, 'S', "레", "D4", 62, "Mid"),
            new QwertyKeyNote(Key.D, 'D', "미", "E4", 64, "Mid"),
            new QwertyKeyNote(Key.F, 'F', "파", "F4", 65, "Mid"),
            new QwertyKeyNote(Key.G, 'G', "솔", "G4", 67, "Mid"),
            new QwertyKeyNote(Key.H, 'H', "라", "A4", 69, "Mid"),
            new QwertyKeyNote(Key.J, 'J', "시", "B4", 71, "Mid")
        };

        // Low Octave (C3 ~ B3: MIDI 48 ~ 59)
        var low = new[]
        {
            new QwertyKeyNote(Key.Z, 'Z', "도", "C3", 48, "Low"),
            new QwertyKeyNote(Key.X, 'X', "레", "D3", 50, "Low"),
            new QwertyKeyNote(Key.C, 'C', "미", "E3", 52, "Low"),
            new QwertyKeyNote(Key.V, 'V', "파", "F3", 53, "Low"),
            new QwertyKeyNote(Key.B, 'B', "솔", "G3", 55, "Low"),
            new QwertyKeyNote(Key.N, 'N', "라", "A3", 57, "Low"),
            new QwertyKeyNote(Key.M, 'M', "시", "B3", 59, "Low")
        };

        HighOctaveKeys = new ObservableCollection<QwertyKeyNote>(high);
        MidOctaveKeys = new ObservableCollection<QwertyKeyNote>(mid);
        LowOctaveKeys = new ObservableCollection<QwertyKeyNote>(low);

        foreach (var k in high.Concat(mid).Concat(low))
        {
            _keyLookup[k.Key] = k;
            _charLookup[k.KeyChar] = k;
        }
    }

    /// <summary>
    /// 메인 윈도우에서 현재 곡 정보 동기화
    /// </summary>
    public void SetTargetTrack(TrackItem? track)
    {
        if (track != null && File.Exists(track.FilePath))
        {
            TargetTrackTitle = track.Title;
            _targetTrackFilePath = track.FilePath;
            string modeName = TranscribeMode switch
            {
                TranscriptionMode.BasicPitchAI => "🤖 AI 모드",
                TranscriptionMode.PianoSolo => "🎹 피아노 DSP",
                _ => "🎤 보컬 DSP"
            };
            TranscribeStatus = $"선택된 음원: '{track.Title}' [{modeName} 준비됨]";
        }
        else
        {
            TargetTrackTitle = "선택된 곡 없음";
            _targetTrackFilePath = null;
            TranscribeStatus = "플레이리스트에서 음원을 선택하면 멜로디를 추출할 수 있습니다.";
        }
    }

    #region Realtime Key Handling

    public void HandleKeyDown(Key key, bool isShiftDown = false)
    {
        if (_currentlyHeldKeys.Contains(key)) return; // 중복 타건(키 반복) 무시

        if (_keyLookup.TryGetValue(key, out var keyNote))
        {
            _currentlyHeldKeys.Add(key);
            keyNote.IsPressed = true;

            int targetMidi = keyNote.MidiNote;
            if (IsSharpMode || isShiftDown)
            {
                targetMidi += 1; // 반음 올림
            }

            _synthEngine.NoteOn(targetMidi);

            // 레코딩 모드 시 실시간 악보 기록
            if (IsRecording)
            {
                RecordKey(keyNote.KeyChar);
            }
        }
    }

    public void HandleKeyUp(Key key)
    {
        if (_keyLookup.TryGetValue(key, out var keyNote))
        {
            _currentlyHeldKeys.Remove(key);
            keyNote.IsPressed = false;
            _synthEngine.NoteOff(keyNote.MidiNote);
            _synthEngine.NoteOff(keyNote.MidiNote + 1);
        }
    }

    private void RecordKey(char keyChar)
    {
        if (string.IsNullOrWhiteSpace(SheetText))
        {
            SheetText = keyChar.ToString();
        }
        else
        {
            SheetText += " " + keyChar;
        }
    }

    #endregion

    #region Timbre & Mode Commands

    partial void OnCurrentWaveTypeChanged(SynthWaveType value)
    {
        _synthEngine.WaveType = value;
        OnPropertyChanged(nameof(IsPianoEP));
        OnPropertyChanged(nameof(IsAcousticHarp));
        OnPropertyChanged(nameof(IsAcousticGuitar));
        OnPropertyChanged(nameof(IsElectricGuitar));
    }

    [RelayCommand]
    public void SelectWaveType(string type)
    {
        CurrentWaveType = type?.ToLowerInvariant() switch
        {
            "harp" => SynthWaveType.AcousticHarp,
            "guitar" or "acoustic_guitar" or "acoustic" => SynthWaveType.AcousticGuitar,
            "electric" or "electric_guitar" or "elec" => SynthWaveType.ElectricGuitar,
            _ => SynthWaveType.PianoEP
        };
    }

    [RelayCommand]
    public void ToggleSharpMode()
    {
        IsSharpMode = !IsSharpMode;
    }

    [RelayCommand]
    public void PlayNoteDirect(QwertyKeyNote note)
    {
        if (note == null) return;
        note.IsPressed = true;
        _synthEngine.NoteOn(note.MidiNote);

        // 터치/마우스 클릭 시 180ms 후 자동 릴리즈
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        timer.Tick += (s, e) =>
        {
            timer.Stop();
            note.IsPressed = false;
            _synthEngine.NoteOff(note.MidiNote);
        };
        timer.Start();

        if (IsRecording)
        {
            RecordKey(note.KeyChar);
        }
    }

    #endregion

    #region Audio to QWERTY Transcription

    [RelayCommand]
    public async Task TranscribeCurrentTrackAsync()
    {
        if (string.IsNullOrWhiteSpace(_targetTrackFilePath) || !File.Exists(_targetTrackFilePath))
        {
            TranscribeStatus = "오류: 추출할 음원 파일을 찾을 수 없습니다.";
            return;
        }

        if (IsTranscribing) return;

        IsTranscribing = true;
        TranscribeProgress = 0;
        const string modeLabel = "Spotify AI";
        TranscribeStatus = $"'{TargetTrackTitle}' ({modeLabel}) 멜로디 분석 중... (0%)";

        try
        {
            var progressReporter = new Progress<int>(p =>
            {
                TranscribeProgress = p;
                TranscribeStatus = $"'{TargetTrackTitle}' ({modeLabel}) 멜로디 분석 중... ({p}%)";
            });

            var (notes, sheet) = await _transcriber.TranscribeAudioAsync(_targetTrackFilePath, TranscribeMode, progressReporter);

            if (!string.IsNullOrWhiteSpace(sheet))
            {
                _lastTranscribedNotes = notes;
                SheetText = sheet;
                TranscribeStatus = $"[{modeLabel} 완료] 총 {notes.Count}개 노트를 정밀 악보로 변환했습니다! (▶ 자동 연주 시 원곡 실제 박자로 재생)";
            }
            else
            {
                _lastTranscribedNotes.Clear();
                TranscribeStatus = $"[{modeLabel} 완료] 감지된 멜로디 노트가 충분하지 않습니다.";
            }
        }
        catch (Exception ex)
        {
            TranscribeStatus = $"분석 실패: {ex.Message}";
        }
        finally
        {
            IsTranscribing = false;
        }
    }

    #endregion

    #region Sheet Playback & Clipboard

    [RelayCommand]
    public void AutoPlaySheet()
    {
        if (IsAutoPlaying)
        {
            StopAutoPlay();
            return;
        }

        if (string.IsNullOrWhiteSpace(SheetText)) return;

        // 1. 추출된 정밀 타임스탬프 노트가 있고, 악보와 동기화된 경우 -> 실제 원곡 박자(Realtime)로 연주!
        if (_lastTranscribedNotes.Count > 0)
        {
            StartRealtimePlayback(_lastTranscribedNotes);
        }
        else
        {
            // 2. 수동 입력 또는 샘플 악보 텍스트 -> 템포 슬라이더 기반 순차 연주
            StartTextSheetPlayback();
        }
    }

    private void StartTextSheetPlayback()
    {
        _autoPlayNotes = SheetText.ToUpperInvariant()
            .Where(c => _charLookup.ContainsKey(c) || c == '-')
            .ToList();

        if (_autoPlayNotes.Count == 0) return;

        IsAutoPlaying = true;
        _autoPlayIndex = 0;

        int interval = Math.Clamp(PlaySpeedMs, 120, 800);
        _autoPlayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(interval) };
        _autoPlayTimer.Tick += (s, e) =>
        {
            if (_autoPlayIndex >= _autoPlayNotes.Count)
            {
                StopAutoPlay();
                return;
            }

            char c = _autoPlayNotes[_autoPlayIndex++];
            if (c == '-') return;

            if (_charLookup.TryGetValue(c, out var keyNote))
            {
                keyNote.IsPressed = true;
                _synthEngine.NoteOn(keyNote.MidiNote);

                int holdTime = (int)(Math.Clamp(PlaySpeedMs, 120, 800) * 0.70);
                var offTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(holdTime) };
                offTimer.Tick += (ts, te) =>
                {
                    offTimer.Stop();
                    keyNote.IsPressed = false;
                    _synthEngine.NoteOff(keyNote.MidiNote);
                };
                offTimer.Start();
            }
        };

        _autoPlayTimer.Start();
    }

    private async void StartRealtimePlayback(List<TranscribedNote> notes)
    {
        IsAutoPlaying = true;
        _autoPlayCts = new CancellationTokenSource();
        var ct = _autoPlayCts.Token;

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int index = 0;

            while (index < notes.Count && !ct.IsCancellationRequested)
            {
                var note = notes[index];
                double targetSec = note.StartTime.TotalSeconds;
                double currentSec = sw.Elapsed.TotalSeconds;

                if (targetSec > currentSec)
                {
                    int delayMs = (int)((targetSec - currentSec) * 1000.0);
                    if (delayMs > 2)
                    {
                        await Task.Delay(delayMs, ct);
                    }
                }

                if (ct.IsCancellationRequested) break;

                // 동시 타건(화음) 지원: 50ms 이내에 시작하는 음들을 함께 연주
                while (index < notes.Count && Math.Abs(notes[index].StartTime.TotalSeconds - targetSec) < 0.05)
                {
                    var n = notes[index];
                    PlaySingleNoteWithDuration(n, ct);
                    index++;
                }
            }

            await Task.Delay(1000, ct);
        }
        catch (OperationCanceledException) { }
        finally
        {
            StopAutoPlay();
        }
    }

    private void PlaySingleNoteWithDuration(TranscribedNote note, CancellationToken ct)
    {
        if (_charLookup.TryGetValue(note.QwertyKey, out var keyNote))
        {
            keyNote.IsPressed = true;
            _synthEngine.NoteOn(note.MidiNote);

            int durMs = Math.Clamp((int)note.Duration.TotalMilliseconds, 80, 2000);
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(durMs, ct);
                }
                catch { }
                finally
                {
                    keyNote.IsPressed = false;
                    _synthEngine.NoteOff(note.MidiNote);
                }
            }, ct);
        }
    }



    [RelayCommand]
    public void StopAutoPlay()
    {
        if (_autoPlayCts != null)
        {
            _autoPlayCts.Cancel();
            _autoPlayCts.Dispose();
            _autoPlayCts = null;
        }

        if (_autoPlayTimer != null)
        {
            _autoPlayTimer.Stop();
            _autoPlayTimer = null;
        }

        IsAutoPlaying = false;
        _synthEngine.AllNotesOff();

        foreach (var k in _keyLookup.Values)
        {
            k.IsPressed = false;
        }
    }

    [RelayCommand]
    public void ToggleRecording()
    {
        IsRecording = !IsRecording;
        if (IsRecording && string.IsNullOrWhiteSpace(SheetText))
        {
            SheetText = string.Empty;
        }
    }

    [RelayCommand]
    public void CopyToClipboard()
    {
        if (!string.IsNullOrWhiteSpace(SheetText))
        {
            Clipboard.SetText(SheetText);
            TranscribeStatus = "악보가 클립보드에 복사되었습니다! 게임 채팅/에디터에 붙여넣기(Ctrl+V)하세요.";
        }
    }

    [RelayCommand]
    public void ClearSheet()
    {
        SheetText = string.Empty;
        _lastTranscribedNotes.Clear();
        StopAutoPlay();
    }

    #endregion

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        StopAutoPlay();
        _synthEngine.Dispose();
    }
}
