using System;
using System.Collections.Generic;
using System.Linq;

namespace Common.Audio.Analysis;

/// <summary>
/// Basic Pitch 신경망 출력 텐서를 음표 이벤트(Note Events)로 디코딩하는 C# 알고리즘 엔진
/// </summary>
public static class BasicPitchDecoder
{
    public const int AudioSampleRate = 22050;
    public const int FftHop = 256;
    public const int AudioNSamples = 43844;
    public const int AnnotNFrames = 172;
    public const int AnnotationsFps = 86;
    public const int MidiOffset = 21; // A0 (MIDI 21)
    public const int MaxFreqIdx = 87; // 88 semitones (0 ~ 87)

    public record RawNoteEvent(double StartTimeSec, double EndTimeSec, int MidiNote, double Amplitude);

    /// <summary>
    /// 여러 윈도우의 [172, 88] 출력을 하나의 연속된 시간축 행렬 [nFrames, 88]로 병합(Unwrap)합니다.
    /// </summary>
    public static float[,] UnwrapOutput(List<float[,]> windowOutputs, int originalAudioLengthSamples, int nOverlappingFrames = 30)
    {
        int nOlap = nOverlappingFrames / 2; // 15
        int validFramesPerWindow = AnnotNFrames - nOverlappingFrames; // 172 - 30 = 142
        int totalValidFrames = windowOutputs.Count * validFramesPerWindow;
        int targetFrames = (int)Math.Floor(originalAudioLengthSamples * ((double)AnnotationsFps / AudioSampleRate));
        int finalFrames = Math.Min(totalValidFrames, targetFrames);

        var unwrapped = new float[finalFrames, 88];
        int currentFrame = 0;

        foreach (var window in windowOutputs)
        {
            for (int f = nOlap; f < AnnotNFrames - nOlap; f++)
            {
                if (currentFrame >= finalFrames) break;

                for (int s = 0; s < 88; s++)
                {
                    unwrapped[currentFrame, s] = window[f, s];
                }
                currentFrame++;
            }
            if (currentFrame >= finalFrames) break;
        }

        return unwrapped;
    }

    /// <summary>
    /// Spotify Basic Pitch 논문 및 구현체 기반 다성음(Polyphonic) 음표 디코딩
    /// </summary>
    public static List<RawNoteEvent> DecodeNotes(
        float[,] frames,
        float[,] onsets,
        float onsetThresh = 0.55f,
        float frameThresh = 0.35f,
        double minNoteLengthMs = 120.0,
        double minFreq = 120.0,
        double maxFreq = 2200.0,
        bool melodiaTrick = true,
        bool inferOnsets = true,
        int energyTol = 11)
    {
        int nFrames = frames.GetLength(0);
        int nFreqs = frames.GetLength(1);

        if (nFrames <= 2) return new List<RawNoteEvent>();

        // 1. 주파수 바운더리 마스킹 (120Hz ~ 2,200Hz 컷)
        ConstrainFrequency(onsets, frames, minFreq, maxFreq);

        // 2. 프레임 차분 기반 온셋 보강 (Infer Onsets)
        if (inferOnsets)
        {
            ApplyInferedOnsets(onsets, frames);
        }

        // 3. 온셋 극댓값 피크 탐지
        var onsetPeaks = FindOnsetPeaks(onsets, onsetThresh);

        // 4. 온셋 기반 음표 확장 추적
        var remainingEnergy = (float[,])frames.Clone();
        int minNoteFrames = (int)Math.Round((minNoteLengthMs / 1000.0) * ((double)AudioSampleRate / FftHop));

        var rawNotes = new List<(int startFrame, int endFrame, int midiNote, double amp)>();

        foreach (var (t, f) in onsetPeaks)
        {
            if (t >= nFrames - 1) continue;

            int i = t + 1;
            int k = 0;
            while (i < nFrames - 1 && k < energyTol)
            {
                if (remainingEnergy[i, f] < frameThresh)
                    k++;
                else
                    k = 0;
                i++;
            }

            i -= k;

            if (i - t <= minNoteFrames) continue;

            // 남은 에너지 소거
            ClearEnergy(remainingEnergy, t, i, f);

            // 진폭 계산
            double sumAmp = 0;
            for (int r = t; r < i; r++) sumAmp += frames[r, f];
            double amp = sumAmp / (i - t);

            rawNotes.Add((t, i, f + MidiOffset, amp));
        }

        // 5. Melodia 트릭 (온셋을 놓친 잔류 에너지 영역 추적)
        if (melodiaTrick)
        {
            RunMelodiaTrick(frames, remainingEnergy, rawNotes, frameThresh, minNoteFrames, energyTol);
        }

        // 6. 프레임 번호 -> 실제 초(Seconds) 변환 및 RawNoteEvent 반환
        var result = new List<RawNoteEvent>();
        foreach (var (startF, endF, midi, amp) in rawNotes)
        {
            double startSec = FrameToSeconds(startF);
            double endSec = FrameToSeconds(endF);
            result.Add(new RawNoteEvent(startSec, endSec, midi, amp));
        }

        return result;
    }

    public static double FrameToSeconds(int frameIndex)
    {
        double origTime = frameIndex * (double)FftHop / AudioSampleRate;
        double windowNum = Math.Floor((double)frameIndex / AnnotNFrames);
        double windowOffset = ((double)FftHop / AudioSampleRate) * (AnnotNFrames - ((double)AudioNSamples / FftHop)) + 0.0018;
        return Math.Max(0.0, origTime - (windowOffset * windowNum));
    }

    private static void ConstrainFrequency(float[,] onsets, float[,] frames, double minFreq, double maxFreq)
    {
        int minFreqIdx = Math.Clamp((int)Math.Round(HzToMidi(minFreq) - MidiOffset), 0, MaxFreqIdx);
        int maxFreqIdx = Math.Clamp((int)Math.Round(HzToMidi(maxFreq) - MidiOffset), 0, MaxFreqIdx);

        int nFrames = frames.GetLength(0);

        for (int t = 0; t < nFrames; t++)
        {
            for (int f = 0; f < minFreqIdx; f++)
            {
                onsets[t, f] = 0;
                frames[t, f] = 0;
            }
            for (int f = maxFreqIdx + 1; f <= MaxFreqIdx; f++)
            {
                onsets[t, f] = 0;
                frames[t, f] = 0;
            }
        }
    }

    private static double HzToMidi(double hz)
    {
        return 12.0 * Math.Log2(hz / 440.0) + 69.0;
    }

    private static void ApplyInferedOnsets(float[,] onsets, float[,] frames)
    {
        int nFrames = frames.GetLength(0);
        int nFreqs = frames.GetLength(1);

        var frameDiff = new float[nFrames, nFreqs];
        float maxDiff = 0f;
        float maxOnset = 0f;

        for (int t = 0; t < nFrames; t++)
        {
            for (int f = 0; f < nFreqs; f++)
            {
                if (onsets[t, f] > maxOnset) maxOnset = onsets[t, f];

                if (t >= 2)
                {
                    float d1 = frames[t, f] - frames[t - 1, f];
                    float d2 = frames[t, f] - frames[t - 2, f];
                    float diff = Math.Min(d1, d2);
                    if (diff < 0) diff = 0;
                    frameDiff[t, f] = diff;
                    if (diff > maxDiff) maxDiff = diff;
                }
            }
        }

        if (maxDiff > 1e-6f && maxOnset > 0)
        {
            float scale = maxOnset / maxDiff;
            for (int t = 0; t < nFrames; t++)
            {
                for (int f = 0; f < nFreqs; f++)
                {
                    float diffScaled = frameDiff[t, f] * scale;
                    if (diffScaled > onsets[t, f])
                    {
                        onsets[t, f] = diffScaled;
                    }
                }
            }
        }
    }

    private static List<(int time, int freq)> FindOnsetPeaks(float[,] onsets, float onsetThresh)
    {
        int nFrames = onsets.GetLength(0);
        int nFreqs = onsets.GetLength(1);
        var peaks = new List<(int time, int freq)>();

        for (int f = 0; f < nFreqs; f++)
        {
            for (int t = 1; t < nFrames - 1; t++)
            {
                float val = onsets[t, f];
                if (val >= onsetThresh && val > onsets[t - 1, f] && val > onsets[t + 1, f])
                {
                    peaks.Add((t, f));
                }
            }
        }

        // 역순 정렬 (Python [::-1] 매칭)
        peaks.Sort((a, b) => b.time != a.time ? b.time.CompareTo(a.time) : b.freq.CompareTo(a.freq));
        return peaks;
    }

    private static void ClearEnergy(float[,] energy, int startFrame, int endFrame, int freq)
    {
        for (int r = startFrame; r < endFrame; r++)
        {
            energy[r, freq] = 0;
            if (freq < MaxFreqIdx) energy[r, freq + 1] = 0;
            if (freq > 0) energy[r, freq - 1] = 0;
        }
    }

    private static void RunMelodiaTrick(
        float[,] frames,
        float[,] remainingEnergy,
        List<(int startFrame, int endFrame, int midiNote, double amp)> rawNotes,
        float frameThresh,
        int minNoteFrames,
        int energyTol)
    {
        int nFrames = frames.GetLength(0);
        int nFreqs = frames.GetLength(1);

        while (true)
        {
            float maxVal = 0;
            int maxT = -1;
            int maxF = -1;

            for (int t = 0; t < nFrames; t++)
            {
                for (int f = 0; f < nFreqs; f++)
                {
                    if (remainingEnergy[t, f] > maxVal)
                    {
                        maxVal = remainingEnergy[t, f];
                        maxT = t;
                        maxF = f;
                    }
                }
            }

            if (maxVal <= frameThresh || maxT < 0) break;

            remainingEnergy[maxT, maxF] = 0;

            // 전방 탐색
            int i = maxT + 1;
            int k = 0;
            while (i < nFrames - 1 && k < energyTol)
            {
                if (remainingEnergy[i, maxF] < frameThresh) k++;
                else k = 0;

                remainingEnergy[i, maxF] = 0;
                if (maxF < MaxFreqIdx) remainingEnergy[i, maxF + 1] = 0;
                if (maxF > 0) remainingEnergy[i, maxF - 1] = 0;
                i++;
            }
            int iEnd = i - 1 - k;

            // 후방 탐색
            i = maxT - 1;
            k = 0;
            while (i > 0 && k < energyTol)
            {
                if (remainingEnergy[i, maxF] < frameThresh) k++;
                else k = 0;

                remainingEnergy[i, maxF] = 0;
                if (maxF < MaxFreqIdx) remainingEnergy[i, maxF + 1] = 0;
                if (maxF > 0) remainingEnergy[i, maxF - 1] = 0;
                i--;
            }
            int iStart = i + 1 + k;

            if (iStart < 0) iStart = 0;
            if (iEnd >= nFrames) iEnd = nFrames - 1;

            if (iEnd - iStart > minNoteFrames)
            {
                double sumAmp = 0;
                for (int r = iStart; r < iEnd; r++) sumAmp += frames[r, maxF];
                double amp = sumAmp / Math.Max(1, iEnd - iStart);

                rawNotes.Add((iStart, iEnd, maxF + MidiOffset, amp));
            }
        }
    }
}
