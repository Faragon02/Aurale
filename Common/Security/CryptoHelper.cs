using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Common.Security;

/// <summary>
/// 범용 AES-256 데이터 및 자산 암복호화 헬퍼 (디스크에 평문을 남기지 않고 메모리에서 직접 로드)
/// </summary>
public static class CryptoHelper
{
    private const int KeySize = 256;
    private const int BlockSize = 128;
    private const int Iterations = 50_000;

    /// <summary>
    /// 평문 바이트 배열을 AES-256으로 암호화합니다. (Salt 및 IV가 앞에 자동 결합됨)
    /// </summary>
    public static byte[] EncryptBytes(byte[] plainBytes, string password)
    {
        if (plainBytes == null || plainBytes.Length == 0) return Array.Empty<byte>();

        byte[] salt = RandomNumberGenerator.GetBytes(16);
        using var keyDerivation = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        byte[] key = keyDerivation.GetBytes(KeySize / 8);

        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.BlockSize = BlockSize;
        aes.Key = key;
        aes.GenerateIV();
        byte[] iv = aes.IV;

        using var ms = new MemoryStream();
        ms.Write(salt, 0, salt.Length);
        ms.Write(iv, 0, iv.Length);

        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        {
            cs.Write(plainBytes, 0, plainBytes.Length);
            cs.FlushFinalBlock();
        }

        return ms.ToArray();
    }

    /// <summary>
    /// 암호화된 바이트 배열을 복호화하여 평문 바이트 배열로 반환합니다.
    /// </summary>
    public static byte[] DecryptBytes(byte[] cipherBytes, string password)
    {
        if (cipherBytes == null || cipherBytes.Length < 32)
        {
            throw new ArgumentException("유효하지 않은 암호화 데이터입니다.", nameof(cipherBytes));
        }

        byte[] salt = new byte[16];
        byte[] iv = new byte[16];
        Buffer.BlockCopy(cipherBytes, 0, salt, 0, 16);
        Buffer.BlockCopy(cipherBytes, 16, iv, 0, 16);

        using var keyDerivation = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        byte[] key = keyDerivation.GetBytes(KeySize / 8);

        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.BlockSize = BlockSize;
        aes.Key = key;
        aes.IV = iv;

        using var msInput = new MemoryStream(cipherBytes, 32, cipherBytes.Length - 32);
        using var cs = new CryptoStream(msInput, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var msOutput = new MemoryStream();

        cs.CopyTo(msOutput);
        return msOutput.ToArray();
    }

    /// <summary>
    /// 암호화된 파일을 읽어 디스크에 쓰지 않고 메모리 스트림(MemoryStream)으로 직접 복호화합니다.
    /// (AI 모델, 음원 파일, 이미지 등 보호 시 사용)
    /// </summary>
    public static MemoryStream DecryptFileToMemory(string encryptedFilePath, string password)
    {
        if (!File.Exists(encryptedFilePath))
        {
            throw new FileNotFoundException("암호화된 파일을 찾을 수 없습니다.", encryptedFilePath);
        }

        byte[] cipherBytes = File.ReadAllBytes(encryptedFilePath);
        byte[] plainBytes = DecryptBytes(cipherBytes, password);
        return new MemoryStream(plainBytes);
    }

    /// <summary>
    /// 문자열 암호화 (Base64 반환)
    /// </summary>
    public static string EncryptString(string plainText, string password)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] cipherBytes = EncryptBytes(plainBytes, password);
        return Convert.ToBase64String(cipherBytes);
    }

    /// <summary>
    /// Base64 암호화 문자열 복호화
    /// </summary>
    public static string DecryptString(string cipherBase64, string password)
    {
        if (string.IsNullOrEmpty(cipherBase64)) return string.Empty;
        byte[] cipherBytes = Convert.FromBase64String(cipherBase64);
        byte[] plainBytes = DecryptBytes(cipherBytes, password);
        return Encoding.UTF8.GetString(plainBytes);
    }
}
