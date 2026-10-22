using System.IO;
using Common.Utils;
using NAudio.Wave;

namespace AuraLe.Models;

/// <summary>
/// 재생목록 내 MP3 트랙 정보 모델
/// </summary>
public class TrackItem
{
    public string Title { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public string DurationText => TimeFormatter.Format(Duration);
    public long FileSizeBytes { get; set; }
    public DateTime ModifiedTime { get; set; }

    public TrackItem(string filePath)
    {
        FilePath = filePath;
        Title = Path.GetFileNameWithoutExtension(filePath);
        if (File.Exists(filePath))
        {
            var info = new FileInfo(filePath);
            FileSizeBytes = info.Length;
            ModifiedTime = info.LastWriteTime;

            try
            {
                using var reader = new AudioFileReader(filePath);
                Duration = reader.TotalTime;
            }
            catch
            {
                Duration = TimeSpan.Zero;
            }
        }
    }
}
