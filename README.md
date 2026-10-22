# 📼 AuraLe (오라레)

<p align="center">
  <img src="Assets/reel.png" width="128" height="128" alt="AuraLe Reel Logo" />
  <br />
  <b>Retro Cassette Deck MP3 Player & Spotify Basic Pitch AI Transcriber</b>
  <br />
  <i>.NET 8.0 WPF 기반의 레트로 사이버펑크 감성 카세트 덱 & 21키 QWERTY 인게임 악보 채보 플레이어</i>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 8.0" />
  <img src="https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows&logoColor=white" alt="Windows Desktop" />
  <img src="https://img.shields.io/badge/AI_Engine-Spotify_Basic_Pitch_(C#)-1DB954?logo=spotify&logoColor=white" alt="Spotify Basic Pitch" />
  <img src="https://img.shields.io/badge/Inference-ONNX_Runtime_1.31-005CED" alt="ONNX Runtime" />
  <img src="https://img.shields.io/badge/Audio-NAudio_WASAPI-orange" alt="NAudio" />
  <img src="https://img.shields.io/badge/License-MIT-green.svg" alt="License" />
</p>

<p align="center">
  <a href="../../releases/latest">
    <img src="https://img.shields.io/badge/📥_최신_버전_다운로드-AuraLe.exe_(무설치_단일_실행_파일)-00F0FF?style=for-the-badge&logo=windows&logoColor=black" alt="Download Latest Release" />
  </a>
  <br />
  <sub>⚡ 복잡한 설치나 개발 환경 구성 없이, <code>AuraLe.exe</code> 단 하나만 받아 더블클릭하면 즉시 실행됩니다!</sub>
</p>

---

## 🏃 빠른 시작 (Quick Start)

### 🎵 1. 프로그램 실행만 하고 싶다면 (일반 사용자)
1. 상단의 **[📥 최신 버전 다운로드]** 배너를 클릭하거나 우측 **[Releases]** 탭으로 이동합니다.
2. `AuraLe.exe` 단일 파일을 다운로드합니다.
3. 더블클릭하여 바로 실행합니다! (에셋, 사운드 폰트, AI 모델이 모두 바이너리 내부에 내장되어 있어 추가 파일이 필요 없습니다.)
4. `C:\AuraLe\Music` 폴더에 소장 중인 MP3 파일을 넣고 레트로 감성 음악 감상 및 21키 악보 자동 채보를 즐겨보세요!

### 💻 2. 소스코드를 분석하거나 직접 빌드하고 싶다면 (개발자)
하단의 [🚀 빌드 및 실행 방법](#-빌드-및-실행-방법-build--run) 섹션을 참고하세요.

---

## ✨ 프로젝트 개요 (Overview)

**AuraLe**는 감각적인 레트로 카세트 워크맨 감성에 최신 오디오 인공지능 기술을 결합한 차세대 데스크톱 오디오 플레이어입니다.

단순한 음악 재생을 넘어, **Spotify Basic Pitch AI 엔진을 100% 순수 C# 인프로세스로 포팅**하여 별도의 Python 런타임 설치 없이 MP3 음원을 **0.2초 만에 분석하여 3옥타브 21키 QWERTY 인게임 악보로 자동 변환**합니다. 또한 내장된 4종 가상 신디사이저로 키보드를 직접 연주하거나 자동 채보된 악보를 실시간으로 합주할 수 있습니다.

---

## 🚀 주요 특징 (Key Features)

### 1. 📼 Retro Cassette Deck & Tape Rack
- **회전 릴 애니메이션**: 음악 재생 상태 및 템포에 연동되어 부드럽게 회전하는 기어 릴 휠
- **독립형 테이프 서랍(Playlist Rack)**: 본체 우측에 도킹되어 보관된 테이프 목록을 한눈에 탐색하고 관리
- **커스텀 비주얼 커스터마이징**:
  - 나만의 카세트 배경 일러스트 등록 및 가로 위치(-200~+200px) 조절
  - 유리 투명도(Glass Opacity) 조절 및 네온 발광(Glow) 블러 효과
  - 릴 휠 에셋 PNG 실시간 교체 및 네온/그라데이션 테마 컬러 팔레트 지원

### 2. 🤖 Spotify Basic Pitch AI 채보 엔진 (Pure C# In-Process)
- **Zero Python Dependency**: 파이썬 인터프리터나 가상환경 없이 `Microsoft.ML.OnnxRuntime`과 NAudio 기반으로 C# 내에서 100% 인프로세스 구동
- **20배 초고속화**: 기존 CLI 연동 대비 **약 20배 고속화 (5초 ➔ 220ms)**
- **완벽한 알고리즘 포팅**:
  - 22,050Hz Mono 고품질 리샘플링
  - 30프레임 오버랩 슬라이싱 윈도우 추론 & 시간역순 피크 탐지 (Spotify 원작 Melodia Trick 100% 일치)
  - 저음역 럼블 제거 & 초고주파 노이즈 차단 (120Hz ~ 2,200Hz BPF)
- **게임 연주용 21키 QWERTY 악보 출력**:
  - C3 ~ B5 음역대를 3옥타브 21키(`Q~U`, `A~J`, `Z~M`)로 자동 매핑
  - 동시 타건 화음 괄호 결합 `(AG)` 및 쉼표 휴지부(`-`) 자동 포맷팅

### 3. 🎹 4종 음색 가상 QWERTY 신디사이저 (Virtual Instrument)
- **QWERTY 키보드 연주**: 실제 PC 키보드 타건 및 화면 터치/클릭으로 21키 실시간 화음 연주 지원
- **4가지 고유 음색 프리셋 탑재**:
  1. 🎹 **피아노 (Piano EP)**: 해머 타현 배음과 부드러운 릴리즈 감쇄의 영롱한 사운드
  2. 🪕 **하프 (Acoustic Harp)**: 맑고 청량한 타현 플럭(Pluck) 음색
  3. 🎸 **통기타 (Acoustic Guitar)**: **Karplus-Strong 물리 모델링** 기반, 현을 튕기는 피크 찰현감과 리얼한 어쿠스틱 스트링 진동 구현
  4. ⚡ **일렉기타 (Electric Guitar)**: 진공관 튜브 오버드라이브 비선형 새츄레이션 & 캐비닛 하이컷 필터의 묵직한 드라이브 톤
- **자동 악보 연주(Auto Play)**: AI가 추출한 악보를 원하는 템포(BPM)로 자동 타건 및 프리뷰 연주
- **반음(♯) 모드 토글**: 클릭 한 번으로 모든 음계를 샤프(#) 반음으로 변환 연주

### 4. 📁 스마트 로컬 라이브러리
- 복잡한 초기 설정 없이, 기동 시 `C:\AuraLe\Music` 폴더를 기본 보관함으로 자동 감지 및 로드
- 사용자가 원하는 다른 음악 폴더로 언제든 자유롭게 변경 가능

---

## 🛠️ 기술 스택 (Architecture & Tech Stack)

| 구분 | 기술 / 라이브러리 | 용도 |
| :--- | :--- | :--- |
| **Framework** | .NET 8.0-windows (WPF) | C# 12 최신 문법 및 XAML 하드웨어 가속 UI |
| **Architecture** | MVVM Pattern | `CommunityToolkit.Mvvm` 8.4 기반 반응형 구조 |
| **Audio Engine** | NAudio 2.2.1 | WASAPI 저지연 오디오 재생, 리샘플링, 신디사이저 믹싱 |
| **AI Inference** | Microsoft.ML.OnnxRuntime 1.31 | Basic Pitch 경량화 ONNX 신경망 모델 인프로세스 텐서 추론 |
| **Embedded Model**| `Model/nmp.onnx` (225KB) | 어셈블리 리소스로 내장되어 외부 모델 파일 누락 원천 차단 |
| **DSP Algorithm** | Karplus-Strong / IIR Filter | 현악기 물리 모델링 합성 및 아날로그 튜브 오버드라이브 에뮬레이션 |

---

## 📂 프로젝트 구조 (Project Structure)

```text
AuraLe/                              # AuraLe 솔루션 루트
├── Core/                            # 공용 핵심 라이브러리 (순수 C# 엔진)
│   ├── Audio/
│   │   ├── Analysis/                # Spotify Basic Pitch AI C# 엔진
│   │   │   ├── BasicPitchModel.cs   # ONNX 런타임 세션 및 텐서 추론
│   │   │   ├── BasicPitchDecoder.cs # 윈도우 Unwrap & 피크 탐지 디코더
│   │   │   ├── BasicPitchTranscriber.cs # 21키 QWERTY 매핑 및 악보 생성
│   │   │   └── Model/nmp.onnx       # 인베디드 신경망 가중치 모델
│   │   ├── Synth/                   # 4종 가상 신디사이저 음원 합성 엔진
│   │   │   ├── SynthVoice.cs        # ADSR, Karplus-Strong 통기타, 튜브 일렉
│   │   │   └── SynthEngine.cs       # 동시 발음(Polyphony) 믹서
│   │   └── AudioPlayer.cs           # NAudio 기반 스트리밍 플레이어
│   └── Security/                    # 안티 디버깅 및 자가 무결성 가드
│
├── Assets/                          # 릴 회전 휠, 로고, app.ico 등 리소스
├── ViewModels/                      # MainViewModel, SynthViewModel
├── Views/
│   ├── CassetteDeckView.xaml        # 메인 카세트 덱 플레이어 화면
│   ├── PlaylistDrawerView.xaml      # 우측 테이프 랙 서랍
│   ├── SynthDeckView.xaml           # 21키 QWERTY 건반 & 악보 채보 화면
│   ├── HomeView.xaml                # 대시보드 홈 화면
│   └── SettingsView.xaml            # 테마/배경/보관함 폴더 환경설정
├── MainWindow.xaml                  # 통합 윈도우 쉘
├── AuraLe.csproj                    # 프로젝트 파일
└── README.md                        # 공식 가이드 문서
```

---

## 🚀 빌드 및 실행 방법 (Build & Run)

### 1. 사전 요구사항
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 이상이 설치되어 있어야 합니다.

### 2. 개발 모드 빌드 및 실행
```bash
# 솔루션 복원 및 빌드
dotnet build

# 애플리케이션 즉시 실행
dotnet run
```

### 3. 무설치 단일 실행 파일(.exe) 배포 빌드
모든 이미지 에셋과 AI 모델이 EXE 바이너리 내부에 100% 임베드되므로, 별도 폴더 없이 **단일 실행 파일 하나로 깨끗하게 배포**할 수 있습니다.

```bash
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./dist
```
> 빌드 완료 후 `./dist/AuraLe.exe` 단일 파일만 복사하여 즉시 어디서나 실행할 수 있습니다.

---

## 📜 라이선스 (License)

- 이 프로젝트의 메인 코드는 [MIT License](LICENSE)를 따릅니다.
- 음원 채보 신경망 모델은 [Spotify Basic Pitch](https://github.com/spotify/basic-pitch) (Apache-2.0 License)의 아키텍처를 기반으로 포팅되었습니다.
- 사용된 오디오 엔진 및 MVVM 컴포넌트는 [NAudio](https://github.com/naudio/NAudio) 및 [.NET Community Toolkit](https://github.com/CommunityToolkit/dotnet)을 활용합니다.
