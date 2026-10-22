# Common Security Module (범용 보안 툴킷 뼈대)

.NET 8 기반 Windows 데스크톱 애플리케이션을 위한 **재사용 가능한 범용 보안 인프라**입니다.
외부 패키지 설치 없이 순수 .NET 기본 라이브러리 및 Win32 API로만 구현되어, 다른 프로젝트로 클래스 파일만 복사하여 즉시 활용할 수 있습니다.

---

## 📁 구성 요소

### 1. `CryptoHelper.cs` (자산 & 데이터 암호화)
- **알고리즘**: AES-256 + PBKDF2 (SHA-256, 50,000 Iterations)
- **주요 기능**:
  - `EncryptBytes(plainBytes, password)` / `DecryptBytes(cipherBytes, password)`
  - `DecryptFileToMemory(path, password)`: 디스크에 평문 파일을 쓰지 않고 **RAM 메모리 상에서 직접 복호화 스트림(`MemoryStream`)** 반환 (AI 모델, 음원, DB 보호)
  - `EncryptString` / `DecryptString`: API 키나 설정 문자열 보호

```csharp
// 사용 예시: 암호화된 AI 모델을 디스크에 풀지 않고 메모리에서 바로 ONNX 세션 생성
using var ms = CryptoHelper.DecryptFileToMemory("model.enc", "MySecretKey");
var session = new InferenceSession(ms.ToArray());
```

---

### 2. `SecurityGuard.cs` (안티 디버깅 & 무결성 검증)
- **주요 기능**:
  - `IsDebuggerDetected()`: 관리형(`.NET`) 및 네이티브(`x64dbg`, `Cheat Engine` 등) 디버거 연결 동시 탐지
  - `CalculateSelfHash()`: 실행 중인 바이너리의 SHA-256 해시 계산 (변조/크랙 감지)
  - `StartWatchdog(intervalMs, onThreatDetected)`: 백그라운드 주기 감시 (디버거 침투 시 즉각 방어/종료)

```csharp
// 사용 예시: App.xaml.cs 또는 Program.cs 시작점에 배치
SecurityGuard.StartWatchdog(intervalMs: 2000, onThreatDetected: () =>
{
    // 디버거 침투 시 조용히 프로세스 종료
    Environment.Exit(0);
});
```

---

## 🚀 다른 프로젝트에서 단일 파일(Single-File) 배포 템플릿
프로젝트의 `.csproj`에 아래 PropertyGroup을 추가하면 DLL 및 리소스가 단 하나의 `.exe`로 통합 패키징됩니다:

```xml
<PropertyGroup>
  <PublishSingleFile>true</PublishSingleFile>
  <SelfContained>true</SelfContained>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
</PropertyGroup>
```
