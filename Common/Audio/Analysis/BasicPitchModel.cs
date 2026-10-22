using System;
using System.IO;
using System.Reflection;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Common.Audio.Analysis;

/// <summary>
/// Basic Pitch ONNX AI 추론 세션 래퍼 (인프로세스 AI 추론)
/// </summary>
public sealed class BasicPitchModel : IDisposable
{
    public const int InputSamples = 43844;
    public const int OutputFrames = 172;
    public const int SemitoneBins = 88;
    public const int ContourBins = 264;

    private const string InputName = "serving_default_input_2:0";
    private const string NoteOutputName = "StatefulPartitionedCall:1";
    private const string OnsetOutputName = "StatefulPartitionedCall:2";
    private const string ContourOutputName = "StatefulPartitionedCall:0";

    private readonly InferenceSession _session;
    private bool _disposed;

    public BasicPitchModel(string? customModelPath = null)
    {
        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            InterOpNumThreads = 1,
            IntraOpNumThreads = Environment.ProcessorCount
        };

        if (!string.IsNullOrEmpty(customModelPath) && File.Exists(customModelPath))
        {
            _session = new InferenceSession(customModelPath, options);
            return;
        }

        // 1. 실행 디렉토리 경로 우선 확인
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string candidate1 = Path.Combine(baseDir, "Audio", "Analysis", "Model", "nmp.onnx");
        if (File.Exists(candidate1))
        {
            _session = new InferenceSession(candidate1, options);
            return;
        }

        string candidate2 = Path.Combine(baseDir, "nmp.onnx");
        if (File.Exists(candidate2))
        {
            _session = new InferenceSession(candidate2, options);
            return;
        }

        // 2. EmbeddedResource 스트림에서 직접 로드
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("Common.Audio.Analysis.Model.nmp.onnx");
        if (stream != null)
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            _session = new InferenceSession(ms.ToArray(), options);
            return;
        }

        throw new FileNotFoundException("Basic Pitch ONNX 모델(nmp.onnx)을 찾을 수 없습니다.");
    }

    /// <summary>
    /// 43,844 샘플 오디오 윈도우 1개를 입력받아 [172, 88] 형태의 Note 및 Onset 행렬을 반환
    /// </summary>
    public (float[,] Note, float[,] Onset) RunWindow(ReadOnlySpan<float> window43844)
    {
        if (window43844.Length != InputSamples)
        {
            throw new ArgumentException($"입력 오디오 윈도우는 정확히 {InputSamples} 샘플이어야 합니다. (현재: {window43844.Length})", nameof(window43844));
        }

        var dimensions = new[] { 1, InputSamples, 1 };
        var tensor = new DenseTensor<float>(dimensions);
        var buffer = tensor.Buffer.Span;

        for (int i = 0; i < InputSamples; i++)
        {
            buffer[i] = window43844[i];
        }

        var inputs = new[]
        {
            NamedOnnxValue.CreateFromTensor(InputName, tensor)
        };

        using var results = _session.Run(inputs, new[] { NoteOutputName, OnsetOutputName });

        var noteTensor = results[0].AsTensor<float>();
        var onsetTensor = results[1].AsTensor<float>();

        var noteMatrix = new float[OutputFrames, SemitoneBins];
        var onsetMatrix = new float[OutputFrames, SemitoneBins];

        for (int f = 0; f < OutputFrames; f++)
        {
            for (int s = 0; s < SemitoneBins; s++)
            {
                noteMatrix[f, s] = noteTensor[0, f, s];
                onsetMatrix[f, s] = onsetTensor[0, f, s];
            }
        }

        return (noteMatrix, onsetMatrix);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _session.Dispose();
            _disposed = true;
        }
    }
}
