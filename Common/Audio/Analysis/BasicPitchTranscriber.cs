using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Audio.Analysis;

/// <summary>
/// 순수 C# (.NET) 기반 Spotify Basic Pitch AI 채보 엔진 (Python 불필요)
/// </summary>
public class BasicPitchTranscriber
{
    private static readonly string[] SolfegeTable =
    {
        "도", "도#", "레", "레#", "미", "파",
        "파#", "솔", "솔#", "라", "라#", "시"
    };

    /// <summary>
    /// 오디오 파일을 순수 C# ONNX AI로 분석하여 TranscribedNote 목록과 QWERTY 악보 텍스트를 반환합니다.
    /// </summary>
    public async Task<(List<TranscribedNote> Notes, string QwertySheet)> TranscribeAsync(
        string audioFilePath,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(audioFilePath))
        {
            throw new FileNotFoundException("분석할 오디오 파일을 찾을 수 없습니다.", audioFilePath);
        }

        return await Task.Run(() =>
        {
            progress?.Report(5);

            // 1. 오디오 로드 및 22,050Hz 리샘플링
            float[] audioSamples = BasicPitchAudioResampler.LoadAndResample(audioFilePath);
            int originalLength = audioSamples.Length;

            if (originalLength < 1000)
            {
                return (new List<TranscribedNote>(), string.Empty);
            }

            progress?.Report(20);

            // 2. 윈도잉 준비 (30 frames overlap)
            int nOverlappingFrames = 30;
            int overlapLen = nOverlappingFrames * BasicPitchDecoder.FftHop; // 7680
            int hopSize = BasicPitchDecoder.AudioNSamples - overlapLen;     // 36164
            int halfOverlap = overlapLen / 2;                              // 3840

            var paddedAudio = new float[halfOverlap + originalLength];
            Array.Copy(audioSamples, 0, paddedAudio, halfOverlap, originalLength);

            var windowOutputs = new List<float[,]>();
            var onsetOutputs = new List<float[,]>();

            // 3. 인프로세스 ONNX 추론 루프
            using (var model = new BasicPitchModel())
            {
                int totalWindows = (paddedAudio.Length + hopSize - 1) / hopSize;
                int windowIdx = 0;
                float[] windowBuffer = new float[BasicPitchDecoder.AudioNSamples];

                for (int start = 0; start < paddedAudio.Length; start += hopSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    int copyLen = Math.Min(BasicPitchDecoder.AudioNSamples, paddedAudio.Length - start);
                    Array.Copy(paddedAudio, start, windowBuffer, 0, copyLen);
                    if (copyLen < BasicPitchDecoder.AudioNSamples)
                    {
                        Array.Clear(windowBuffer, copyLen, BasicPitchDecoder.AudioNSamples - copyLen);
                    }

                    var (noteOut, onsetOut) = model.RunWindow(windowBuffer);
                    windowOutputs.Add(noteOut);
                    onsetOutputs.Add(onsetOut);

                    windowIdx++;
                    if (progress != null && totalWindows > 0)
                    {
                        int p = 20 + (windowIdx * 60) / totalWindows; // 20% ~ 80%
                        progress.Report(Math.Min(80, p));
                    }
                }
            }

            progress?.Report(82);

            // 4. Unwrap (윈도우 병합)
            var unwrappedFrames = BasicPitchDecoder.UnwrapOutput(windowOutputs, originalLength, nOverlappingFrames);
            var unwrappedOnsets = BasicPitchDecoder.UnwrapOutput(onsetOutputs, originalLength, nOverlappingFrames);

            progress?.Report(88);

            // 5. 음표 디코딩 (Spotify Basic Pitch 최적 파라미터 적용)
            var rawEvents = BasicPitchDecoder.DecodeNotes(
                unwrappedFrames,
                unwrappedOnsets,
                onsetThresh: 0.55f,
                frameThresh: 0.35f,
                minNoteLengthMs: 120.0,
                minFreq: 120.0,
                maxFreq: 2200.0,
                melodiaTrick: true,
                inferOnsets: true,
                energyTol: 11
            );

            progress?.Report(94);

            // 6. 노이즈 필터링 (길이 >= 0.09초, 진폭 >= 0.20)
            var validEvents = new List<BasicPitchDecoder.RawNoteEvent>();
            foreach (var ev in rawEvents)
            {
                double dur = ev.EndTimeSec - ev.StartTimeSec;
                if (dur >= 0.09 && ev.Amplitude >= 0.20)
                {
                    validEvents.Add(ev);
                }
            }

            // 시작 시간, 음정 순 정렬
            validEvents.Sort((a, b) =>
                a.StartTimeSec != b.StartTimeSec
                    ? a.StartTimeSec.CompareTo(b.StartTimeSec)
                    : a.MidiNote.CompareTo(b.MidiNote));

            // TranscribedNote 리스트 구성
            var notes = new List<TranscribedNote>();
            foreach (var ev in validEvents)
            {
                double dur = ev.EndTimeSec - ev.StartTimeSec;
                var (keyChar, isSharp) = MidiToQwerty(ev.MidiNote);
                string name = SolfegeTable[ev.MidiNote % 12];
                double freq = 440.0 * Math.Pow(2.0, (ev.MidiNote - 69) / 12.0);

                notes.Add(new TranscribedNote(
                    TimeSpan.FromSeconds(ev.StartTimeSec),
                    TimeSpan.FromSeconds(dur),
                    ev.MidiNote,
                    Math.Round(freq, 2),
                    keyChar,
                    name,
                    isSharp
                ));
            }

            // 7. QWERTY 악보 포맷팅 (동시 타건 화음 묶음 및 쉼표 표기)
            string sheetText = FormatQwertySheet(notes);

            progress?.Report(100);
            return (notes, sheetText);

        }, cancellationToken);
    }

    public static (char Key, bool IsSharp) MidiToQwerty(int midiNote)
    {
        int norm = midiNote;
        while (norm < 48) norm += 12;
        while (norm > 83) norm -= 12;

        int semitone = norm % 12;
        bool isSharp = (semitone == 1 || semitone == 3 || semitone == 6 || semitone == 8 || semitone == 10);
        int baseMidi = isSharp ? (norm - 1) : norm;

        char key = baseMidi switch
        {
            // Low Octave (C3 ~ B3: 48 ~ 59)
            48 => 'Z', 50 => 'X', 52 => 'C', 53 => 'V', 55 => 'B', 57 => 'N', 59 => 'M',
            // Mid Octave (C4 ~ B4: 60 ~ 71)
            60 => 'A', 62 => 'S', 64 => 'D', 65 => 'F', 67 => 'G', 69 => 'H', 71 => 'J',
            // High Octave (C5 ~ B5: 72 ~ 83)
            72 => 'Q', 74 => 'W', 76 => 'E', 77 => 'R', 79 => 'T', 81 => 'Y', 83 => 'U',
            _ => 'A'
        };

        return (key, isSharp);
    }

    private static string FormatQwertySheet(List<TranscribedNote> notes)
    {
        if (notes.Count == 0) return string.Empty;

        var tokens = new List<string>();
        int i = 0;
        double lastEnd = 0.0;

        while (i < notes.Count)
        {
            var current = notes[i];
            double startSec = current.StartTime.TotalSeconds;
            double durSec = current.Duration.TotalSeconds;

            if (i > 0 && (startSec - lastEnd) > 0.40)
            {
                tokens.Add("-");
            }

            var chordKeys = new List<char> { current.QwertyKey };
            double clusterEnd = startSec + durSec;
            int j = i + 1;

            while (j < notes.Count && (notes[j].StartTime.TotalSeconds - startSec) < 0.06)
            {
                char k = notes[j].QwertyKey;
                if (!chordKeys.Contains(k)) chordKeys.Add(k);
                clusterEnd = Math.Max(clusterEnd, notes[j].StartTime.TotalSeconds + notes[j].Duration.TotalSeconds);
                j++;
            }

            if (chordKeys.Count > 1)
            {
                tokens.Add($"({new string(chordKeys.ToArray())})");
            }
            else
            {
                tokens.Add(chordKeys[0].ToString());
            }

            lastEnd = clusterEnd;
            i = j;
        }

        var sb = new StringBuilder();
        for (int idx = 0; idx < tokens.Count; idx++)
        {
            sb.Append(tokens[idx]);
            if ((idx + 1) % 4 == 0)
            {
                sb.Append("  ");
            }
            else
            {
                sb.Append(' ');
            }

            if ((idx + 1) % 16 == 0)
            {
                sb.AppendLine();
            }
        }

        return sb.ToString().Trim();
    }
}
