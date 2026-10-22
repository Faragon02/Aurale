using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Common.Audio.Analysis;

/// <summary>
/// 오디오 파일을 22,050Hz Mono 32-bit Float 파형으로 디코딩 및 고품질 리샘플링
/// </summary>
public static class BasicPitchAudioResampler
{
    public const int TargetSampleRate = 22050;

    /// <summary>
    /// 오디오 파일(MP3, WAV 등)을 22,050Hz Mono float[] 배열로 읽어옵니다.
    /// </summary>
    public static float[] LoadAndResample(string audioFilePath)
    {
        if (!File.Exists(audioFilePath))
        {
            throw new FileNotFoundException("오디오 파일을 찾을 수 없습니다.", audioFilePath);
        }

        using var reader = new AudioFileReader(audioFilePath);

        ISampleProvider provider = reader;

        // 1. 다채널 -> 모노 다운믹스
        if (provider.WaveFormat.Channels > 1)
        {
            provider = new StereoToMonoSampleProvider(provider)
            {
                LeftVolume = 0.5f,
                RightVolume = 0.5f
            };
        }

        // 2. 샘플 레이트 변환 -> 22,050Hz
        if (provider.WaveFormat.SampleRate != TargetSampleRate)
        {
            provider = new WdlResamplingSampleProvider(provider, TargetSampleRate);
        }

        // 3. 전체 샘플 버퍼로 읽기
        var samples = new List<float>();
        float[] buffer = new float[4096];
        int read;

        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                samples.Add(buffer[i]);
            }
        }

        return samples.ToArray();
    }
}

/// <summary>
/// Stereo ISampleProvider를 Mono ISampleProvider로 다운믹스
/// </summary>
internal class StereoToMonoSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly WaveFormat _waveFormat;
    private float[] _sourceBuffer = Array.Empty<float>();

    public float LeftVolume { get; set; } = 0.5f;
    public float RightVolume { get; set; } = 0.5f;

    public StereoToMonoSampleProvider(ISampleProvider source)
    {
        if (source.WaveFormat.Channels != 2)
        {
            throw new ArgumentException("소스는 2채널(스테레오)이어야 합니다.", nameof(source));
        }

        _source = source;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);
    }

    public WaveFormat WaveFormat => _waveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int sourceSamplesRequired = count * 2;
        if (_sourceBuffer.Length < sourceSamplesRequired)
        {
            _sourceBuffer = new float[sourceSamplesRequired];
        }

        int sourceSamplesRead = _source.Read(_sourceBuffer, 0, sourceSamplesRequired);
        int outIndex = offset;

        for (int i = 0; i < sourceSamplesRead; i += 2)
        {
            buffer[outIndex++] = (_sourceBuffer[i] * LeftVolume) + (_sourceBuffer[i + 1] * RightVolume);
        }

        return sourceSamplesRead / 2;
    }
}
