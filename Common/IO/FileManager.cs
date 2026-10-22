using System.IO;

namespace Common.IO;

/// <summary>
/// 파일 및 경로 관련 안전 처리 유틸리티
/// </summary>
public static class FileManager
{
    private static readonly HashSet<char> InvalidChars = new(Path.GetInvalidFileNameChars());

    /// <summary>
    /// 파일명에 사용할 수 없는 특수문자를 제거하거나 밑줄로 대체하여 안전한 파일명을 반환합니다.
    /// </summary>
    public static string GetSafeFileName(string fileName, char replacement = '_')
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "unnamed";
        }

        var buffer = new char[fileName.Length];
        for (int i = 0; i < fileName.Length; i++)
        {
            char c = fileName[i];
            buffer[i] = InvalidChars.Contains(c) ? replacement : c;
        }

        var cleaned = new string(buffer).Trim();
        return string.IsNullOrEmpty(cleaned) ? "unnamed" : cleaned;
    }

    /// <summary>
    /// 대상 경로에 이미 파일이 존재할 경우 "(1)", "(2)" 형태의 고유한 파일 경로를 생성합니다.
    /// </summary>
    public static string GetUniqueFilePath(string folderPath, string fileName)
    {
        var safeName = GetSafeFileName(fileName);
        var fullPath = Path.Combine(folderPath, safeName);

        if (!File.Exists(fullPath))
        {
            return fullPath;
        }

        var nameWithoutExt = Path.GetFileNameWithoutExtension(safeName);
        var ext = Path.GetExtension(safeName);
        int counter = 1;

        while (File.Exists(fullPath))
        {
            fullPath = Path.Combine(folderPath, $"{nameWithoutExt} ({counter}){ext}");
            counter++;
        }

        return fullPath;
    }

    /// <summary>
    /// 파일 이름 안전하게 변경
    /// </summary>
    public static bool TryRenameFile(string originalFullPath, string newFileName, out string updatedFullPath)
    {
        updatedFullPath = originalFullPath;
        try
        {
            if (!File.Exists(originalFullPath)) return false;

            var dir = Path.GetDirectoryName(originalFullPath);
            if (string.IsNullOrEmpty(dir)) return false;

            var safeName = GetSafeFileName(newFileName);
            if (!safeName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            {
                safeName += ".mp3";
            }

            var targetPath = Path.Combine(dir, safeName);
            if (targetPath.Equals(originalFullPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (File.Exists(targetPath))
            {
                return false; // 이미 동일한 이름의 파일 존재
            }

            File.Move(originalFullPath, targetPath);
            updatedFullPath = targetPath;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
