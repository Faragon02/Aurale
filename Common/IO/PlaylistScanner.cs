using System.IO;

namespace Common.IO;

/// <summary>
/// 재생목록 폴더 내 MP3 파일 스캔 유틸리티
/// </summary>
public static class PlaylistScanner
{
    /// <summary>
    /// 지정된 폴더 내의 모든 .mp3 파일 경로 목록을 반환합니다.
    /// </summary>
    public static List<FileInfo> ScanMp3Files(string folderPath)
    {
        var result = new List<FileInfo>();
        if (!Directory.Exists(folderPath))
        {
            return result;
        }

        try
        {
            var dirInfo = new DirectoryInfo(folderPath);
            var files = dirInfo.GetFiles("*.mp3", SearchOption.TopDirectoryOnly);
            result.AddRange(files.OrderByDescending(f => f.LastWriteTime));
        }
        catch
        {
            // 접근 권한 오류 등 무시
        }

        return result;
    }
}
