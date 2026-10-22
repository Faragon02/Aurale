using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using AuraLe.Models;
using AuraLe.Services;
using Common.Audio;
using Common.IO;
using Common.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuraLe.ViewModels;

/// <summary>
/// 메인 화면 뷰 구분 (4.0 공용창 내 탭 전환)
/// </summary>
public enum ActiveView
{
    Home,
    Player,
    Download,
    Settings,
    Synth
}

/// <summary>
/// AuraLe 메인 뷰모델 (재생, 다운로드, 설정, 플레이리스트 총괄)
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly AudioPlayer _player;
    private readonly DispatcherTimer _progressTimer;
    private readonly string _settingsFilePath;
    private AppSettings _settings;
    private bool _isDisposed;

    #region Observable Properties

    [ObservableProperty]
    private ActiveView _currentView = ActiveView.Player;

    /// <summary>
    /// 상단 공용 타이틀 (홈: AuraLe - CASSETTE, 재생: AuraLe - CASSETTE DECK, 다운로드: AuraLe - TAPE RECORDER, 설정: AuraLe - CONFIGURATION)
    /// </summary>
    public string HeaderTitle => CurrentView switch
    {
        ActiveView.Home => "AuraLe - CASSETTE",
        ActiveView.Download => "AuraLe - TAPE RECORDER",
        ActiveView.Settings => "AuraLe - CONFIGURATION",
        ActiveView.Synth => "AuraLe - CASSETTE SYNTH",
        _ => "AuraLe - CASSETTE DECK"
    };

    public bool IsHomeView => CurrentView == ActiveView.Home;
    public bool IsPlayerView => CurrentView == ActiveView.Player;
    public bool IsDownloadView => CurrentView == ActiveView.Download;
    public bool IsSettingsView => CurrentView == ActiveView.Settings;
    public bool IsSynthView => CurrentView == ActiveView.Synth;
    public bool HasBackgroundImage => !string.IsNullOrWhiteSpace(BackgroundImagePath) && File.Exists(BackgroundImagePath);

    [ObservableProperty]
    private SynthViewModel _synth = new();

    [ObservableProperty]
    private ObservableCollection<TrackItem> _playlist = new();

    [ObservableProperty]
    private TrackItem? _currentTrack;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private double _currentPositionSeconds;

    [ObservableProperty]
    private double _totalDurationSeconds;

    [ObservableProperty]
    private string _currentTimeText = "00:00";

    [ObservableProperty]
    private string _totalTimeText = "00:00";

    [ObservableProperty]
    private float _volume = 0.8f;

    [ObservableProperty]
    private bool _isDrawerOpen = true;

    [ObservableProperty]
    private bool _isRecMode;

    /// <summary>
    /// 사용자가 시크 슬라이더를 잡고 드래그(탐색) 중인지 여부 (타이머 간섭 방지)
    /// </summary>
    public bool IsUserSeeking { get; set; }

    // 환경 설정 (P2)
    [ObservableProperty]
    private double _windowOpacity = 1.0;

    [ObservableProperty]
    private string? _backgroundImagePath;

    [ObservableProperty]
    private double _backgroundImageOffsetX;

    [ObservableProperty]
    private string _statusMessage = "AuraLe CASSETTE READY";

    [ObservableProperty]
    private bool _isShuffle;

    [ObservableProperty]
    private RepeatMode _repeatMode = RepeatMode.Off;

    public string RepeatButtonText => RepeatMode switch
    {
        RepeatMode.RepeatAll => "🔁",
        RepeatMode.RepeatOne => "🔂",
        _ => "🔁"
    };

    public string RepeatToolTip => RepeatMode switch
    {
        RepeatMode.RepeatAll => "반복: 전체 연속 재생 (ALL)",
        RepeatMode.RepeatOne => "반복: 한 곡 반복 (ONE)",
        _ => "반복: 끔 (1곡 재생 후 정지)"
    };

    public string ShuffleToolTip => IsShuffle ? "셔플: 켬 (랜덤 재생)" : "셔플: 끔 (순서대로)";

    public System.Windows.Media.Brush RepeatButtonForeground => RepeatMode switch
    {
        RepeatMode.RepeatAll => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xF0, 0xFF)),
        RepeatMode.RepeatOne => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xE6, 0x00)),
        _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6E, 0x80, 0x96))
    };

    public System.Windows.Media.Brush RepeatButtonBorder => RepeatMode switch
    {
        RepeatMode.RepeatAll => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xF0, 0xFF)),
        RepeatMode.RepeatOne => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xE6, 0x00)),
        _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2A, 0x3A, 0x4E))
    };

    public System.Windows.Media.Brush ShuffleButtonForeground => IsShuffle
        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xF0, 0xFF))
        : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6E, 0x80, 0x96));

    public System.Windows.Media.Brush ShuffleButtonBorder => IsShuffle
        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xF0, 0xFF))
        : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2A, 0x3A, 0x4E));

    public bool IsRepeatOne => RepeatMode == RepeatMode.RepeatOne;

    // 설정 화면 서브 탭 & 색상 카테고리
    [ObservableProperty]
    private string _settingsSubTab = "Cassette"; // "Cassette" | "Download"

    [ObservableProperty]
    private string _colorCategory = "Neon"; // "Normal" | "Neon" | "Gradient"

    public bool IsCassetteSettingsTab => SettingsSubTab == "Cassette";
    public bool IsDownloadSettingsTab => SettingsSubTab == "Download";

    public bool IsNormalColorCategory => ColorCategory == "Normal";
    public bool IsNeonColorCategory => ColorCategory == "Neon";
    public bool IsGradientColorCategory => ColorCategory == "Gradient";

    // 동적 테마 브러시 및 커스텀 휠
    [ObservableProperty]
    private System.Windows.Media.Brush _themeAccentBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xF0, 0xFF));

    [ObservableProperty]
    private System.Windows.Media.Color _themeAccentColor = System.Windows.Media.Color.FromRgb(0x00, 0xF0, 0xFF);

    [ObservableProperty]
    private bool _isNeonGlow;

    [ObservableProperty]
    private string? _customReelImagePath;

    public object ReelImageSource => !string.IsNullOrEmpty(CustomReelImagePath) && File.Exists(CustomReelImagePath)
        ? CustomReelImagePath
        : "/Assets/reel.png";

    public string CurrentDownloadPath => _settings.DownloadPath;
    public string CurrentTempPath => _settings.TempPath;
    public string CurrentThemeColor1 => _settings.ThemeColor1;
    public string CurrentThemeColor2 => _settings.ThemeColor2 ?? "#FFE600";

    partial void OnRepeatModeChanged(RepeatMode value)
    {
        OnPropertyChanged(nameof(RepeatButtonText));
        OnPropertyChanged(nameof(RepeatToolTip));
        OnPropertyChanged(nameof(RepeatButtonForeground));
        OnPropertyChanged(nameof(RepeatButtonBorder));
        OnPropertyChanged(nameof(IsRepeatOne));
        _settings.RepeatMode = value;
        SaveSettings();
    }

    partial void OnIsShuffleChanged(bool value)
    {
        OnPropertyChanged(nameof(ShuffleToolTip));
        OnPropertyChanged(nameof(ShuffleButtonForeground));
        OnPropertyChanged(nameof(ShuffleButtonBorder));
        _settings.IsShuffle = value;
        SaveSettings();
    }

    partial void OnSettingsSubTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsCassetteSettingsTab));
        OnPropertyChanged(nameof(IsDownloadSettingsTab));
    }

    partial void OnColorCategoryChanged(string value)
    {
        OnPropertyChanged(nameof(IsNormalColorCategory));
        OnPropertyChanged(nameof(IsNeonColorCategory));
        OnPropertyChanged(nameof(IsGradientColorCategory));
    }

    partial void OnIsNeonGlowChanged(bool value)
    {
        _settings.IsNeonGlow = value;
        SaveSettings();
        StatusMessage = value ? "네온 글로우 발광 효과: ON" : "네온 글로우 발광 효과: OFF";
    }

    #endregion

    public MainViewModel()
    {
        _player = new AudioPlayer();

        _settingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AuraLe",
            "settings.json");

        _settings = SettingsManager.Load(_settingsFilePath, new AppSettings());
        _volume = _settings.Volume;
        _player.Volume = _volume;
        _windowOpacity = _settings.WindowOpacity;
        _backgroundImagePath = _settings.BackgroundImagePath;
        _backgroundImageOffsetX = _settings.BackgroundImageOffsetX;
        _isShuffle = _settings.IsShuffle;
        _repeatMode = _settings.RepeatMode;
        _customReelImagePath = _settings.CustomReelImagePath;
        _colorCategory = _settings.ThemeCategory;
        _isNeonGlow = _settings.IsNeonGlow;
        UpdateThemeBrush();

        // 플레이어 이벤트 구독
        _player.StateChanged += OnPlayerStateChanged;
        _player.TrackEnded += OnTrackEnded;

        // 재생 시간 타이머 설정 (200ms 간격으로 시간 업데이트)
        _progressTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _progressTimer.Tick += OnProgressTimerTick;

        // C 드라이브 기본 음원 보관 폴더 및 임시 폴더 자동 생성 (폴더 선택은 나중에 필요 시)
        try
        {
            if (!Directory.Exists(_settings.DownloadPath))
            {
                Directory.CreateDirectory(_settings.DownloadPath);
            }
            if (!Directory.Exists(_settings.TempPath))
            {
                Directory.CreateDirectory(_settings.TempPath);
            }
        }
        catch
        {
            // 권한 오류 시 예외 방어
        }

        // 시작 시 저장 폴더의 MP3 목록 불러오기
        RefreshPlaylist();
    }

    #region Player Commands

    [RelayCommand]
    public void Play()
    {
        if (CurrentTrack == null)
        {
            if (Playlist.Count > 0)
            {
                SelectAndPlay(Playlist[0]);
            }
            return;
        }

        // 플레이어에 아직 파일이 열리지 않았거나, 현재 선택된 트랙과 다른 파일인 경우 새로 열고 재생
        if (_player.CurrentFilePath == null || !_player.CurrentFilePath.Equals(CurrentTrack.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            SelectAndPlay(CurrentTrack);
            return;
        }

        if (!_player.IsPlaying)
        {
            _player.Play();
        }
    }

    [RelayCommand]
    public void Pause()
    {
        if (_player.IsPlaying)
        {
            _player.Pause();
        }
    }

    [RelayCommand]
    public void PlayPause()
    {
        if (CurrentTrack == null)
        {
            if (Playlist.Count > 0)
            {
                SelectAndPlay(Playlist[0]);
            }
            return;
        }

        // 플레이어에 아직 파일이 열리지 않았거나, 현재 선택된 트랙과 다른 파일인 경우 새로 열고 재생
        if (_player.CurrentFilePath == null || !_player.CurrentFilePath.Equals(CurrentTrack.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            SelectAndPlay(CurrentTrack);
            return;
        }

        if (_player.IsPlaying)
        {
            _player.Pause();
        }
        else
        {
            _player.Play();
        }
    }

    [RelayCommand]
    public void Stop()
    {
        _player.Stop();
        CurrentPositionSeconds = 0;
        CurrentTimeText = "00:00";
    }

    [RelayCommand]
    public void ToggleShuffle()
    {
        IsShuffle = !IsShuffle;
        if (IsShuffle)
        {
            // 셔플이 켜지면 반복 모드는 완전 해제 (상호 배타적)
            RepeatMode = RepeatMode.Off;
            StatusMessage = "모드: 셔플(랜덤) 재생 ON";
        }
        else
        {
            StatusMessage = "모드: 1곡 재생 후 정지 (기본)";
        }
    }

    [RelayCommand]
    public void ToggleRepeat()
    {
        RepeatMode = RepeatMode switch
        {
            RepeatMode.Off => RepeatMode.RepeatAll,
            RepeatMode.RepeatAll => RepeatMode.RepeatOne,
            _ => RepeatMode.Off
        };

        if (RepeatMode != RepeatMode.Off)
        {
            // 반복 모드가 켜지면 셔플 모드는 완전 해제 (상호 배타적)
            IsShuffle = false;
        }

        StatusMessage = RepeatMode switch
        {
            RepeatMode.RepeatAll => "모드: 전체 연속 재생 (ALL)",
            RepeatMode.RepeatOne => "모드: 한 곡 반복 재생 (ONE)",
            _ => "모드: 1곡 재생 후 정지 (반복 끔)"
        };
    }

    [RelayCommand]
    public void NextTrack()
    {
        if (Playlist.Count == 0) return;
        var cur = CurrentTrack != null ? Playlist.IndexOf(CurrentTrack) : -1;
        var target = (IsShuffle && Playlist.Count > 1)
            ? GetRandomTrack(cur)
            : Playlist[(cur + 1) % Playlist.Count];
        SelectAndPlay(target);
    }

    [RelayCommand]
    public void PrevTrack()
    {
        if (Playlist.Count == 0) return;
        var currentIndex = CurrentTrack != null ? Playlist.IndexOf(CurrentTrack) : 0;
        var prevIndex = (currentIndex - 1 + Playlist.Count) % Playlist.Count;
        SelectAndPlay(Playlist[prevIndex]);
    }

    [RelayCommand]
    public void SelectAndPlay(TrackItem? track)
    {
        if (track == null || !File.Exists(track.FilePath)) return;

        try
        {
            CurrentTrack = track;
            _settings.LastPlayedFilePath = track.FilePath;
            SaveSettings();

            CurrentPositionSeconds = 0;
            CurrentTimeText = "00:00";

            _player.Open(track.FilePath);
            _player.Play();
            TotalDurationSeconds = _player.TotalTime.TotalSeconds;
            TotalTimeText = TimeFormatter.Format(_player.TotalTime);
            StatusMessage = $"PLAYING: {track.Title}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"오류: {ex.Message}";
        }
    }

    private bool _wasPlayingBeforeSeek = false;

    /// <summary>
    /// 마우스 클릭/드래그 시작 시 호출: 재생 중이면 일시 정지하고 타이머를 중단하여 충돌 원천 차단
    /// </summary>
    public void BeginSeek(double targetSeconds)
    {
        if (CurrentTrack == null || _player.TotalTime <= TimeSpan.Zero) return;

        IsUserSeeking = true;
        _progressTimer.Stop();

        _wasPlayingBeforeSeek = _player.IsPlaying;
        if (_wasPlayingBeforeSeek)
        {
            _player.Pause();
        }

        CurrentPositionSeconds = targetSeconds;
        CurrentTimeText = TimeFormatter.FormatSeconds(targetSeconds);
    }

    /// <summary>
    /// 드래그 중 실시간 슬라이더 및 시간 텍스트 미리보기
    /// </summary>
    public void UpdateSeekPreview(double targetSeconds)
    {
        if (!IsUserSeeking) return;

        CurrentPositionSeconds = targetSeconds;
        CurrentTimeText = TimeFormatter.FormatSeconds(targetSeconds);
    }

    /// <summary>
    /// 마우스를 놓는 순간: 지정된 위치로 오디오를 이동하고 원래 재생 중이었다면 재생 재개
    /// </summary>
    [RelayCommand]
    public void EndSeek(double targetSeconds)
    {
        if (CurrentTrack == null || _player.TotalTime <= TimeSpan.Zero)
        {
            IsUserSeeking = false;
            return;
        }

        _player.Seek(TimeSpan.FromSeconds(targetSeconds));
        CurrentPositionSeconds = targetSeconds;
        CurrentTimeText = TimeFormatter.FormatSeconds(targetSeconds);

        if (_wasPlayingBeforeSeek)
        {
            _player.Play();
            _progressTimer.Start();
        }

        IsUserSeeking = false;
        _wasPlayingBeforeSeek = false;
    }

    [RelayCommand]
    public void Seek(double targetSeconds)
    {
        EndSeek(targetSeconds);
    }

    partial void OnCurrentViewChanged(ActiveView value)
    {
        IsRecMode = value == ActiveView.Download;
        OnPropertyChanged(nameof(HeaderTitle));
        OnPropertyChanged(nameof(IsHomeView));
        OnPropertyChanged(nameof(IsPlayerView));
        OnPropertyChanged(nameof(IsDownloadView));
        OnPropertyChanged(nameof(IsSettingsView));
        OnPropertyChanged(nameof(IsSynthView));

        if (value == ActiveView.Synth)
        {
            // 규칙 준수: 연주 진입 시 재생 중인 음원 무조건 강제 정지
            if (IsPlaying)
            {
                Stop();
            }
            Synth.SetTargetTrack(CurrentTrack);
            StatusMessage = "AuraLe CASSETTE SYNTH // QWERTY INSTRUMENT";
        }
        else if (value == ActiveView.Download)
        {
            StatusMessage = "REC MODE: YOUTUBE DOWNLOAD";
        }
        else if (value == ActiveView.Home)
        {
            StatusMessage = "AuraLe SYSTEM HOME";
        }
        else if (value == ActiveView.Settings)
        {
            StatusMessage = "AuraLe SYSTEM CONFIGURATION";
        }
        else
        {
            StatusMessage = CurrentTrack != null ? $"READY: {CurrentTrack.Title}" : "CASSETTE DECK MODE";
        }
    }

    partial void OnCurrentTrackChanged(TrackItem? value)
    {
        Synth.SetTargetTrack(value);
    }

    partial void OnBackgroundImagePathChanged(string? value)
    {
        OnPropertyChanged(nameof(HasBackgroundImage));
    }

    partial void OnVolumeChanged(float value)
    {
        _player.Volume = value;
        _settings.Volume = value;
        SaveSettings();
    }

    partial void OnWindowOpacityChanged(double value)
    {
        _settings.WindowOpacity = value;
        SaveSettings();
    }

    partial void OnBackgroundImageOffsetXChanged(double value)
    {
        _settings.BackgroundImageOffsetX = value;
        SaveSettings();
    }

    #endregion

    #region Drawer & REC Mode Commands

    [RelayCommand]
    public void SwitchView(string viewName)
    {
        CurrentView = viewName?.ToLowerInvariant() switch
        {
            "home" => ActiveView.Home,
            "settings" or "config" => ActiveView.Settings,
            "synth" or "instrument" or "keyboard" => ActiveView.Synth,
            _ => ActiveView.Player
        };
    }

    [RelayCommand]
    public void SwitchSettingsTab(string tab)
    {
        SettingsSubTab = tab;
        OnPropertyChanged(nameof(IsCassetteSettingsTab));
        OnPropertyChanged(nameof(IsDownloadSettingsTab));
    }

    [RelayCommand]
    public void SwitchColorCategory(string category)
    {
        ColorCategory = category;
        OnPropertyChanged(nameof(IsNormalColorCategory));
        OnPropertyChanged(nameof(IsNeonColorCategory));
        OnPropertyChanged(nameof(IsGradientColorCategory));
    }

    [RelayCommand]
    public void ApplySolidColor(string param)
    {
        var parts = param.Split('|');
        var name = parts[0];
        var hex = parts.Length > 1 ? parts[1] : parts[0];

        _settings.ThemeColor1 = hex;
        _settings.IsGradientTheme = false;
        _settings.ThemeCategory = ColorCategory;
        UpdateThemeBrush();
        SaveSettings();
        StatusMessage = $"테마 색상 적용: {name}";
    }

    [RelayCommand]
    public void ApplyGradientColor(string param)
    {
        var parts = param.Split('|');
        var name = parts[0];
        var hex1 = parts.Length > 1 ? parts[1] : "#00F0FF";
        var hex2 = parts.Length > 2 ? parts[2] : "#FFE600";

        _settings.ThemeColor1 = hex1;
        _settings.ThemeColor2 = hex2;
        _settings.IsGradientTheme = true;
        _settings.ThemeCategory = "Gradient";
        UpdateThemeBrush();
        SaveSettings();
        StatusMessage = $"그라데이션 테마 적용: {name}";
    }

    private void UpdateThemeBrush()
    {
        try
        {
            if (_settings.IsGradientTheme && !string.IsNullOrEmpty(_settings.ThemeColor2))
            {
                var c1 = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_settings.ThemeColor1);
                var c2 = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_settings.ThemeColor2);
                ThemeAccentColor = c1;
                ThemeAccentBrush = new System.Windows.Media.LinearGradientBrush(
                    c1, c2,
                    new System.Windows.Point(0, 0),
                    new System.Windows.Point(1, 1));
            }
            else
            {
                var c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_settings.ThemeColor1);
                ThemeAccentColor = c;
                ThemeAccentBrush = new System.Windows.Media.SolidColorBrush(c);
            }
        }
        catch
        {
            ThemeAccentColor = System.Windows.Media.Color.FromRgb(0x00, 0xF0, 0xFF);
            ThemeAccentBrush = new System.Windows.Media.SolidColorBrush(ThemeAccentColor);
        }

        OnPropertyChanged(nameof(CurrentThemeColor1));
        OnPropertyChanged(nameof(CurrentThemeColor2));
    }

    [RelayCommand]
    public void PickCustomSolidColor()
    {
        var owner = GetWindowHandle();
        var initial = ThemeAccentColor;
        var picked = WindowsColorPicker.ShowColorDialog(owner, initial);
        if (picked.HasValue)
        {
            var hex = $"#{picked.Value.R:X2}{picked.Value.G:X2}{picked.Value.B:X2}";
            _settings.ThemeColor1 = hex;
            _settings.IsGradientTheme = false;
            _settings.ThemeCategory = ColorCategory;
            UpdateThemeBrush();
            SaveSettings();
            StatusMessage = $"Windows 색상표 직접 선택 적용: {hex}";
        }
    }

    [RelayCommand]
    public void PickGradientStartColor()
    {
        var owner = GetWindowHandle();
        var initial = ThemeAccentColor;
        var picked = WindowsColorPicker.ShowColorDialog(owner, initial);
        if (picked.HasValue)
        {
            var hex = $"#{picked.Value.R:X2}{picked.Value.G:X2}{picked.Value.B:X2}";
            _settings.ThemeColor1 = hex;
            _settings.IsGradientTheme = true;
            _settings.ThemeCategory = "Gradient";
            ColorCategory = "Gradient";
            UpdateThemeBrush();
            SaveSettings();
            StatusMessage = $"그라데이션 시작 색상 설정: {hex}";
        }
    }

    [RelayCommand]
    public void PickGradientEndColor()
    {
        var owner = GetWindowHandle();
        System.Windows.Media.Color? initial = null;
        try
        {
            if (!string.IsNullOrEmpty(_settings.ThemeColor2))
            {
                initial = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_settings.ThemeColor2);
            }
        }
        catch {}

        var picked = WindowsColorPicker.ShowColorDialog(owner, initial);
        if (picked.HasValue)
        {
            var hex = $"#{picked.Value.R:X2}{picked.Value.G:X2}{picked.Value.B:X2}";
            _settings.ThemeColor2 = hex;
            _settings.IsGradientTheme = true;
            _settings.ThemeCategory = "Gradient";
            ColorCategory = "Gradient";
            UpdateThemeBrush();
            SaveSettings();
            StatusMessage = $"그라데이션 끝 색상 설정: {hex}";
        }
    }

    [RelayCommand]
    public void ConvertCurrentColorToNeon()
    {
        var baseColor = ThemeAccentColor;
        var neonColor = WindowsColorPicker.ConvertToNeon(baseColor);
        var hex = $"#{neonColor.R:X2}{neonColor.G:X2}{neonColor.B:X2}";

        _settings.ThemeColor1 = hex;
        _settings.IsGradientTheme = false;
        _settings.ThemeCategory = "Neon";
        ColorCategory = "Neon";
        UpdateThemeBrush();
        SaveSettings();
        StatusMessage = $"채도 100% 네온 톤으로 변환 완료: {hex}";
    }

    private static IntPtr GetWindowHandle()
    {
        if (Application.Current?.MainWindow != null)
        {
            return new System.Windows.Interop.WindowInteropHelper(Application.Current.MainWindow).Handle;
        }
        return IntPtr.Zero;
    }

    [RelayCommand]
    public void ChangeReelImage()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "카세트 릴(회전 휠/기어) 이미지 선택",
            Filter = "PNG 이미지 (*.png)|*.png|모든 이미지 (*.png;*.jpg;*.webp)|*.png;*.jpg;*.webp"
        };
        if (dialog.ShowDialog() == true)
        {
            CustomReelImagePath = dialog.FileName;
            _settings.CustomReelImagePath = dialog.FileName;
            OnPropertyChanged(nameof(ReelImageSource));
            SaveSettings();
            StatusMessage = "릴 휠 에셋 변경 완료!";
        }
    }

    [RelayCommand]
    public void ResetReelImage()
    {
        CustomReelImagePath = null;
        _settings.CustomReelImagePath = null;
        OnPropertyChanged(nameof(ReelImageSource));
        SaveSettings();
        StatusMessage = "기본 릴 휠로 리셋되었습니다.";
    }

    [RelayCommand]
    public void ChangeDownloadFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "최종 MP3 보관 폴더 선택",
            InitialDirectory = _settings.DownloadPath
        };
        if (dialog.ShowDialog() == true)
        {
            _settings.DownloadPath = dialog.FolderName;
            SaveSettings();
            RefreshPlaylist();
            OnPropertyChanged(nameof(CurrentDownloadPath));
            StatusMessage = $"저장 폴더 변경: {dialog.FolderName}";
        }
    }

    [RelayCommand]
    public void ClearTempCache()
    {
        try
        {
            if (Directory.Exists(_settings.TempPath))
            {
                var files = Directory.GetFiles(_settings.TempPath);
                foreach (var f in files)
                {
                    try { File.Delete(f); } catch {}
                }
                StatusMessage = $"임시 캐시 파일 {files.Length}개 삭제 완료";
            }
            else
            {
                StatusMessage = "삭제할 임시 캐시가 없습니다.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"캐시 삭제 오류: {ex.Message}";
        }
    }

    [RelayCommand]
    public void OpenTempFolder()
    {
        if (!Directory.Exists(_settings.TempPath))
        {
            Directory.CreateDirectory(_settings.TempPath);
        }
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = _settings.TempPath,
            UseShellExecute = true
        });
    }

    [RelayCommand]
    public void ToggleDrawer()
    {
        IsDrawerOpen = !IsDrawerOpen;
    }

    #endregion

    #region Playlist & Management Commands

    [RelayCommand]
    public void RefreshPlaylist()
    {
        var files = PlaylistScanner.ScanMp3Files(_settings.DownloadPath);
        Playlist.Clear();
        foreach (var file in files)
        {
            Playlist.Add(new TrackItem(file.FullName));
        }

        if (Playlist.Count > 0)
        {
            // 마지막으로 재생했던 곡이 저장되어 있으면 복원, 없으면 첫 곡 선택
            TrackItem? targetTrack = null;
            if (!string.IsNullOrEmpty(_settings.LastPlayedFilePath))
            {
                targetTrack = Playlist.FirstOrDefault(t => t.FilePath.Equals(_settings.LastPlayedFilePath, StringComparison.OrdinalIgnoreCase));
            }

            CurrentTrack = targetTrack ?? Playlist[0];
            LoadTrackMetadata(CurrentTrack);
        }
        else
        {
            CurrentTrack = null;
            CurrentPositionSeconds = 0;
            TotalDurationSeconds = 0;
            CurrentTimeText = "00:00";
            TotalTimeText = "00:00";
        }
    }

    private void LoadTrackMetadata(TrackItem track)
    {
        try
        {
            _player.Open(track.FilePath);
            TotalDurationSeconds = _player.TotalTime.TotalSeconds;
            TotalTimeText = TimeFormatter.Format(_player.TotalTime);
            CurrentPositionSeconds = 0;
            CurrentTimeText = "00:00";
            StatusMessage = $"READY: {track.Title}";
        }
        catch
        {
            // 메타데이터 로드 실패 시 무시
        }
    }

    [RelayCommand]
    public void DeleteTrack(TrackItem? track)
    {
        if (track == null) return;

        var result = MessageBox.Show(
            $"'{track.Title}' 곡을 삭제하시겠습니까?",
            "곡 삭제 확인",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            if (CurrentTrack == track)
            {
                _player.Stop();
                CurrentTrack = null;
            }

            try
            {
                if (File.Exists(track.FilePath))
                {
                    File.Delete(track.FilePath);
                }
                Playlist.Remove(track);
                StatusMessage = $"삭제됨: {track.Title}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"파일 삭제 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    public void OpenStorageFolder()
    {
        if (!Directory.Exists(_settings.DownloadPath))
        {
            Directory.CreateDirectory(_settings.DownloadPath);
        }
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = _settings.DownloadPath,
            UseShellExecute = true
        });
    }

    [RelayCommand]
    public void ChangeBackground()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "카세트 배경 이미지 선택",
            Filter = "이미지 파일 (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|모든 파일 (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            BackgroundImagePath = dialog.FileName;
            _settings.BackgroundImagePath = dialog.FileName;
            SaveSettings();
        }
    }

    [RelayCommand]
    public void ResetBackground()
    {
        BackgroundImagePath = null;
        _settings.BackgroundImagePath = null;
        BackgroundImageOffsetX = 0;
        _settings.BackgroundImageOffsetX = 0;
        SaveSettings();
    }

    #endregion

    #region Private Helpers

    private void OnPlayerStateChanged(object? sender, PlaybackState state)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            IsPlaying = state == PlaybackState.Playing;
            if (IsPlaying)
            {
                _progressTimer.Start();
            }
            else
            {
                _progressTimer.Stop();
            }
        });
    }

    private void OnTrackEnded(object? sender, EventArgs e)
    {
        // WASAPI 백그라운드 스레드에서 UI 스레드로 안전하게 마샬링
        Application.Current?.Dispatcher.InvokeAsync(async () =>
        {
            // 1. 재생바 및 텍스트를 끝(100%) 완료 상태로 강제 도달 표시
            CurrentPositionSeconds = TotalDurationSeconds;
            CurrentTimeText = TotalTimeText;

            // 2. 사용자가 곡이 끝까지 다 재생되었음을 인지할 수 있는 미세 대기 (200ms)
            await Task.Delay(200);

            // 3. 0으로 초기화
            CurrentPositionSeconds = 0;
            CurrentTimeText = "00:00";

            // 4. 다음 곡 처리 (순차 재생 및 추후 반복/랜덤 재생 지원)
            HandleTrackEnded();
        });
    }

    private void HandleTrackEnded()
    {
        if (Playlist.Count == 0)
        {
            Stop();
            return;
        }

        var cur = CurrentTrack != null ? Playlist.IndexOf(CurrentTrack) : -1;

        // 패턴 매칭으로 다음 재생 트랙 결정
        TrackItem? target = (RepeatMode, IsShuffle) switch
        {
            (RepeatMode.RepeatOne, _) => CurrentTrack,                                      // 1. 한 곡 반복 (현재 곡 재시작)
            (RepeatMode.RepeatAll, _) => Playlist[(cur + 1) % Playlist.Count],              // 2. 전체 반복 (다음 곡 루프)
            (_, true)                 => Playlist.Count > 1 ? GetRandomTrack(cur) : Playlist[0], // 3. 셔플 (랜덤 재생)
            _                         => null                                               // 4. 반복 끔: 1곡 재생 완료 후 00:00 정지!
        };

        if (target != null)
        {
            SelectAndPlay(target);
        }
        else
        {
            _player.Stop();
            CurrentPositionSeconds = 0;
            CurrentTimeText = "00:00";
            if (CurrentTrack != null)
            {
                StatusMessage = $"READY: {CurrentTrack.Title}";
            }
        }
    }

    private TrackItem GetRandomTrack(int currentIndex)
    {
        if (Playlist.Count <= 1) return Playlist[0];
        int nextIndex;
        do
        {
            nextIndex = Random.Shared.Next(Playlist.Count);
        } while (nextIndex == currentIndex);
        return Playlist[nextIndex];
    }

    private void OnProgressTimerTick(object? sender, EventArgs e)
    {
        // 사용자가 슬라이더를 드래그 중일 때는 타이머가 값을 덮어쓰지 않음
        if (_player.IsPlaying && !IsUserSeeking)
        {
            var sec = _player.CurrentTime.TotalSeconds;
            CurrentPositionSeconds = Math.Min(sec, TotalDurationSeconds);
            CurrentTimeText = TimeFormatter.Format(_player.CurrentTime);
        }
    }

    private void SaveSettings()
    {
        SettingsManager.Save(_settingsFilePath, _settings);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _progressTimer.Stop();
        Synth.Dispose();
        _player.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion
}
