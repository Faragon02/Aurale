using System.Diagnostics;
using System.IO;
using Common.Audio.Synth;
using NAudio.Wave;

namespace Common.Audio.Analysis;

/// <summary>
/// 오디오 분석 모드 프리셋 (Basic Pitch AI 신경망 vs 자체 DSP)
/// </summary>
public enum TranscriptionMode
{
    /// <summary>
    /// Spotify Basic Pitch AI 딥러닝 고품질 변환 (다성음/복잡한 곡도 고정밀 추출)
    /// </summary>
    BasicPitchAI,

    /// <summary>
    /// 피아노/독주 악기 모드 (130~1,500Hz BPF, 해머 온셋 특화, 빠른 아르페지오/16분음표 대응)
    /// </summary>
    PianoSolo,

    /// <summary>
    /// 보컬/일반 모드 (스테레오 센터 채널 추출, 150~1,200Hz BPF, 보컬 레가토/비브라토 스무딩)
    /// </summary>
    VocalCenter
}

/// <summary>
/// 오디오 분석으로 추출된 단일 음표 정보
/// </summary>
public record TranscribedNote(
    TimeSpan StartTime,
    TimeSpan Duration,
    int MidiNote,
    double Frequency,
    char QwertyKey,
    string NoteName,
    bool IsSharp = false
);

/// <summary>
/// 오디오 ➔ 멜로디 분석 및 쿼티 악보 변환기 (Spotify Basic Pitch AI 파이프라인 및 자체 BPF DSP 엔진 탑재)
/// </summary>
public class AudioToNoteTranscriber
{
    private const int SampleRate = 44100;
    private const int FrameSize = 2048; // 약 46ms
    private const int HopSize = 1024;   // 약 23ms

    // 피치 탐색 주파수 범위 (130Hz ~ 1000Hz: 약 C3 ~ C6)
    private const int MinLag = 44;  // 44100 / 1000Hz
    private const int MaxLag = 340; // 44100 / 130Hz
    private const float SilenceRmsThreshold = 0.020f;

    /// <summary>
    /// 음원 파일(MP3/WAV)을 분석하여 안정된 멜로디 노트 목록 및 QWERTY 악보 텍스트 생성
    /// </summary>
    public async Task<(List<TranscribedNote> Notes, string QwertySheet)> TranscribeAudioAsync(
        string audioFilePath,
        TranscriptionMode mode = TranscriptionMode.BasicPitchAI,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(audioFilePath))
        {
            throw new FileNotFoundException("분석할 오디오 파일을 찾을 수 없습니다.", audioFilePath);
        }

        // [1] Basic Pitch AI 모드
        if (mode == TranscriptionMode.BasicPitchAI)
        {
            try
            {
                var aiResult = await TranscribeWithBasicPitchAsync(audioFilePath, progress, cancellationToken);
                if (aiResult.Notes.Count > 0)
                {
                    return aiResult;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioToNoteTranscriber] Basic Pitch AI 실행 실패, DSP 모드로 대체합니다: {ex.Message}");
            }
        }

        // [2] 자체 DSP 엔진 모드 (PianoSolo or VocalCenter)
        var dspMode = (mode == TranscriptionMode.BasicPitchAI) ? TranscriptionMode.PianoSolo : mode;
        return await TranscribeDspAsync(audioFilePath, dspMode, progress, cancellationToken);
    }

    #region Basic Pitch AI Pipeline

    private async Task<(List<TranscribedNote> Notes, string QwertySheet)> TranscribeWithBasicPitchAsync(
        string audioFilePath,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        var transcriber = new BasicPitchTranscriber();
        return await transcriber.TranscribeAsync(audioFilePath, progress, cancellationToken);
    }

    #endregion

    #region Internal DSP Engine

    private async Task<(List<TranscribedNote> Notes, string QwertySheet)> TranscribeDspAsync(
        string audioFilePath,
        TranscriptionMode mode,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            using var reader = new AudioFileReader(audioFilePath);
            var samples = ReadAndPreprocessSamples(reader, mode);
            int totalSamples = samples.Length;

            if (totalSamples < FrameSize)
            {
                return (new List<TranscribedNote>(), string.Empty);
            }

            int totalFrames = (totalSamples - FrameSize) / HopSize;
            var rawMidiFrames = new int[totalFrames];
            var rmsFrames = new float[totalFrames];

            int lastProgressPercent = 0;

            // [1단계: 프레임별 피치 및 음량 감지 (YIN 피치 트래킹)]
            for (int i = 0; i < totalFrames; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int offset = i * HopSize;
                float rms = CalculateRms(samples, offset, FrameSize);
                rmsFrames[i] = rms;

                if (rms >= SilenceRmsThreshold)
                {
                    double freq = DetectPitchYin(samples, offset, FrameSize, mode);
                    if (freq > 0)
                    {
                        rawMidiFrames[i] = NoteFrequencyTable.FrequencyToMidi(freq);
                    }
                    else
                    {
                        rawMidiFrames[i] = -1;
                    }
                }
                else
                {
                    rawMidiFrames[i] = -1;
                }

                if (progress != null && totalFrames > 0)
                {
                    int percent = (i * 60) / totalFrames; // 0% ~ 60%
                    if (percent != lastProgressPercent)
                    {
                        lastProgressPercent = percent;
                        progress.Report(percent);
                    }
                }
            }

            // [2단계: 모드별 메디안 필터 스무딩 (치찰음/떨림/옥타브 튐 노이즈 제거)]
            int medianRadius = (mode == TranscriptionMode.PianoSolo) ? 1 : 2; // 피아노: 3프레임, 보컬: 5프레임
            int minWindowCount = (medianRadius == 1) ? 2 : 3;

            var smoothedMidi = new int[totalFrames];
            for (int i = 0; i < totalFrames; i++)
            {
                int windowStart = Math.Max(0, i - medianRadius);
                int windowEnd = Math.Min(totalFrames - 1, i + medianRadius);
                var window = new List<int>();

                for (int w = windowStart; w <= windowEnd; w++)
                {
                    if (rawMidiFrames[w] > 0) window.Add(rawMidiFrames[w]);
                }

                if (window.Count >= minWindowCount)
                {
                    window.Sort();
                    smoothedMidi[i] = window[window.Count / 2]; // 중간값
                }
                else
                {
                    smoothedMidi[i] = -1;
                }
            }

            // [3단계: 최적의 다장조(C Major) 조옮김(Transpose) 오프셋 계산]
            var validNotesList = smoothedMidi.Where(n => n > 0).ToList();
            int bestTranspose = FindBestDiatonicTranspose(validNotesList);

            // [4단계: 모드별 온셋 감지 및 연속된 노트 그룹화]
            int minNoteFrames = (mode == TranscriptionMode.PianoSolo) ? 5 : 7;
            float onsetRmsRatio = (mode == TranscriptionMode.PianoSolo) ? 1.75f : 2.3f;
            float minOnsetRms = (mode == TranscriptionMode.PianoSolo) ? 0.025f : 0.04f;

            var notes = new List<TranscribedNote>();
            int currentNote = -1;
            int noteStartFrame = 0;
            int noteLengthFrames = 0;

            for (int i = 0; i < totalFrames; i++)
            {
                int note = smoothedMidi[i];
                if (note > 0)
                {
                    note = NoteFrequencyTable.NormalizeToThreeOctaves(note + bestTranspose);
                }

                // 온셋 감지 (RMS 급증)
                bool isOnset = (i > 0 && rmsFrames[i] > rmsFrames[i - 1] * onsetRmsRatio && rmsFrames[i] > minOnsetRms);
                bool isSame = (note == currentNote && !isOnset && note != -1);

                if (isSame)
                {
                    noteLengthFrames++;
                }
                else
                {
                    if (currentNote != -1 && noteLengthFrames >= minNoteFrames)
                    {
                        double startSec = (double)(noteStartFrame * HopSize) / SampleRate;
                        double durSec = (double)(noteLengthFrames * HopSize) / SampleRate;
                        char key = MidiToQwertyKey(currentNote);
                        string name = NoteFrequencyTable.GetSolfege(currentNote);

                        notes.Add(new TranscribedNote(
                            TimeSpan.FromSeconds(startSec),
                            TimeSpan.FromSeconds(durSec),
                            currentNote,
                            NoteFrequencyTable.MidiToFrequency(currentNote),
                            key,
                            name
                        ));
                    }

                    currentNote = note;
                    noteStartFrame = i;
                    noteLengthFrames = 1;
                }

                if (progress != null && totalFrames > 0)
                {
                    int percent = 60 + (i * 40) / totalFrames; // 60% ~ 100%
                    if (percent != lastProgressPercent)
                    {
                        lastProgressPercent = percent;
                        progress.Report(percent);
                    }
                }
            }

            // 마지막 노트 등록
            if (currentNote != -1 && noteLengthFrames >= minNoteFrames)
            {
                double startSec = (double)(noteStartFrame * HopSize) / SampleRate;
                double durSec = (double)(noteLengthFrames * HopSize) / SampleRate;
                char key = MidiToQwertyKey(currentNote);
                string name = NoteFrequencyTable.GetSolfege(currentNote);

                notes.Add(new TranscribedNote(
                    TimeSpan.FromSeconds(startSec),
                    TimeSpan.FromSeconds(durSec),
                    currentNote,
                    NoteFrequencyTable.MidiToFrequency(currentNote),
                    key,
                    name
                ));
            }

            progress?.Report(100);

            string sheetText = FormatToQwertySheetWithRhythm(notes);
            return (notes, sheetText);
        }, cancellationToken);
    }

    /// <summary>
    /// 오디오 샘플 디코딩 및 DSP 전처리 (채널 분리 & 모드별 BPF 대역 통과 필터)
    /// </summary>
    private static float[] ReadAndPreprocessSamples(AudioFileReader reader, TranscriptionMode mode)
    {
        int channels = reader.WaveFormat.Channels;
        var rawBuffer = new float[reader.Length / sizeof(float)];
        int readSamples = reader.Read(rawBuffer, 0, rawBuffer.Length);

        float[] mono;

        if (channels == 1)
        {
            mono = new float[readSamples];
            Array.Copy(rawBuffer, mono, readSamples);
        }
        else
        {
            int monoLength = readSamples / channels;
            mono = new float[monoLength];

            if (mode == TranscriptionMode.VocalCenter)
            {
                // [보컬 모드]: 센터 채널 멜로디 추출 ((L+R)/2 에서 사이드 성분 감쇠)
                for (int i = 0; i < monoLength; i++)
                {
                    float l = rawBuffer[(i * channels)];
                    float r = rawBuffer[(i * channels) + 1];
                    float mid = 0.5f * (l + r);
                    float side = 0.5f * (l - r);

                    float sideEnergy = MathF.Abs(side);
                    float midEnergy = MathF.Abs(mid) + 1e-4f;
                    float factor = MathF.Max(0.2f, 1.0f - (sideEnergy / midEnergy));
                    mono[i] = mid * factor;
                }
            }
            else
            {
                // [피아노 모드]: 좌(저음)/우(고음) 팬닝 손실 없는 1:1 밸런스 모노 다운믹스
                for (int i = 0; i < monoLength; i++)
                {
                    float l = rawBuffer[(i * channels)];
                    float r = rawBuffer[(i * channels) + 1];
                    mono[i] = 0.5f * (l + r);
                }
            }
        }

        // [대역 통과 필터 (Bandpass Filter) 적용]
        double hpCutoff = (mode == TranscriptionMode.PianoSolo) ? 130.0 : 150.0;
        double lpCutoff = (mode == TranscriptionMode.PianoSolo) ? 1500.0 : 1200.0;

        var hpFilter = BiquadFilter.CreateHighPass(hpCutoff, SampleRate);
        var lpFilter = BiquadFilter.CreateLowPass(lpCutoff, SampleRate);

        hpFilter.ProcessInPlace(mono);
        lpFilter.ProcessInPlace(mono);

        return mono;
    }

    private static int FindBestDiatonicTranspose(List<int> notes)
    {
        if (notes.Count == 0) return 0;

        var cMajorScale = new HashSet<int> { 0, 2, 4, 5, 7, 9, 11 };
        int bestShift = 0;
        int maxMatches = -1;

        for (int shift = 0; shift < 12; shift++)
        {
            int matches = 0;
            foreach (var note in notes)
            {
                int pc = (note + shift) % 12;
                if (cMajorScale.Contains(pc)) matches++;
            }

            if (matches > maxMatches)
            {
                maxMatches = matches;
                bestShift = shift;
            }
        }

        return bestShift;
    }

    private static float CalculateRms(float[] samples, int offset, int length)
    {
        double sum = 0;
        int end = Math.Min(samples.Length, offset + length);
        int count = end - offset;
        if (count <= 0) return 0f;

        for (int i = offset; i < end; i++)
        {
            sum += samples[i] * samples[i];
        }

        return (float)Math.Sqrt(sum / count);
    }

    private static double DetectPitchYin(float[] samples, int offset, int length, TranscriptionMode mode)
    {
        if (offset + length + MaxLag > samples.Length) return 0;

        var d = new double[MaxLag];

        for (int tau = MinLag; tau < MaxLag; tau++)
        {
            double diffSum = 0;
            for (int i = 0; i < length; i++)
            {
                double delta = samples[offset + i] - samples[offset + i + tau];
                diffSum += delta * delta;
            }
            d[tau] = diffSum;
        }

        double runningSum = 0;
        var dPrime = new double[MaxLag];
        dPrime[MinLag] = 1.0;

        for (int tau = MinLag; tau < MaxLag; tau++)
        {
            runningSum += d[tau];
            dPrime[tau] = runningSum > 0 ? (d[tau] * (tau - MinLag + 1)) / runningSum : 1.0;
        }

        int bestTau = -1;
        double threshold = (mode == TranscriptionMode.PianoSolo) ? 0.12 : 0.15;

        for (int tau = MinLag; tau < MaxLag - 1; tau++)
        {
            if (dPrime[tau] < threshold && dPrime[tau] <= dPrime[tau - 1] && dPrime[tau] <= dPrime[tau + 1])
            {
                bestTau = tau;
                break;
            }
        }

        if (bestTau == -1)
        {
            double minVal = double.MaxValue;
            for (int tau = MinLag; tau < MaxLag; tau++)
            {
                if (dPrime[tau] < minVal)
                {
                    minVal = dPrime[tau];
                    bestTau = tau;
                }
            }
            if (minVal > (mode == TranscriptionMode.PianoSolo ? 0.25 : 0.30)) return 0;
        }

        if (bestTau <= MinLag || bestTau >= MaxLag - 1) return 0;

        double s0 = dPrime[bestTau - 1];
        double s1 = dPrime[bestTau];
        double s2 = dPrime[bestTau + 1];
        double denom = 2.0 * (2.0 * s1 - s2 - s0);
        double deltaTau = Math.Abs(denom) > 1e-6 ? (s2 - s0) / denom : 0;
        double refinedTau = bestTau + deltaTau;

        if (refinedTau <= 0) return 0;
        return (double)SampleRate / refinedTau;
    }

    public static char MidiToQwertyKey(int midiNote)
    {
        int norm = NoteFrequencyTable.NormalizeToThreeOctaves(midiNote);

        return norm switch
        {
            // Low (C3 ~ B3)
            48 => 'Z', 49 => 'Z',
            50 => 'X', 51 => 'X',
            52 => 'C',
            53 => 'V', 54 => 'V',
            55 => 'B', 56 => 'B',
            57 => 'N', 58 => 'N',
            59 => 'M',

            // Mid (C4 ~ B4)
            60 => 'A', 61 => 'A',
            62 => 'S', 63 => 'S',
            64 => 'D',
            65 => 'F', 66 => 'F',
            67 => 'G', 68 => 'G',
            69 => 'H', 70 => 'H',
            71 => 'J',

            // High (C5 ~ B5)
            72 => 'Q', 73 => 'Q',
            74 => 'W', 75 => 'W',
            76 => 'E',
            77 => 'R', 78 => 'R',
            79 => 'T', 80 => 'T',
            81 => 'Y', 82 => 'Y',
            83 => 'U',

            _ => 'A'
        };
    }

    private static string FormatToQwertySheetWithRhythm(List<TranscribedNote> notes)
    {
        if (notes.Count == 0) return string.Empty;

        var sb = new System.Text.StringBuilder();
        int itemCounter = 0;

        for (int i = 0; i < notes.Count; i++)
        {
            if (i > 0)
            {
                var gap = notes[i].StartTime - (notes[i - 1].StartTime + notes[i - 1].Duration);
                if (gap.TotalSeconds > 0.35)
                {
                    sb.Append("- ");
                    itemCounter++;
                }
            }

            sb.Append(notes[i].QwertyKey);
            itemCounter++;

            if (itemCounter % 4 == 0)
            {
                sb.Append("  ");
            }
            else
            {
                sb.Append(' ');
            }

            if (itemCounter % 16 == 0)
            {
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    private class BiquadFilter
    {
        private double _b0, _b1, _b2, _a1, _a2;
        private double _x1, _x2, _y1, _y2;

        public static BiquadFilter CreateHighPass(double cutoffFreq, double sampleRate, double q = 0.7071)
        {
            var filter = new BiquadFilter();
            double omega = 2.0 * Math.PI * cutoffFreq / sampleRate;
            double cosOmega = Math.Cos(omega);
            double sinOmega = Math.Sin(omega);
            double alpha = sinOmega / (2.0 * q);

            double b0 = (1.0 + cosOmega) / 2.0;
            double b1 = -(1.0 + cosOmega);
            double b2 = (1.0 + cosOmega) / 2.0;
            double a0 = 1.0 + alpha;
            double a1 = -2.0 * cosOmega;
            double a2 = 1.0 - alpha;

            filter._b0 = b0 / a0;
            filter._b1 = b1 / a0;
            filter._b2 = b2 / a0;
            filter._a1 = a1 / a0;
            filter._a2 = a2 / a0;

            return filter;
        }

        public static BiquadFilter CreateLowPass(double cutoffFreq, double sampleRate, double q = 0.7071)
        {
            var filter = new BiquadFilter();
            double omega = 2.0 * Math.PI * cutoffFreq / sampleRate;
            double cosOmega = Math.Cos(omega);
            double sinOmega = Math.Sin(omega);
            double alpha = sinOmega / (2.0 * q);

            double b0 = (1.0 - cosOmega) / 2.0;
            double b1 = 1.0 - cosOmega;
            double b2 = (1.0 - cosOmega) / 2.0;
            double a0 = 1.0 + alpha;
            double a1 = -2.0 * cosOmega;
            double a2 = 1.0 - alpha;

            filter._b0 = b0 / a0;
            filter._b1 = b1 / a0;
            filter._b2 = b2 / a0;
            filter._a1 = a1 / a0;
            filter._a2 = a2 / a0;

            return filter;
        }

        public float Transform(float sample)
        {
            double x0 = sample;
            double y0 = _b0 * x0 + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;

            _x2 = _x1;
            _x1 = x0;
            _y2 = _y1;
            _y1 = y0;

            return (float)y0;
        }

        public void ProcessInPlace(float[] samples)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = Transform(samples[i]);
            }
        }
    }

    #endregion
}
