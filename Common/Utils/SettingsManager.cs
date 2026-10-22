using System.IO;
using System.Text.Json;

namespace Common.Utils;

/// <summary>
/// JSON 기반 애플리케이션 설정 파일 관리 유틸리티
/// </summary>
public static class SettingsManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// 설정 파일 로드 (파일이 없으면 defaultInstance 반환)
    /// </summary>
    public static T Load<T>(string filePath, T defaultInstance) where T : class
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Save(filePath, defaultInstance);
                return defaultInstance;
            }

            var json = File.ReadAllText(filePath);
            var result = JsonSerializer.Deserialize<T>(json, JsonOptions);
            return result ?? defaultInstance;
        }
        catch
        {
            return defaultInstance;
        }
    }

    /// <summary>
    /// 설정 파일 저장
    /// </summary>
    public static bool Save<T>(string filePath, T data) where T : class
    {
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(filePath, json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
