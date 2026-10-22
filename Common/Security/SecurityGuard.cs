using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace Common.Security;

/// <summary>
/// 범용 안티 디버깅 및 무결성 검증 가드 (프로세스 분석 및 메모리 변조 방어 뼈대)
/// </summary>
public static class SecurityGuard
{
    #region WinAPI Native Methods

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool IsDebuggerPresent();

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, ref bool isDebuggerPresent);

    #endregion

    private static Timer? _watchdogTimer;

    /// <summary>
    /// 현재 프로세스에 디버거가 부착되어 있는지 종합 검사합니다.
    /// (관리형 디버거, 네이티브 디버거, 원격 디버거 모두 탐지)
    /// </summary>
    public static bool IsDebuggerDetected()
    {
        // 1. .NET 관리형 디버거 감지 (Visual Studio, Rider 등)
        if (Debugger.IsAttached) return true;

        // 2. Win32 커널 디버거 플래그 감지 (x64dbg, OllyDbg 등)
        try
        {
            if (IsDebuggerPresent()) return true;

            bool isRemote = false;
            if (CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, ref isRemote) && isRemote)
            {
                return true;
            }
        }
        catch
        {
            // P/Invoke 실패 시 기본 .NET 상태로 통과
        }

        return false;
    }

    /// <summary>
    /// 현재 실행 중인 실행 파일(EXE/DLL)의 SHA-256 해시값을 계산하여 반환합니다.
    /// (바이너리 패치 및 크랙 감지용)
    /// </summary>
    public static string CalculateSelfHash()
    {
        string? location = Environment.ProcessPath;
        if (string.IsNullOrEmpty(location) || !File.Exists(location))
        {
            location = Assembly.GetEntryAssembly()?.Location;
        }

        if (string.IsNullOrEmpty(location) || !File.Exists(location))
        {
            return string.Empty;
        }

        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(location);
        byte[] hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// 백그라운드에서 주기적으로 디버거 침투를 감시하는 와치독(Watchdog)을 시작합니다.
    /// </summary>
    /// <param name="intervalMs">감시 주기 (기본값: 1500ms)</param>
    /// <param name="onThreatDetected">디버거 감지 시 실행할 콜백 (null일 경우 프로세스 즉시 종료)</param>
    public static void StartWatchdog(int intervalMs = 1500, Action? onThreatDetected = null)
    {
        StopWatchdog();

        _watchdogTimer = new Timer(_ =>
        {
            if (IsDebuggerDetected())
            {
                if (onThreatDetected != null)
                {
                    onThreatDetected.Invoke();
                }
                else
                {
                    // 기본 방어: 강제 조용히 종료 (크래커의 디버깅 루틴 차단)
                    Environment.Exit(0);
                }
            }
        }, null, intervalMs, intervalMs);
    }

    /// <summary>
    /// 디버거 감시 와치독을 중지합니다.
    /// </summary>
    public static void StopWatchdog()
    {
        _watchdogTimer?.Dispose();
        _watchdogTimer = null;
    }
}
